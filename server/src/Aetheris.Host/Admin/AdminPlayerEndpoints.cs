using Aetheris.Core.Admin;
using Aetheris.Core.Errors;
using Aetheris.Host.Auth;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Host.Security;
using Aetheris.World;
using Aetheris.World.Api;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Admin;

/// <summary>
/// A new e-mail address of a player, set by the support.
/// </summary>
/// <param name="Email">The new address.</param>
public sealed record AdminEmailChangeRequest(string? Email);

/// <summary>One player in the list of the admin panel.</summary>
/// <param name="UserId">The account.</param>
/// <param name="Nickname">The nickname, the Identity user name.</param>
/// <param name="Email">The address, for the administrator who opens the list.</param>
/// <param name="EmailConfirmed">Whether the address is confirmed.</param>
/// <param name="RegisteredAt">The moment of the registration.</param>
/// <param name="RaceNameKey">The localisation key of the race, or <see langword="null"/> before a civilization exists.</param>
/// <param name="PlanetCount">How many bodies the player owns.</param>
public sealed record AdminPlayerRow(
    string UserId,
    string Nickname,
    string? Email,
    bool EmailConfirmed,
    DateTimeOffset RegisteredAt,
    string? RaceNameKey,
    int PlanetCount);

/// <summary>The card of a player. The bodies are labels: the panel does not edit the body itself.</summary>
/// <param name="UserId">The account.</param>
/// <param name="Nickname">The nickname.</param>
/// <param name="Email">The address.</param>
/// <param name="EmailConfirmed">Whether the address is confirmed.</param>
/// <param name="RegisteredAt">The moment of the registration.</param>
/// <param name="Civilization">The civilization, or <see langword="null"/> before the player founds one.</param>
/// <param name="Planets">The bodies the player owns, in the order of their identifiers.</param>
public sealed record AdminPlayerCard(
    string UserId,
    string Nickname,
    string? Email,
    bool EmailConfirmed,
    DateTimeOffset RegisteredAt,
    CivilizationSummary? Civilization,
    IReadOnlyList<OwnedPlanet> Planets);

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
            .WithName("admin.player_email_changed");

        secured.MapGet("/players/{userId}/access-log", ReadAccessLogAsync)
            .WithName("admin.player_access_log_read");

        // One name: the list and the card are the same screen, and an endpoint name is the
        // permission code. Two routes cannot share a name.
        secured.MapGet("/players/{userId?}", ReadPlayersAsync)
            .WithName("admin.players_read");

        return secured;
    }

    private static async Task<IResult> ReadPlayersAsync(
        string? userId,
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "search")] string? search,
        [FromQuery(Name = "sort")] string? sort,
        [FromQuery(Name = "direction")] string? direction,
        AetherisDbContext dbContext,
        IWorldModule world,
        IAdminQueryLimits limits,
        AdminAuditScope audit,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(userId))
        {
            return await ReadPlayerAsync(userId, dbContext, world, audit, cancellationToken);
        }

        return await ListPlayersAsync(page, pageSize, search, sort, direction, dbContext, world, limits, audit, cancellationToken);
    }

    private static async Task<IResult> ListPlayersAsync(
        int? page,
        int? pageSize,
        string? search,
        string? sort,
        string? direction,
        AetherisDbContext dbContext,
        IWorldModule world,
        IAdminQueryLimits limits,
        AdminAuditScope audit,
        CancellationToken cancellationToken)
    {
        PageRequest? request = AdminPaging.Create(page, pageSize, search, sort, direction, limits);

        if (request is null)
        {
            return ApiErrors.Validation(new Dictionary<string, string[]>
            {
                ["search"] = [AdminPaging.SearchTooLong],
            });
        }

        var accounts =
            from profile in dbContext.PlayerProfiles.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on profile.UserId equals user.Id
            select new { profile, user };

        if (request.Search is not null)
        {
            string pattern = AdminPaging.ContainsPattern(request.Search);
            accounts = accounts.Where(account =>
                (account.user.UserName != null && EF.Functions.ILike(account.user.UserName, pattern, AdminPaging.LikeEscape))
                || (account.user.Email != null && EF.Functions.ILike(account.user.Email, pattern, AdminPaging.LikeEscape)));
        }

        int total = await accounts.CountAsync(cancellationToken);

        var pageAccounts = await accounts
            .OrderByDescending(account => account.profile.RegisteredAt)
            .ThenBy(account => account.user.Id)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(account => new
            {
                account.user.Id,
                account.user.UserName,
                account.user.Email,
                account.user.EmailConfirmed,
                account.profile.RegisteredAt,
            })
            .ToListAsync(cancellationToken);

        // The search text may be an address, so the journal keeps only that the list was opened.
        audit.Record("players", null, null);

        if (pageAccounts.Count == 0)
        {
            return AdminJson.Ok(new PageResult<AdminPlayerRow>([], total, request.Page, request.PageSize));
        }

        List<string> playerIds = pageAccounts.ConvertAll(account => account.Id);
        IReadOnlyDictionary<string, int> planetCounts = await world.CountOwnedPlanetsAsync(playerIds, cancellationToken);
        IReadOnlyDictionary<string, PlayerRaceBrief> races = await world.GetRaceBriefsAsync(playerIds, cancellationToken);

        List<AdminPlayerRow> players = pageAccounts.ConvertAll(account => new AdminPlayerRow(
            account.Id,
            account.UserName ?? string.Empty,
            account.Email,
            account.EmailConfirmed,
            account.RegisteredAt,
            races.GetValueOrDefault(account.Id)?.NameKey,
            planetCounts.GetValueOrDefault(account.Id)));

        return AdminJson.Ok(new PageResult<AdminPlayerRow>(players, total, request.Page, request.PageSize));
    }

    private static async Task<IResult> ReadPlayerAsync(
        string userId,
        AetherisDbContext dbContext,
        IWorldModule world,
        AdminAuditScope audit,
        CancellationToken cancellationToken)
    {
        var account = await (
            from profile in dbContext.PlayerProfiles.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on profile.UserId equals user.Id
            where profile.UserId == userId
            select new
            {
                user.Id,
                user.UserName,
                user.Email,
                user.EmailConfirmed,
                profile.RegisteredAt,
            }).SingleOrDefaultAsync(cancellationToken);

        if (account is null)
        {
            return ApiErrors.Result(StatusCodes.Status404NotFound, GameErrorCodes.NotFound);
        }

        // The journal keeps the account, never the address.
        audit.Record(account.Id, null, null);

        CivilizationSummary? civilization = await world.GetCivilizationAsync(account.Id, cancellationToken);
        IReadOnlyList<OwnedPlanet> planets = await world.GetOwnedPlanetsAsync(account.Id, cancellationToken);

        return AdminJson.Ok(new AdminPlayerCard(
            account.Id,
            account.UserName ?? string.Empty,
            account.Email,
            account.EmailConfirmed,
            account.RegisteredAt,
            civilization,
            planets));
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
