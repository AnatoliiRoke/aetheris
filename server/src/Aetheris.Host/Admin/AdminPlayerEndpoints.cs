using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Host.Auth;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Host.Security;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Admin;

/// <summary>
/// A new e-mail address of a player, set by the support.
/// </summary>
/// <param name="Email">The new address.</param>
public sealed record AdminEmailChangeRequest(string? Email);

/// <summary>Validates <see cref="AdminEmailChangeRequest"/>.</summary>
public sealed class AdminEmailChangeRequestValidator : AbstractValidator<AdminEmailChangeRequest>
{
    /// <summary>Initializes a new instance of the <see cref="AdminEmailChangeRequestValidator"/> class.</summary>
    /// <param name="settings">The security settings.</param>
    public AdminEmailChangeRequestValidator(SecuritySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        RuleFor(request => request.Email).AccountEmail(settings);
    }
}

/// <summary>
/// The support actions on the accounts of the players (task 00006, decisions 8 and 10).
/// </summary>
/// <remarks>
/// A player never changes the own address; the support does, and the change is written to
/// <c>admin_audit_log</c>. The journal of the registrations and sign-ins with their IP
/// addresses is read here only.
/// </remarks>
public static class AdminPlayerEndpoints
{
    /// <summary>The error code of an address that another account has.</summary>
    public const string EmailTaken = "email_taken";

    /// <summary>
    /// Maps the support actions on the secured admin group.
    /// </summary>
    /// <param name="secured">The secured admin group.</param>
    /// <returns>The same group.</returns>
    public static RouteGroupBuilder MapPlayerAdminEndpoints(this RouteGroupBuilder secured)
    {
        ArgumentNullException.ThrowIfNull(secured);

        secured.MapPut("/players/{userId}/email", ChangeEmailAsync)
            .WithName("admin.player_email_changed")
            .RequireAuthorization(SecurityPolicies.AdminSupport);

        secured.MapGet("/players/{userId}/access-log", ReadAccessLogAsync)
            .WithName("admin.player_access_log_read")
            .RequireAuthorization(SecurityPolicies.AdminSupport);

        return secured;
    }

    private static async Task<IResult> ChangeEmailAsync(
        string userId,
        AdminEmailChangeRequest request,
        UserManager<IdentityUser> userManager,
        AdminAuditScope audit)
    {
        IdentityUser? user = await userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return ApiErrors.Result(StatusCodes.Status404NotFound, GameErrorCodes.NotFound);
        }

        string email = request.Email!.Trim();
        IdentityUser? holder = await userManager.FindByEmailAsync(email);

        if (holder is not null && !string.Equals(holder.Id, user.Id, StringComparison.Ordinal))
        {
            return ApiErrors.Validation(new Dictionary<string, string[]>
            {
                [nameof(AdminEmailChangeRequest.Email)] = [EmailTaken],
            });
        }

        string? oldEmail = user.Email;

        // The support has checked the owner, so the new address counts as confirmed. The change
        // is only staged: the audit filter saves it together with its record. A new security
        // stamp ends the sessions that were opened with the old address.
        user.Email = email;
        user.NormalizedEmail = userManager.NormalizeEmail(email);
        user.EmailConfirmed = true;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.ConcurrencyStamp = Guid.NewGuid().ToString();

        // The audit keeps the change without the full addresses (task 00002, section 2.7).
        audit.Record(user.Id, Mask(oldEmail), Mask(email));

        return Results.Ok(new { user_id = user.Id, email_confirmed = true });
    }

    private static async Task<IResult> ReadAccessLogAsync(
        string userId,
        int? limit,
        AetherisDbContext dbContext,
        SecuritySettings settings,
        AdminAuditScope audit)
    {
        int pageSize = Math.Clamp(limit ?? settings.AdminAuditPageSize, 1, settings.AdminAuditPageSize);

        List<AccountAccessLogEntity> entries = await dbContext.AccountAccessLog
            .AsNoTracking()
            .Where(entry => entry.UserId == userId)
            .OrderByDescending(entry => entry.Id)
            .Take(pageSize)
            .ToListAsync();

        audit.Record(userId, null, null);

        return Results.Ok(entries.Select(entry => new
        {
            id = entry.Id,
            kind = entry.Kind,
            ip_address = entry.IpAddress,
            at = entry.At,
        }));
    }

    /// <summary>
    /// Keeps the first character of the local part and the domain: <c>j***@example.com</c>.
    /// </summary>
    /// <param name="email">The address.</param>
    /// <returns>The masked address.</returns>
    private static string? Mask(string? email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return email;
        }

        int at = email.IndexOf('@', StringComparison.Ordinal);

        return at <= 0 ? "***" : $"{email[0]}***{email[at..]}";
    }
}
