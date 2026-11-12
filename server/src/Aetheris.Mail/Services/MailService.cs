using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Players;
using Aetheris.Core.Time;
using Aetheris.Mail.Api;
using Aetheris.Mail.Configuration;
using Aetheris.Mail.Data;
using Aetheris.Mail.Data.Entities;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Mail.Services;

/// <summary>
/// The mail of a player (task 00060): the list of the letters, the folders, the actions with the
/// letters and the drafts.
/// </summary>
/// <remarks>
/// <para>
/// The list is read in one query of the boxes of the player with their letters and their
/// categories; the names and the races of the senders come in one read of the world and of the
/// directory of the players, so the list never asks for a sender one by one (task 00060,
/// section 3.1, point 4).
/// </para>
/// <para>
/// The counters of the folders and of the categories are counted by the rows of the boxes, not by
/// a second pass over the letters of the list, so the numbers of the screen and the list always
/// agree.
/// </para>
/// </remarks>
public sealed class MailService
{
    /// <summary>
    /// The number of the words of the preview of a letter in the list.
    /// </summary>
    private const int PreviewWords = 12;

    /// <summary>
    /// The largest number of the letters of one page of the list.
    /// </summary>
    private const int PageSize = 50;

    /// <summary>
    /// The default of the number of the days a letter lies in the bin, when the settings do not
    /// name it yet.
    /// </summary>
    private const long DefaultTrashDays = 7;

    private readonly MailDbContext _dbContext;
    private readonly IGameConfig _config;
    private readonly IGameClock _clock;
    private readonly IPlayerDirectory _players;
    private readonly IWorldModule _world;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="config">The configuration of the game.</param>
    /// <param name="clock">The clock of the game.</param>
    /// <param name="players">The nicknames of the players.</param>
    /// <param name="world">The world.</param>
    public MailService(
        MailDbContext dbContext,
        IGameConfig config,
        IGameClock clock,
        IPlayerDirectory players,
        IWorldModule world)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(world);

