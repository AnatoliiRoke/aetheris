using System.Text.RegularExpressions;
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
/// The letters that players write to each other, and the letters the game writes to a player
/// (task 00060).
/// </summary>
/// <remarks>
/// <para>
/// A letter of a player is checked here: the receiver must exist, the player may not write to
/// himself, the text must fit the limits of the settings and must carry no link, and the player
/// may not write more letters in one hour than the settings allow (task 00060, section 3.1,
/// point 3).
/// </para>
/// <para>
/// The limits are read from the configuration, so the balance of the mail is changed without a
/// change of the code (documentation/tech/09, section 2).
/// </para>
/// </remarks>
public sealed partial class MailSendService
{
    /// <summary>
    /// The most players the field «to» offers at once.
    /// </summary>
    private const int RecipientLimit = 20;

    /// <summary>
    /// The most players the search for a receiver reads at once.
    /// </summary>
    private const int ReceiverSearchLimit = 2;

    /// <summary>
    /// The default of the limit of the characters of the text of a letter, when the settings do not
    /// name it yet.
    /// </summary>
    private const long DefaultBodyMaxLength = 2000;

    /// <summary>
    /// The default of the limit of the characters of the subject of a letter, when the settings do
    /// not name it yet.
    /// </summary>
    private const long DefaultSubjectMaxLength = 200;

    /// <summary>
    /// The default of the number of the letters a player writes in one hour, when the settings do
    /// not name it yet.
    /// </summary>
    private const long DefaultSendLimitPerHour = 30;

    /// <summary>
    /// The default of the number of the drafts of a player, when the settings do not name it yet.
    /// </summary>
    private const long DefaultDraftLimit = 20;

    /// <summary>
    /// The default of the number of the complaints about one letter, when the settings do not name
    /// it yet.
    /// </summary>
    private const long DefaultComplaintLimit = 3;

