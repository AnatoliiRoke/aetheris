using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Admin;

/// <summary>
/// Creates administrator accounts (task 00002, section 2.5; task 00007, decision 4).
/// </summary>
/// <remarks>
/// The authenticator key is always enrolled, so the second factor works as soon as
/// <c>admin.require_totp</c> is switched on. The roles must exist in <c>admin_role</c>.
/// </remarks>
public sealed class AdminAccountService
{
    /// <summary>
    /// The actor name of the audit records written by the command line, which has no
    /// signed-in administrator.
    /// </summary>
    public const string SystemActor = "system";

    /// <summary>
    /// The action of the audit record of a new administrator account.
    /// </summary>
    public const string AccountCreatedAction = "admin.account_created";

    private readonly AetherisDbContext _dbContext;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminAccountService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="userManager">The account manager.</param>
    /// <param name="timeProvider">The clock.</param>
    public AdminAccountService(AetherisDbContext dbContext, UserManager<IdentityUser> userManager, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _userManager = userManager;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Creates an administrator account and its authenticator key.
    /// </summary>
    /// <param name="email">The e-mail address of the administrator.</param>
    /// <param name="password">The password of a new account; ignored when the account exists.</param>
    /// <param name="displayName">The name shown in the admin panel.</param>
    /// <param name="roles">The role codes, from the <c>admin_role</c> table.</param>
    /// <returns>The authenticator key to enrol in an authenticator app.</returns>
    public async Task<string> CreateAsync(string email, string password, string displayName, IReadOnlyCollection<string> roles)
    {
        ArgumentException.ThrowIfNullOrEmpty(email);
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(roles);

        List<AdminRoleEntity> knownRoles = await _dbContext.AdminRoles.AsNoTracking()
            .Where(role => roles.Contains(role.Code))
            .ToListAsync().ConfigureAwait(false);
        List<string> unknownRoles = roles.Except(knownRoles.Select(role => role.Code), StringComparer.Ordinal).ToList();

        if (roles.Count == 0 || unknownRoles.Count > 0)
        {
            throw new ArgumentException($"Unknown administrator roles: {string.Join(", ", unknownRoles)}.", nameof(roles));
        }

        IdentityUser? user = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);

        if (user is null)
        {
            user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            IdentityResult created = await _userManager.CreateAsync(user, password).ConfigureAwait(false);

            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    "The account could not be created: " + string.Join(", ", created.Errors.Select(error => error.Code)));
            }
        }

        if (await _dbContext.AdminAccounts.AnyAsync(admin => admin.IdentityUserId == user.Id).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The account is already an administrator.");
        }

        await _userManager.ResetAuthenticatorKeyAsync(user).ConfigureAwait(false);
        await _userManager.SetTwoFactorEnabledAsync(user, true).ConfigureAwait(false);

        string key = await _userManager.GetAuthenticatorKeyAsync(user).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The authenticator key was not created.");

        AdminAccountEntity admin = new()
        {
            Id = Guid.NewGuid().ToString(),
            IdentityUserId = user.Id,
            DisplayName = displayName,
            TwoFactorEnabled = true,
            CreatedAt = _timeProvider.GetUtcNow(),
        };

        _dbContext.AdminAccounts.Add(admin);
        _dbContext.AdminAccountRoles.AddRange(knownRoles.Select(role => new AdminAccountRoleEntity
        {
            AdminAccountId = admin.Id,
            AdminRoleId = role.Id,
        }));
        _dbContext.AdminAuditLogs.Add(new AdminAuditLogEntity
        {
            AdminId = SystemActor,
            Action = AccountCreatedAction,
            Target = admin.Id,
            NewValue = string.Join(',', knownRoles.Select(role => role.Code)),
            At = admin.CreatedAt,
        });

        await _dbContext.SaveChangesAsync().ConfigureAwait(false);

        return key;
    }
}
