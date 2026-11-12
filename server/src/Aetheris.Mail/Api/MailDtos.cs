namespace Aetheris.Mail.Api;

/// <summary>
/// The mail of a player (task 00060): the folders, the categories, the list of the letters and the
/// counters.
/// </summary>
/// <param name="Folders">The folders of the mail with the number of the unread letters in each.</param>
/// <param name="Categories">The categories of the letters with the number of the letters in each.</param>
/// <param name="Letters">The letters of the list, the newest first.</param>
/// <param name="UnreadCount">How many letters of the folder «incoming» are not read.</param>
/// <param name="TotalCount">How many letters the list holds.</param>
/// <param name="GameNow">The game time of the answer, so the client counts the time left of a letter.</param>
public sealed record MailViewDto(
    IReadOnlyList<MailFolderDto> Folders,
    IReadOnlyList<MailCategoryDto> Categories,
    IReadOnlyList<MailLetterDto> Letters,
    int UnreadCount,
    int TotalCount,
    DateTimeOffset GameNow = default);

/// <summary>
/// A folder of the mail with the number of the unread letters in it (task 00060).
/// </summary>
/// <param name="Code">The code of the folder, one of the folders of the game.</param>
/// <param name="UnreadCount">How many letters of the folder are not read.</param>
/// <param name="TotalCount">How many letters the folder holds.</param>
public sealed record MailFolderDto(string Code, int UnreadCount, int TotalCount);

/// <summary>
/// A category of the letters (task 00060).
/// </summary>
/// <param name="Id">The category.</param>
/// <param name="Code">The code of the category.</param>
/// <param name="NameKey">The key of the name of the category in the texts of the game.</param>
/// <param name="ImageKey">The name of the icon of the category, or <see langword="null"/>.</param>
/// <param name="Count">How many letters of the category the list holds.</param>
public sealed record MailCategoryDto(long Id, string Code, string NameKey, string? ImageKey, int Count);

/// <summary>
/// A letter of the mail as the list shows it (task 00060).
/// </summary>
/// <param name="Id">The letter.</param>
/// <param name="FolderCode">The folder the letter lies in for the player.</param>
/// <param name="CategoryId">The category of the letter.</param>
/// <param name="CategoryNameKey">The key of the name of the category in the texts of the game.</param>
/// <param name="SenderPlayerId">The player who wrote the letter, or <see langword="null"/> when the game wrote it.</param>
/// <param name="SenderNickname">The nickname of the sender, or <see langword="null"/> when the game wrote it.</param>
/// <param name="SenderRaceColorKey">The colour of the race of the sender, or <see langword="null"/>.</param>
/// <param name="Subject">The subject of the letter, or an empty text for a letter of the game.</param>
/// <param name="Preview">The first words of the letter.</param>
/// <param name="KindCode">The kind of the letter: a letter of a player, a letter of the game or a report.</param>
/// <param name="SystemKindCode">The kind of a letter of the game, or <see langword="null"/>.</param>
/// <param name="IsRead">Whether the player has read the letter.</param>
/// <param name="IsSaved">Whether the player has saved the letter.</param>
/// <param name="IsDeleted">Whether the letter lies in the bin.</param>
/// <param name="HasAttachment">Whether the letter carries an attachment.</param>
/// <param name="CreatedAt">The moment of game time the letter was written at.</param>
/// <param name="GroupCount">How many letters of the same kind of the game the letter stands for, when
/// the list groups them.</param>
public sealed record MailLetterDto(
    long Id,
    string FolderCode,
    long CategoryId,
    string CategoryNameKey,
    string? SenderPlayerId,
    string? SenderNickname,
    string? SenderRaceColorKey,
    string Subject,
    string Preview,
    string KindCode,
    string? SystemKindCode,
    bool IsRead,
    bool IsSaved,
    bool IsDeleted,
    bool HasAttachment,
    DateTimeOffset CreatedAt,
    int GroupCount = 1);

/// <summary>
/// One letter of the mail with its whole text (task 00060).
/// </summary>
/// <param name="Letter">The letter as the list shows it.</param>
/// <param name="Body">The text of the letter of a player, or an empty text for a letter of the game.</param>
/// <param name="TextKey">The key of the text of a letter of the game, or <see langword="null"/>.</param>
/// <param name="TextParameters">The parameters of the text of a letter of the game as a document, or <see langword="null"/>.</param>
public sealed record MailLetterDetailDto(
    MailLetterDto Letter,
    string Body,
    string? TextKey,
    string? TextParameters);

/// <summary>
/// The drafts of a player (task 00060).
/// </summary>
/// <param name="Drafts">The drafts, the newest first.</param>
public sealed record MailDraftsDto(IReadOnlyList<MailDraftDto> Drafts);

/// <summary>
/// A draft of a letter (task 00060).
/// </summary>
/// <param name="Id">The draft.</param>
/// <param name="ReceiverPlayerId">The receiver named by the draft, or <see langword="null"/>.</param>
/// <param name="Subject">The subject of the draft.</param>
/// <param name="Body">The text of the draft.</param>
/// <param name="UpdatedAt">The moment of game time the draft was changed at last.</param>
public sealed record MailDraftDto(
    long Id,
    string? ReceiverPlayerId,
    string Subject,
    string Body,
    DateTimeOffset UpdatedAt);

/// <summary>
/// The players a letter may be written to (task 00060).
/// </summary>
/// <param name="Players">The players, with their nickname, their race and whether the player has
/// already written to them.</param>
public sealed record MailRecipientsDto(IReadOnlyList<MailRecipientDto> Players);

/// <summary>
/// One player a letter may be written to (task 00060).
/// </summary>
/// <param name="PlayerId">The player.</param>
/// <param name="Nickname">The nickname of the player.</param>
/// <param name="RaceColorKey">The colour of the race of the player, or <see langword="null"/>.</param>
/// <param name="RaceNameKey">The key of the name of the race in the texts of the game.</param>
/// <param name="HasWritten">Whether the player has already written to them.</param>
public sealed record MailRecipientDto(
    string PlayerId,
    string Nickname,
    string? RaceColorKey,
    string RaceNameKey,
    bool HasWritten);

/// <summary>
/// The state of the mail of a player (task 00060): how many letters are not read.
/// </summary>
/// <param name="UnreadCount">How many letters of the folder «incoming» are not read.</param>
public sealed record MailUnreadDto(int UnreadCount);