    private readonly MailDbContext _dbContext;
    private readonly IGameConfig _config;
    private readonly IGameClock _clock;
    private readonly IPlayerDirectory _players;
    private readonly IWorldModule _world;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailSendService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="config">The configuration of the game.</param>
    /// <param name="clock">The clock of the game.</param>
    /// <param name="players">The nicknames of the players.</param>
    /// <param name="world">The world.</param>
    public MailSendService(
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
    /// The players a letter may be written to (task 00060): the ones whose nickname begins with
    /// the text, with their race and whether the player has already written to them.
    /// </summary>
    /// <param name="playerId">The player who writes.</param>
    /// <param name="text">The text of the nickname.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The players to offer.</returns>
    public async Task<MailRecipientsDto> ReadRecipientsAsync(
        string playerId,
        string? text,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        if (string.IsNullOrWhiteSpace(text))
        {
            return new MailRecipientsDto([]);
        }

        IReadOnlyList<PlayerBrief> found = await _players
            .FindByNicknameAsync(text, RecipientLimit, cancellationToken)
            .ConfigureAwait(false);

        string[] ids = [.. found.Select(player => player.PlayerId)];
        IReadOnlyDictionary<string, PlayerRaceBrief> races = await _world
            .GetRaceBriefsAsync(ids, cancellationToken)
            .ConfigureAwait(false);

        // The players the letter was already written to come in one read of the boxes.
        var written = await _dbContext.MailBoxes.AsNoTracking()
            .Where(box => box.PlayerId == playerId && box.FolderCode == MailFolders.Sent)
            .Join(
                _dbContext.MailMessages.AsNoTracking(),
                box => box.MessageId,
                message => message.Id,
                (box, message) => message.ReceiverPlayerId)
            .Where(receiver => receiver != null)
            .Select(receiver => receiver!)
            .Where(receiver => ids.Contains(receiver))
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        HashSet<string> known = [.. written];

        return new MailRecipientsDto(
        [
            .. found.Select(player => new MailRecipientDto(
                player.PlayerId,
                player.Nickname,
                races.TryGetValue(player.PlayerId, out PlayerRaceBrief? race) ? race.ColorKey : null,
                races.TryGetValue(player.PlayerId, out PlayerRaceBrief? named) ? named.NameKey : string.Empty,
                known.Contains(player.PlayerId))),
        ]);
    }

    /// <summary>
    /// Writes a letter of a player to another player (task 00060).
    /// </summary>
    /// <param name="playerId">The player who writes.</param>
    /// <param name="receiverNickname">The nickname of the player who receives.</param>
    /// <param name="subject">The subject of the letter.</param>
    /// <param name="body">The text of the letter.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The letter that was written.</returns>
    /// <exception cref="GameException">The letter may not be written.</exception>
    public async Task<long> SendPlayerLetterAsync(
        string playerId,
        string receiverNickname,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);
        ArgumentException.ThrowIfNullOrEmpty(receiverNickname);

        int bodyLimit = (int)_config.GetInt64(MailConfigKeys.LetterBodyMaxLength, DefaultBodyMaxLength);
        int subjectLimit = (int)_config.GetInt64(MailConfigKeys.LetterSubjectMaxLength, DefaultSubjectMaxLength);

        if (string.IsNullOrWhiteSpace(body) || body.Length > bodyLimit)
        {
            throw new GameException(MailErrorCodes.BodyInvalid, "The text of the letter does not fit the limits.");
        }

        if (subject.Length > subjectLimit)
        {
            throw new GameException(MailErrorCodes.SubjectInvalid, "The subject of the letter is too long.");
        }

        if (LinkPattern().IsMatch(body))
        {
            throw new GameException(MailErrorCodes.LinkNotAllowed, "The text of the letter carries a link.");
        }

        IReadOnlyList<PlayerBrief> receivers = await _players
            .FindByNicknameAsync(receiverNickname, ReceiverSearchLimit, cancellationToken)
            .ConfigureAwait(false);

        PlayerBrief? receiver = receivers.FirstOrDefault(player =>
            string.Equals(player.Nickname, receiverNickname.Trim(), StringComparison.OrdinalIgnoreCase));

        if (receiver is null)
        {
            throw new GameException(MailErrorCodes.ReceiverUnknown, "The player who is to receive the letter is unknown.");
        }

        if (receiver.PlayerId == playerId)
        {
            throw new GameException(MailErrorCodes.SelfNotAllowed, "The player may not write to himself.");
        }

        await CheckSendLimitAsync(playerId, cancellationToken).ConfigureAwait(false);

        MailCategoryEntity category = await ReadPlayerCategoryAsync(cancellationToken).ConfigureAwait(false);

        DateTimeOffset now = _clock.GameNow;
        MailMessageEntity message = new()
        {
            SenderPlayerId = playerId,
            ReceiverPlayerId = receiver.PlayerId,
            CategoryId = category.Id,
            Subject = subject.Trim(),
            Body = body.Trim(),
            KindCode = MailMessageKinds.Player,
            CreatedAt = now,
        };

        _dbContext.MailMessages.Add(message);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // The letter lies in two boxes: the box of the sender in the folder «sent», and the box of
        // the receiver in the folder «incoming». The receiver has not read it yet.
        _dbContext.MailBoxes.Add(new MailBoxEntity
        {
            PlayerId = playerId,
            MessageId = message.Id,
            FolderCode = MailFolders.Sent,
            IsRead = true,
            CreatedAt = now,
        });
        _dbContext.MailBoxes.Add(new MailBoxEntity
        {
            PlayerId = receiver.PlayerId,
            MessageId = message.Id,
            FolderCode = MailFolders.Incoming,
            IsRead = false,
            CreatedAt = now,
        });

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return message.Id;
    }

    /// <summary>
    /// Writes a letter of the game to a player (task 00060).
    /// </summary>
    /// <param name="playerId">The player the letter is written to.</param>
    /// <param name="categoryCode">The code of the category of the letter.</param>
    /// <param name="textKey">The key of the text of the letter.</param>
    /// <param name="parameters">The parameters of the text as a document.</param>
    /// <param name="systemKindCode">The kind of the letter, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The identifier of the letter, or <see langword="null"/> when the category is unknown.</returns>
    public async Task<long?> SendSystemLetterAsync(
        string playerId,
        string categoryCode,
        string textKey,
        string? parameters,
        string? systemKindCode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);
        ArgumentException.ThrowIfNullOrEmpty(categoryCode);
        ArgumentException.ThrowIfNullOrEmpty(textKey);