        _dbContext = dbContext;
        _config = config;
        _clock = clock;
        _players = players;
        _world = world;
    }

    /// <summary>
    /// Reads the mail of a player: the folders, the categories and the list of the letters.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="folderCode">The folder to read, or <see langword="null"/> for every folder.</param>
    /// <param name="categoryId">The category to read, or <see langword="null"/> for every category.</param>
    /// <param name="search">What to look for in the subject and in the text, or <see langword="null"/> for everything.</param>
    /// <param name="page">The page of the list, from one.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The mail of the player.</returns>
    public async Task<MailViewDto> ReadAsync(
        string playerId,
        string? folderCode,
        long? categoryId,
        string? search,
        int page,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        IQueryable<MailBoxEntity> boxes = _dbContext.MailBoxes.AsNoTracking().Where(box => box.PlayerId == playerId);

        // The list of the screen shows the letters of the folder, and the letters of the bin are
        // out of it until the player asks for the bin.
        if (!string.IsNullOrEmpty(folderCode))
        {
            boxes = boxes.Where(box => box.FolderCode == folderCode);
        }
        else
        {
            boxes = boxes.Where(box => box.FolderCode != MailFolders.Trash);
        }

        if (categoryId is { } category)
        {
            boxes = boxes.Where(box => _dbContext.MailMessages.Any(message => message.Id == box.MessageId && message.CategoryId == category));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string needle = search.Trim();
            boxes = boxes.Where(box => _dbContext.MailMessages.Any(message =>
                message.Id == box.MessageId
                && (message.Subject.Contains(needle) || message.Body.Contains(needle))));
        }

        List<MailBoxEntity> selected = await boxes
            .OrderByDescending(box => box.CreatedAt)
            .ThenByDescending(box => box.Id)
            .Skip((Math.Max(page, 1) - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // The letters of the page come with their categories in one query.
        long[] messageIds = [.. selected.Select(box => box.MessageId)];
        Dictionary<long, MailMessageEntity> messages = await _dbContext.MailMessages.AsNoTracking()
            .Where(message => messageIds.Contains(message.Id))
            .ToDictionaryAsync(message => message.Id, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<long, MailCategoryEntity> categories = await _dbContext.MailCategories.AsNoTracking()
            .ToDictionaryAsync(category => category.Id, cancellationToken)
            .ConfigureAwait(false);

        // The senders come in one read of the directory and of the world.
        string[] senderIds = [.. messages.Values
            .Where(message => message.SenderPlayerId is not null)
            .Select(message => message.SenderPlayerId!)
            .Distinct()];
        IReadOnlyDictionary<string, string> nicknames = await _players
            .GetNicknamesAsync(senderIds, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyDictionary<string, PlayerRaceBrief> races = await _world
            .GetRaceBriefsAsync(senderIds, cancellationToken)
            .ConfigureAwait(false);

        List<MailLetterDto> letters =
        [
            .. selected
                .Where(box => messages.ContainsKey(box.MessageId))
                .Select(box => ToLetter(box, messages[box.MessageId], categories, nicknames, races)),
        ];

        List<MailFolderDto> folders = await ReadFoldersAsync(playerId, cancellationToken).ConfigureAwait(false);
        List<MailCategoryDto> categoryDtos = await ReadCategoriesAsync(playerId, cancellationToken).ConfigureAwait(false);

        return new MailViewDto(
            folders,
            categoryDtos,
            letters,
            folders.Where(folder => folder.Code == MailFolders.Incoming).Select(folder => folder.UnreadCount).FirstOrDefault(),
            letters.Count,
            _clock.GameNow);
    }

    /// <summary>
    /// Reads one letter of a player with its whole text, and marks it as read.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="messageId">The letter.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The letter, or <see langword="null"/> when the player has no such letter.</returns>
    public async Task<MailLetterDetailDto?> ReadOneAsync(
        string playerId,
        long messageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        MailBoxEntity? box = await _dbContext.MailBoxes.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.PlayerId == playerId && entry.MessageId == messageId, cancellationToken)
            .ConfigureAwait(false);

        if (box is null)
        {
            return null;
        }

        MailMessageEntity message = await _dbContext.MailMessages.AsNoTracking()
            .SingleAsync(entry => entry.Id == messageId, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<long, MailCategoryEntity> categories = await _dbContext.MailCategories.AsNoTracking()
            .ToDictionaryAsync(category => category.Id, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyDictionary<string, string> nicknames = await _players
            .GetNicknamesAsync(message.SenderPlayerId is null ? [] : [message.SenderPlayerId], cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyDictionary<string, PlayerRaceBrief> races = await _world
            .GetRaceBriefsAsync(message.SenderPlayerId is null ? [] : [message.SenderPlayerId], cancellationToken)
            .ConfigureAwait(false);

        // The letter the player opens is read. The box of the letter is read again, because the
        // one that was read with the query carries no changes and would say the letter is not read.
        if (!box.IsRead)
        {
            MailBoxEntity? stored = await _dbContext.MailBoxes
                .SingleAsync(entry => entry.Id == box.Id, cancellationToken)
                .ConfigureAwait(false);

            stored.IsRead = true;
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            box.IsRead = true;
        }

        MailLetterDto letter = ToLetter(box, message, categories, nicknames, races);

        return new MailLetterDetailDto(letter, message.Body, message.TextKey, message.TextParameters);
    }

    /// <summary>
    /// Counts the letters of a player that are not read.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>How many letters of the folder «incoming» are not read.</returns>
    public async Task<int> ReadUnreadCountAsync(string playerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return await _dbContext.MailBoxes.AsNoTracking()
            .Where(box => box.PlayerId == playerId && box.FolderCode == MailFolders.Incoming && !box.IsRead && !box.IsDeleted)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Marks every letter of a player of a folder as read.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="folderCode">The folder, or <see langword="null"/> for every folder.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>How many letters were marked.</returns>
    public async Task<int> MarkAllReadAsync(
        string playerId,
        string? folderCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        List<MailBoxEntity> boxes = await _dbContext.MailBoxes
            .Where(box => box.PlayerId == playerId && !box.IsRead && !box.IsDeleted)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        int marked = 0;

        foreach (MailBoxEntity box in boxes)
        {
            if (folderCode is not null && box.FolderCode != folderCode)
            {
                continue;
            }

            box.IsRead = true;
            marked++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return marked;
    }

    /// <summary>
    /// Saves a letter of a player or takes it back from the saved ones.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="messageId">The letter.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the letter was saved, <see langword="false"/> when it was
    /// taken back, and <see langword="null"/> when the player has no such letter.</returns>
    public async Task<bool?> ToggleSavedAsync(
        string playerId,
        long messageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        MailBoxEntity? box = await _dbContext.MailBoxes
            .SingleOrDefaultAsync(entry => entry.PlayerId == playerId && entry.MessageId == messageId, cancellationToken)
            .ConfigureAwait(false);

        if (box is null)
        {
            return null;
        }

        bool saved = !box.IsSaved;
        box.IsSaved = saved;

        // A saved letter lies in the folder «saved», and it is not in the bin.
        box.FolderCode = saved ? MailFolders.Saved : box.FolderCode == MailFolders.Saved ? MailFolders.Incoming : box.FolderCode;
        box.IsDeleted = false;
        box.DeletedAt = null;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return saved;
    }

    /// <summary>
    /// Moves a letter of a player to the bin, or takes it back from the bin.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="messageId">The letter.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the letter went to the bin, <see langword="false"/> when
    /// it came back, and <see langword="null"/> when the player has no such letter.</returns>
    public async Task<bool?> ToggleDeletedAsync(
        string playerId,
        long messageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        MailBoxEntity? box = await _dbContext.MailBoxes
            .SingleOrDefaultAsync(entry => entry.PlayerId == playerId && entry.MessageId == messageId, cancellationToken)
            .ConfigureAwait(false);

        if (box is null)
        {
            return null;
        }

        bool deleted = !box.IsDeleted;
        box.IsDeleted = deleted;
        box.DeletedAt = deleted ? _clock.GameNow : null;
        box.FolderCode = deleted
            ? MailFolders.Trash
            : box.IsSaved ? MailFolders.Saved : box.FolderCode == MailFolders.Trash ? MailFolders.Incoming : box.FolderCode;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return deleted;
    }

    /// <summary>
    /// Empties the bin of a player: the letters of the bin are gone for good.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>How many letters were removed.</returns>
    public async Task<int> EmptyTrashAsync(string playerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        List<MailBoxEntity> boxes = await _dbContext.MailBoxes
            .Where(box => box.PlayerId == playerId && box.IsDeleted)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        int removed = boxes.Count;

        foreach (MailBoxEntity box in boxes)
        {
            MailMessageEntity? message = await _dbContext.MailMessages
                .SingleOrDefaultAsync(entry => entry.Id == box.MessageId, cancellationToken)
                .ConfigureAwait(false);

            _dbContext.MailBoxes.Remove(box);

            // A letter of the game lies in one box only, so it goes with it. A letter of a player
            // lies in two boxes, so it stays as long as the other box holds it.
            if (message is not null && await _dbContext.MailBoxes.AnyAsync(entry => entry.MessageId == message.Id, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            if (message is not null)
            {
                _dbContext.MailMessages.Remove(message);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return removed;
    }

    /// <summary>
    /// Removes the letters of the bins that lay there longer than the time of the settings.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>How many letters were removed.</returns>
    /// <remarks>
    /// The work of the event of the scheduler: nothing asks the table of the bin (task 00060,
    /// section 3.1, point 4).
    /// </remarks>
    public async Task<int> CleanTrashAsync(CancellationToken cancellationToken = default)
    {
        int days = (int)_config.GetInt64(MailConfigKeys.TrashDays, DefaultTrashDays);
        DateTimeOffset before = _clock.GameNow.AddDays(-days);

        List<MailBoxEntity> boxes = await _dbContext.MailBoxes
            .Where(box => box.IsDeleted && box.DeletedAt != null && box.DeletedAt <= before)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (boxes.Count == 0)
        {
            return 0;
        }

        foreach (MailBoxEntity box in boxes)
        {
            _dbContext.MailBoxes.Remove(box);
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return boxes.Count;
    }

    /// <summary>
    /// Reads the folders of a player with their counters.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The folders of the mail.</returns>
    private async Task<List<MailFolderDto>> ReadFoldersAsync(string playerId, CancellationToken cancellationToken)
    {
        var counts = await _dbContext.MailBoxes.AsNoTracking()
            .Where(box => box.PlayerId == playerId)
            .GroupBy(box => box.FolderCode)
            .Select(group => new
            {
                Code = group.Key,
                Unread = group.Count(box => !box.IsRead && !box.IsDeleted),
                Total = group.Count(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, (int Unread, int Total)> byCode = counts.ToDictionary(
            row => row.Code,
            row => (row.Unread, row.Total));

        return
        [
            .. MailFolders.All.Select(code =>
            {
                byCode.TryGetValue(code, out (int Unread, int Total) found);

                return new MailFolderDto(code, found.Unread, found.Total);
            }),
        ];
    }

    /// <summary>
    /// Reads the categories of the letters of a player with their counters.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The categories of the mail.</returns>
    private async Task<List<MailCategoryDto>> ReadCategoriesAsync(string playerId, CancellationToken cancellationToken)
    {
        var counts = await _dbContext.MailBoxes.AsNoTracking()
            .Where(box => box.PlayerId == playerId && box.FolderCode != MailFolders.Trash)
            .Join(
                _dbContext.MailMessages.AsNoTracking(),
                box => box.MessageId,
                message => message.Id,
                (box, message) => new { message.CategoryId })
            .GroupBy(row => row.CategoryId)
            .Select(group => new { CategoryId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<long, int> byId = counts.ToDictionary(row => row.CategoryId, row => row.Count);

        List<MailCategoryEntity> categories = await _dbContext.MailCategories.AsNoTracking()
            .Where(category => category.IsVisible && !category.IsArchived)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. categories.Select(category => new MailCategoryDto(
                category.Id,
                category.Code,
                category.NameKey,
                category.ImageKey,
                byId.GetValueOrDefault(category.Id))),
        ];
    }

    /// <summary>
    /// Turns a letter of the box into the answer of the list.
    /// </summary>
    /// <param name="box">The box of the letter.</param>
    /// <param name="message">The letter.</param>
    /// <param name="categories">The categories of the letters.</param>
    /// <param name="nicknames">The nicknames of the senders.</param>
    /// <param name="races">The races of the senders.</param>
    /// <returns>The letter as the list shows it.</returns>
    private static MailLetterDto ToLetter(
        MailBoxEntity box,
        MailMessageEntity message,
        IReadOnlyDictionary<long, MailCategoryEntity> categories,
        IReadOnlyDictionary<string, string> nicknames,
        IReadOnlyDictionary<string, PlayerRaceBrief> races)
    {
        ArgumentNullException.ThrowIfNull(box);
        ArgumentNullException.ThrowIfNull(message);

        string? senderId = message.SenderPlayerId;
        races.TryGetValue(senderId ?? string.Empty, out PlayerRaceBrief? race);
        nicknames.TryGetValue(senderId ?? string.Empty, out string? nickname);

        return new MailLetterDto(
            message.Id,
            box.FolderCode,
            message.CategoryId,
            categories.TryGetValue(message.CategoryId, out MailCategoryEntity? category) ? category.NameKey : string.Empty,
            senderId,
            nickname,
            race?.ColorKey,
            message.Subject,
            Preview(message),
            message.KindCode,
            message.SystemKindCode,
            box.IsRead,
            box.IsSaved,
            box.IsDeleted,
            false,
            message.CreatedAt);
    }

    /// <summary>
    /// The first words of a letter, for the list.
    /// </summary>
    /// <param name="message">The letter.</param>
    /// <returns>The preview of the letter.</returns>
    private static string Preview(MailMessageEntity message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.Body.Length == 0)
        {
            return string.Empty;
        }

        string[] words = message.Body.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return string.Join(' ', words.Take(PreviewWords));
    }
}
