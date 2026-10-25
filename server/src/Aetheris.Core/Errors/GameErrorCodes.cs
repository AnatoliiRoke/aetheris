namespace Aetheris.Core.Errors;

/// <summary>
/// Stable error codes sent to the client.
/// </summary>
/// <remarks>
/// The code is the localisation key: the client translates it through the translation
/// files (documentation/ui-art/04-lokalizatsiia.md). Codes therefore never change once
/// published, only new ones are added.
/// The core only knows the codes of its own mechanisms. A feature adds its own codes next
/// to its own code when it arrives.
/// </remarks>
public static class GameErrorCodes
{
    /// <summary>
    /// The player does not own the object the command targets.
    /// </summary>
    public const string NotYourObject = "not_your_object";

    /// <summary>
    /// The requested object or route does not exist.
    /// </summary>
    public const string NotFound = "not_found";

    /// <summary>
    /// The account is not authenticated.
    /// </summary>
    public const string NotAuthenticated = "not_authenticated";

    /// <summary>
    /// The account lacks the permission required for the action.
    /// </summary>
    public const string AccessDenied = "access_denied";

    /// <summary>
    /// The e-mail or the password is wrong. The code does not say which one, so that it
    /// cannot be used to find out whether an account exists.
    /// </summary>
    public const string InvalidCredentials = "invalid_credentials";

    /// <summary>
    /// The account is temporarily locked after a series of failed sign-in attempts.
    /// </summary>
    public const string AccountLocked = "account_locked";

    /// <summary>
    /// The e-mail address of the account has not been confirmed yet.
    /// </summary>
    public const string EmailNotConfirmed = "email_not_confirmed";

    /// <summary>
    /// The one-time code of the second factor is missing or wrong.
    /// </summary>
    public const string SecondFactorRequired = "second_factor_required";

    /// <summary>
    /// The confirmation, reset or refresh token is not valid.
    /// </summary>
    public const string InvalidToken = "invalid_token";

    /// <summary>
    /// The command payload did not pass validation.
    /// </summary>
    public const string ValidationFailed = "validation_failed";

    /// <summary>
    /// The request body is larger than the configured limit.
    /// </summary>
    public const string PayloadTooLarge = "payload_too_large";

    /// <summary>
    /// The request was rejected as a duplicate of an earlier one.
    /// </summary>
    public const string DuplicateRequest = "duplicate_request";

    /// <summary>
    /// The request lacks a valid anti-forgery token.
    /// </summary>
    public const string CsrfTokenInvalid = "csrf_token_invalid";

    /// <summary>
    /// Too many requests in the current window.
    /// </summary>
    public const string RateLimited = "rate_limited";

    /// <summary>
    /// The server failed to handle the request.
    /// </summary>
    public const string InternalError = "internal_error";
}
