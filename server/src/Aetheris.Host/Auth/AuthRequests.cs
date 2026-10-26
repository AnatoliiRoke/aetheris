using System.Text.Json.Serialization;
using Aetheris.Host.Security;
using FluentValidation;

namespace Aetheris.Host.Auth;

/// <summary>
/// The registration of a new account (task 00006, decision 1).
/// </summary>
/// <param name="Email">The e-mail address.</param>
/// <param name="Nickname">The nickname.</param>
/// <param name="Password">The password.</param>
/// <param name="PasswordConfirmation">The password, repeated.</param>
/// <param name="CaptchaToken">The answer of the captcha, when the server asked for it.</param>
public sealed record RegisterRequest(
    string? Email,
    string? Nickname,
    string? Password,
    [property: JsonPropertyName("password_confirmation")] string? PasswordConfirmation,
    [property: JsonPropertyName("captcha_token")] string? CaptchaToken = null);

/// <summary>
/// The sign-in of a player (task 00006, decision 4).
/// </summary>
/// <param name="Login">The e-mail address or the nickname.</param>
/// <param name="Password">The password.</param>
/// <param name="RememberMe">Whether the session outlives the browser.</param>
public sealed record LoginRequest(
    string? Login,
    string? Password,
    [property: JsonPropertyName("remember_me")] bool RememberMe = false);

/// <summary>
/// The confirmation of an e-mail address.
/// </summary>
/// <param name="UserId">The account.</param>
/// <param name="Token">The token from the confirmation e-mail.</param>
public sealed record ConfirmEmailRequest(string? UserId, string? Token);

/// <summary>
/// The request of a password reset.
/// </summary>
/// <param name="Email">The e-mail address.</param>
public sealed record ForgotPasswordRequest(string? Email);

/// <summary>
/// The request of a new confirmation e-mail.
/// </summary>
/// <param name="Email">The e-mail address.</param>
public sealed record ResendConfirmationRequest(string? Email);

/// <summary>
/// The reset of a password.
/// </summary>
/// <param name="UserId">The account.</param>
/// <param name="Token">The token from the reset e-mail.</param>
/// <param name="NewPassword">The new password.</param>
public sealed record ResetPasswordRequest(string? UserId, string? Token, string? NewPassword);

/// <summary>
/// The limits shared by the validators of the account requests.
/// </summary>
internal static class AccountRules
{
    /// <summary>
    /// The length of the key column of the Identity tables. It is part of the schema, not
    /// a tunable limit.
    /// </summary>
    public const int UserIdMaxLength = 450;

    /// <summary>
    /// Adds the rules of an e-mail address.
    /// </summary>
    /// <typeparam name="T">The request type.</typeparam>
    /// <param name="rule">The rule builder.</param>
    /// <param name="settings">The security settings.</param>
    /// <returns>The same builder.</returns>
    public static IRuleBuilderOptions<T, string?> AccountEmail<T>(this IRuleBuilder<T, string?> rule, SecuritySettings settings) =>
        rule.NotEmpty().MaximumLength(settings.EmailMaxLength).EmailAddress();

    /// <summary>
    /// Adds the rules of a password field. The strength rules are checked by
    /// <see cref="ConfigPasswordValidator"/>; this only rejects empty and oversized values early.
    /// </summary>
    /// <typeparam name="T">The request type.</typeparam>
    /// <param name="rule">The rule builder.</param>
    /// <param name="settings">The security settings.</param>
    /// <returns>The same builder.</returns>
    public static IRuleBuilderOptions<T, string?> AccountPassword<T>(this IRuleBuilder<T, string?> rule, SecuritySettings settings) =>
        rule.NotEmpty().MaximumLength(settings.PasswordMaxLength);

    /// <summary>
    /// Adds the rules of a confirmation or reset token.
    /// </summary>
    /// <typeparam name="T">The request type.</typeparam>
    /// <param name="rule">The rule builder.</param>
    /// <param name="settings">The security settings.</param>
    /// <returns>The same builder.</returns>
    public static IRuleBuilderOptions<T, string?> AccountToken<T>(this IRuleBuilder<T, string?> rule, SecuritySettings settings) =>
        rule.NotEmpty().MaximumLength(settings.TokenMaxLength);

    /// <summary>
    /// Adds the rules of an account identifier.
    /// </summary>
    /// <typeparam name="T">The request type.</typeparam>
    /// <param name="rule">The rule builder.</param>
    /// <returns>The same builder.</returns>
    public static IRuleBuilderOptions<T, string?> AccountId<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().MaximumLength(UserIdMaxLength);
}

/// <summary>Validates <see cref="RegisterRequest"/>.</summary>
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    /// <summary>Initializes a new instance of the <see cref="RegisterRequestValidator"/> class.</summary>
    /// <param name="settings">The security settings.</param>
    public RegisterRequestValidator(SecuritySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        RuleFor(request => request.Email).AccountEmail(settings);
        RuleFor(request => request.Nickname).NotEmpty();
        RuleFor(request => request.Password).AccountPassword(settings);
        RuleFor(request => request.PasswordConfirmation).NotEmpty();
        RuleFor(request => request.CaptchaToken).MaximumLength(settings.TokenMaxLength);
    }
}

/// <summary>Validates <see cref="LoginRequest"/>.</summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    /// <summary>Initializes a new instance of the <see cref="LoginRequestValidator"/> class.</summary>
    /// <param name="settings">The security settings.</param>
    public LoginRequestValidator(SecuritySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        RuleFor(request => request.Login).NotEmpty().MaximumLength(settings.EmailMaxLength);
        RuleFor(request => request.Password).AccountPassword(settings);
    }
}

/// <summary>Validates <see cref="ConfirmEmailRequest"/>.</summary>
public sealed class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    /// <summary>Initializes a new instance of the <see cref="ConfirmEmailRequestValidator"/> class.</summary>
    /// <param name="settings">The security settings.</param>
    public ConfirmEmailRequestValidator(SecuritySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        RuleFor(request => request.UserId).AccountId();
        RuleFor(request => request.Token).AccountToken(settings);
    }
}

/// <summary>Validates <see cref="ForgotPasswordRequest"/>.</summary>
public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    /// <summary>Initializes a new instance of the <see cref="ForgotPasswordRequestValidator"/> class.</summary>
    /// <param name="settings">The security settings.</param>
    public ForgotPasswordRequestValidator(SecuritySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        RuleFor(request => request.Email).AccountEmail(settings);
    }
}

/// <summary>Validates <see cref="ResendConfirmationRequest"/>.</summary>
public sealed class ResendConfirmationRequestValidator : AbstractValidator<ResendConfirmationRequest>
{
    /// <summary>Initializes a new instance of the <see cref="ResendConfirmationRequestValidator"/> class.</summary>
    /// <param name="settings">The security settings.</param>
    public ResendConfirmationRequestValidator(SecuritySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        RuleFor(request => request.Email).AccountEmail(settings);
    }
}

/// <summary>Validates <see cref="ResetPasswordRequest"/>.</summary>
public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    /// <summary>Initializes a new instance of the <see cref="ResetPasswordRequestValidator"/> class.</summary>
    /// <param name="settings">The security settings.</param>
    public ResetPasswordRequestValidator(SecuritySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        RuleFor(request => request.UserId).AccountId();
        RuleFor(request => request.Token).AccountToken(settings);
        RuleFor(request => request.NewPassword).AccountPassword(settings);
    }
}
