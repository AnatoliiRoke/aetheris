using Aetheris.Core.Configuration;

namespace Aetheris.Mail.Configuration;

/// <summary>
/// The parameters of the mail (task 00060).
/// </summary>
/// <remarks>
/// The limits of the mail are numbers of the game, so they live in the configuration and not in
/// the code (documentation/tech/09, section 2).
/// </remarks>
public static class MailConfigKeys
{
    /// <summary>
    /// The largest number of the characters of the text of a letter of a player (task 00060).
    /// </summary>
    public const string LetterBodyMaxLength = "mail.letter.body_max_length";

    /// <summary>
    /// The largest number of the characters of the subject of a letter of a player (task 00060).
    /// </summary>
    public const string LetterSubjectMaxLength = "mail.letter.subject_max_length";

    /// <summary>
    /// How many letters a player may write in one hour (task 00060): the guard against spam.
    /// </summary>
    public const string SendLimitPerHour = "mail.letter.send_limit_per_hour";

    /// <summary>
    /// How many days a letter stays in the bin before it is removed for good (task 00060).
    /// </summary>
    public const string TrashDays = "mail.trash.days";

    /// <summary>
    /// How many drafts a player may keep (task 00060).
    /// </summary>
    public const string DraftLimit = "mail.draft.limit";

    /// <summary>
    /// How many complaints about one letter a player may write (task 00060).
    /// </summary>
    public const string ComplaintLimit = "mail.complaint.limit";

    /// <summary>
    /// Gets the definitions of the parameters with their starting values and bounds.
    /// </summary>
    public static IReadOnlyList<ConfigParameterDefinition> Definitions { get; } =
    [
        new(LetterBodyMaxLength, "2000", "1", "4000", "Найбільша кількість символів у тексті листа гравця."),
        new(LetterSubjectMaxLength, "200", "1", "200", "Найбільша кількість символів у темі листа гравця."),
        new(SendLimitPerHour, "30", "1", "1000", "Скільки листів гравець може надіслати за годину."),
        new(TrashDays, "7", "1", "365", "Скільки днів лист лежить у кошику, перш ніж зникне назавжди."),
        new(DraftLimit, "20", "1", "200", "Скільки чернеток листів може мати гравець."),
        new(ComplaintLimit, "3", "1", "20", "Скільки скарг на один лист може написати гравець."),
    ];
}