        MailCategoryEntity? category = await _dbContext.MailCategories.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.Code == categoryCode && !entry.IsArchived, cancellationToken)
            .ConfigureAwait(false);

        if (category is null)
        {
            return null;
        }

        DateTimeOffset now = _clock.GameNow;
        MailMessageEntity message = new()
        {
            SenderPlayerId = null,
            ReceiverPlayerId = playerId,
            CategoryId = category.Id,
            Subject = string.Empty,
            Body = string.Empty,
            TextKey = textKey,
            TextParameters = parameters,
            KindCode = systemKindCode is null ? MailMessageKinds.System : MailMessageKinds.Report,
            SystemKindCode = systemKindCode,
            CreatedAt = now,
        };

        _dbContext.MailMessages.Add(message);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _dbContext.MailBoxes.Add(new MailBoxEntity
        {
            PlayerId = playerId,
            MessageId = message.Id,
            FolderCode = MailFolders.Incoming,
            IsRead = false,
            CreatedAt = now,
        });

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return message.Id;
    }

    /// <summary>
    /// Writes a draft of a letter, or changes the one that is named (task 00060).
    /// </summary>
    /// <param name="playerId">The player who writes.</param>
    /// <param name="draftId">The draft to change, or zero for a new draft.</param>
    /// <param name="receiverNickname">The nickname of the receiver, or <see langword="null"/>.</param>
    /// <param name="subject">The subject of the draft.</param>
    /// <param name="body">The text of the draft.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The draft that was written.</returns>
    /// <exception cref="GameException">The draft may not be written.</exception>
    public async Task<MailDraftDto> SaveDraftAsync(
        string playerId,
        long draftId,
        string? receiverNickname,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        int bodyLimit = (int)_config.GetInt64(MailConfigKeys.LetterBodyMaxLength, DefaultBodyMaxLength);
        int subjectLimit = (int)_config.GetInt64(MailConfigKeys.LetterSubjectMaxLength, DefaultSubjectMaxLength);

        if (body.Length > bodyLimit)
        {
            throw new GameException(MailErrorCodes.BodyInvalid, "The text of the draft does not fit the limits.");
        }

        if (subject.Length > subjectLimit)
        {
            throw new GameException(MailErrorCodes.SubjectInvalid, "The subject of the draft is too long.");
        }

        string? receiverId = null;

        if (!string.IsNullOrWhiteSpace(receiverNickname))
        {
            IReadOnlyList<PlayerBrief> receivers = await _players
                .FindByNicknameAsync(receiverNickname, ReceiverSearchLimit, cancellationToken)
                .ConfigureAwait(false);

            receiverId = receivers
                .FirstOrDefault(player => string.Equals(player.Nickname, receiverNickname.Trim(), StringComparison.OrdinalIgnoreCase))?
                .PlayerId;
        }

        DateTimeOffset now = _clock.GameNow;
        MailDraftEntity draft;

        if (draftId > 0)
        {
            draft = await _dbContext.MailDrafts
                .SingleOrDefaultAsync(entry => entry.Id == draftId && entry.PlayerId == playerId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new GameException(MailErrorCodes.DraftUnknown, "The draft is not in the mail of the player.");
        }
        else
        {
            int limit = (int)_config.GetInt64(MailConfigKeys.DraftLimit, DefaultDraftLimit);
            int count = await _dbContext.MailDrafts.AsNoTracking()
                .CountAsync(entry => entry.PlayerId == playerId, cancellationToken)
                .ConfigureAwait(false);

            if (count >= limit)
            {
                throw new GameException(MailErrorCodes.DraftLimitReached, "The player has as many drafts as the mail allows.");
            }

            draft = new MailDraftEntity { PlayerId = playerId, CreatedAt = now };
            _dbContext.MailDrafts.Add(draft);
        }

        draft.ReceiverPlayerId = receiverId;
        draft.Subject = subject.Trim();
        draft.Body = body;
        draft.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new MailDraftDto(draft.Id, draft.ReceiverPlayerId, draft.Subject, draft.Body, draft.UpdatedAt);
    }

    /// <summary>
    /// Reads the drafts of a player.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The drafts of the player, the newest first.</returns>
    public async Task<MailDraftsDto> ReadDraftsAsync(string playerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        List<MailDraftEntity> drafts = await _dbContext.MailDrafts.AsNoTracking()
            .Where(draft => draft.PlayerId == playerId)
            .OrderByDescending(draft => draft.UpdatedAt)
            .ThenByDescending(draft => draft.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new MailDraftsDto(
        [
            .. drafts.Select(draft => new MailDraftDto(
                draft.Id,
                draft.ReceiverPlayerId,
                draft.Subject,
                draft.Body,
                draft.UpdatedAt)),
        ]);
    }

    /// <summary>
    /// Removes a draft of a player.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="draftId">The draft.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the draft was removed.</returns>
    public async Task<bool> RemoveDraftAsync(
        string playerId,
        long draftId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        MailDraftEntity? draft = await _dbContext.MailDrafts
            .SingleOrDefaultAsync(entry => entry.Id == draftId && entry.PlayerId == playerId, cancellationToken)
            .ConfigureAwait(false);

        if (draft is null)
        {
            return false;
        }

        _dbContext.MailDrafts.Remove(draft);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Complains about a letter of another player (task 00060).
    /// </summary>
    /// <param name="playerId">The player who complains.</param>
    /// <param name="messageId">The letter.</param>
    /// <param name="reason">The reason of the complaint.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The complaint that was written.</returns>
    /// <exception cref="GameException">The complaint may not be written.</exception>
    public async Task<long> ComplainAsync(
        string playerId,
        long messageId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        MailBoxEntity? box = await _dbContext.MailBoxes.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.PlayerId == playerId && entry.MessageId == messageId, cancellationToken)
            .ConfigureAwait(false);

        if (box is null)
        {
            throw new GameException(MailErrorCodes.LetterUnknown, "The letter is not in the mail of the player.");
        }

        MailMessageEntity message = await _dbContext.MailMessages.AsNoTracking()
            .SingleAsync(entry => entry.Id == messageId, cancellationToken)
            .ConfigureAwait(false);

        if (message.KindCode != MailMessageKinds.Player || message.SenderPlayerId is null)
        {
            throw new GameException(MailErrorCodes.ComplaintNotAllowed, "The player may complain only about a letter of another player.");
        }

        int limit = (int)_config.GetInt64(MailConfigKeys.ComplaintLimit, DefaultComplaintLimit);
        int count = await _dbContext.MailComplaints.AsNoTracking()
            .CountAsync(entry => entry.PlayerId == playerId && entry.MessageId == messageId, cancellationToken)
            .ConfigureAwait(false);

        if (count >= limit)
        {
            throw new GameException(MailErrorCodes.ComplaintLimitReached, "The player wrote too many complaints about this letter.");
        }

        MailComplaintEntity complaint = new()
        {
            PlayerId = playerId,
            MessageId = messageId,
            Reason = reason.Trim(),
            CreatedAt = _clock.GameNow,
        };

        _dbContext.MailComplaints.Add(complaint);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return complaint.Id;
    }

    /// <summary>
    /// Checks that the player has not written too many letters in one hour.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the check is done.</returns>
    /// <exception cref="GameException">The player wrote too many letters in one hour.</exception>
    private async Task CheckSendLimitAsync(string playerId, CancellationToken cancellationToken)
    {
        int limit = (int)_config.GetInt64(MailConfigKeys.SendLimitPerHour, DefaultSendLimitPerHour);
        DateTimeOffset since = _clock.GameNow.AddHours(-1);

        int written = await _dbContext.MailBoxes.AsNoTracking()
            .Where(box => box.PlayerId == playerId && box.FolderCode == MailFolders.Sent && box.CreatedAt >= since)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        if (written >= limit)
        {
            throw new GameException(MailErrorCodes.SendLimitReached, "The player wrote too many letters in one hour.");
        }
    }

    /// <summary>
    /// Reads the category of the letters of the players.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The category of the letters of the players.</returns>
    /// <exception cref="GameException">The category is unknown.</exception>
    private async Task<MailCategoryEntity> ReadPlayerCategoryAsync(CancellationToken cancellationToken)
    {
        MailCategoryEntity? category = await _dbContext.MailCategories.AsNoTracking()
            .Where(entry => entry.Code == MailCategoryCodes.Player && !entry.IsArchived)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return category ?? throw new GameException(MailErrorCodes.CategoryUnknown, "The category of the letters of the players is unknown.");
    }

    /// <summary>
    /// The pattern of a link in the text of a letter: a scheme, or a word that names a site.
    /// </summary>
    [GeneratedRegex(@"(?i)(https?://|www\.|\b[a-z0-9\-]+\.(com|net|org|io|ua|рф)\b)", RegexOptions.CultureInvariant)]
    private static partial Regex LinkPattern();
}

/// <summary>
/// The codes of the categories of the letters (task 00060).
/// </summary>
/// <remarks>
/// The codes are the codes of the rows of the table of the categories, so the game may add a
/// category without a change of the code (documentation/tech/09, section 2).
/// </remarks>
public static class MailCategoryCodes
{
    /// <summary>The letters of the players.</summary>
    public const string Player = "mail_category_1";

    /// <summary>The letters of the game.</summary>
    public const string System = "mail_category_2";

    /// <summary>The reports of the game.</summary>
    public const string Report = "mail_category_3";

    /// <summary>The letters of the market.</summary>
    public const string Market = "mail_category_4";
}
