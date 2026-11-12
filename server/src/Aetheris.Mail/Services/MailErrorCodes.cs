namespace Aetheris.Mail.Services;

/// <summary>
/// The reasons the mail refuses a request (task 00060).
/// </summary>
/// <remarks>
/// The codes are the words of the error of the API, and the client translates them through the
/// texts of the game (documentation/tech/04-api.md, section 8).
/// </remarks>
public static class MailErrorCodes
{
    /// <summary>The player who is to receive the letter is unknown or writes no letter.</summary>
    public const string ReceiverUnknown = "mail_receiver_unknown";

    /// <summary>The player writes to himself.</summary>
    public const string SelfNotAllowed = "mail_self_not_allowed";

    /// <summary>The text of the letter is empty or too long.</summary>
    public const string BodyInvalid = "mail_body_invalid";

    /// <summary>The subject of the letter is too long.</summary>
    public const string SubjectInvalid = "mail_subject_invalid";

    /// <summary>The text of the letter carries a link.</summary>
    public const string LinkNotAllowed = "mail_link_not_allowed";

    /// <summary>The player wrote too many letters in one hour.</summary>
    public const string SendLimitReached = "mail_send_limit_reached";

    /// <summary>The category of the letter is unknown or archived.</summary>
    public const string CategoryUnknown = "mail_category_unknown";

    /// <summary>The letter is not in the mail of the player.</summary>
    public const string LetterUnknown = "mail_letter_unknown";

    /// <summary>The player has as many drafts as the mail allows.</summary>
    public const string DraftLimitReached = "mail_draft_limit_reached";

    /// <summary>The draft is not in the mail of the player.</summary>
    public const string DraftUnknown = "mail_draft_unknown";

    /// <summary>The player wrote too many complaints about one letter.</summary>
    public const string ComplaintLimitReached = "mail_complaint_limit_reached";

    /// <summary>The player may not complain about a letter of the game.</summary>
    public const string ComplaintNotAllowed = "mail_complaint_not_allowed";
}
