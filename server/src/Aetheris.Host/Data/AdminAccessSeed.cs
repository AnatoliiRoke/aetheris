using System.Text.Json;
using Aetheris.Host.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Data;

/// <summary>
/// Fills the roles and the permissions of the administrators and the list of the read-only
/// parameters (task 00007, decisions 4 and 7).
/// </summary>
/// <remarks>
/// <para>
/// The starting data live in the embedded file <c>Data/admin-access-seed.json</c>, not in the
/// code. A permission is the action code of an admin endpoint, so a new endpoint brings its
/// permission with the seed: a missing permission is added on every start and granted to the
/// roles the file lists for it.
/// </para>
/// <para>
/// What an administrator changed later is kept: a role gets its permissions only when the role
/// itself is new, and the list of the read-only parameters is written only while it is empty.
/// </para>
/// </remarks>
public static class AdminAccessSeed
{
    /// <summary>
    /// The name of the embedded starting data.
    /// </summary>
    public const string StreamName = "Aetheris.Host.admin-access-seed.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Reads the starting data.
    /// </summary>
    /// <returns>The starting data.</returns>
    public static AdminAccessSeedData Load()
    {
        using Stream stream = typeof(AdminAccessSeed).Assembly.GetManifestResourceStream(StreamName)
            ?? throw new InvalidOperationException($"The embedded starting data {StreamName} is missing.");

        return JsonSerializer.Deserialize<AdminAccessSeedData>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"The embedded starting data {StreamName} is empty.");
    }

    /// <summary>
    /// Writes the missing roles, permissions and grants.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="modulePermissions">
    /// The permissions of the endpoints the modules map themselves, such as the reference kinds of
    /// the admin panel (task 00023); a role with every permission gets them as well.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the data are written.</returns>
    public static async Task SeedAsync(
        AetherisDbContext dbContext,
        IReadOnlyList<SeedAdminPermission> modulePermissions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(modulePermissions);

        AdminAccessSeedData file = Load();
        AdminAccessSeedData seed = file with
        {
            Permissions = [.. file.Permissions, .. modulePermissions.Where(permission => !file.Permissions.Any(known => known.Code == permission.Code))],
        };

        Dictionary<string, AdminPermissionEntity> permissions = await dbContext.AdminPermissions
            .ToDictionaryAsync(permission => permission.Code, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        List<AdminPermissionEntity> newPermissions = seed.Permissions
            .Where(permission => !permissions.ContainsKey(permission.Code))
            .Select(permission => new AdminPermissionEntity { Code = permission.Code, Kind = permission.Kind })
            .ToList();

        Dictionary<string, AdminRoleEntity> roles = await dbContext.AdminRoles
            .ToDictionaryAsync(role => role.Code, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        List<(SeedAdminRole Seed, AdminRoleEntity Entity)> newRoles = seed.Roles
            .Where(role => !roles.ContainsKey(role.Code))
            .Select(role => (role, new AdminRoleEntity { Code = role.Code, NameKey = role.NameKey, SortOrder = role.SortOrder }))
            .ToList();

        dbContext.AdminPermissions.AddRange(newPermissions);
        dbContext.AdminRoles.AddRange(newRoles.Select(role => role.Entity));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (AdminPermissionEntity permission in newPermissions)
        {
            permissions[permission.Code] = permission;
        }

        foreach ((SeedAdminRole role, AdminRoleEntity entity) in newRoles)
        {
            roles[role.Code] = entity;
        }

        HashSet<string> newRoleCodes = newRoles.Select(role => role.Seed.Code).ToHashSet(StringComparer.Ordinal);
        HashSet<string> newPermissionCodes = newPermissions.Select(permission => permission.Code).ToHashSet(StringComparer.Ordinal);

        foreach (SeedAdminRole role in seed.Roles)
        {
            IEnumerable<string> granted = role.AllPermissions ? seed.Permissions.Select(permission => permission.Code) : role.Permissions;

            // A new role gets everything the file lists for it; an existing role only gets the
            // permissions that appeared in this start.
            IEnumerable<string> toGrant = newRoleCodes.Contains(role.Code)
                ? granted
                : granted.Where(newPermissionCodes.Contains);

            dbContext.AdminRolePermissions.AddRange(toGrant
                .Where(permissions.ContainsKey)
                .Distinct(StringComparer.Ordinal)
                .Select(code => new AdminRolePermissionEntity
                {
                    AdminRoleId = roles[role.Code].Id,
                    AdminPermissionId = permissions[code].Id,
                }));
        }

        if (!await dbContext.ReadOnlyConfigParameters.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            dbContext.ReadOnlyConfigParameters.AddRange(seed.ReadOnlyParameters
                .Select(key => new ReadOnlyConfigParameterEntity { Key = key }));
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>The starting roles and permissions of the administrators.</summary>
/// <param name="Permissions">The permissions.</param>
/// <param name="Roles">The roles.</param>
/// <param name="ReadOnlyParameters">The keys of the parameters the admin panel never changes.</param>
public sealed record AdminAccessSeedData(
    IReadOnlyList<SeedAdminPermission> Permissions,
    IReadOnlyList<SeedAdminRole> Roles,
    IReadOnlyList<string> ReadOnlyParameters);

/// <summary>A permission of the starting data.</summary>
/// <param name="Code">The action code.</param>
/// <param name="Kind">The kind, one of <see cref="AdminPermissionKinds"/>.</param>
public sealed record SeedAdminPermission(string Code, string Kind);

/// <summary>A role of the starting data.</summary>
/// <param name="Code">The stable code.</param>
/// <param name="NameKey">The localization key of the name.</param>
/// <param name="SortOrder">The position in lists.</param>
/// <param name="AllPermissions">Whether the role gets every permission of the file.</param>
/// <param name="Permissions">The permissions of the role.</param>
public sealed record SeedAdminRole(
    string Code,
    string NameKey,
    int SortOrder,
    bool AllPermissions,
    IReadOnlyList<string> Permissions);
