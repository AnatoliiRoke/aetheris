using System.Security.Claims;
using Aetheris.Core.Admin;
using Aetheris.Host.Localization;
using Aetheris.Host.Security;

namespace Aetheris.Host.Admin;

/// <summary>
/// The shared endpoints of the reference kinds of the admin panel (task 00023, section 4.1):
/// the kinds of every module for the menu, with the description of their fields, the languages
/// of the texts and the limits of the pictures.
/// </summary>
/// <remarks>
/// The modules map the endpoints of their own kinds; the host only gathers their descriptions,
/// so a new kind of a module shows up in the menu without a change of the host or the client.
/// </remarks>
public static class ReferenceKindEndpoints
{
    /// <summary>
    /// Maps the endpoints on the secured admin group.
    /// </summary>
    /// <param name="secured">The secured admin group.</param>
    /// <returns>The same group.</returns>
    public static RouteGroupBuilder MapReferenceKindEndpoints(this RouteGroupBuilder secured)
    {
        ArgumentNullException.ThrowIfNull(secured);

        secured.MapGet("/reference/kinds", ReadKindsAsync)
            .WithName("admin.reference_kinds_read");

        return secured;
    }

    /// <summary>
    /// Lists the kinds the administrator may read, in the order of the menu.
    /// </summary>
    private static async Task<IResult> ReadKindsAsync(
        ClaimsPrincipal principal,
        IEnumerable<IAdminReferenceService> references,
        AdminAccessService access,
        TextSettings textSettings,
        ReferenceImageStore images,
        CancellationToken cancellationToken)
    {
        HashSet<string> granted = [.. await access.GetPermissionCodesAsync(principal.GetAdminId()!, cancellationToken).ConfigureAwait(false)];

        List<AdminReferenceDescriptor> kinds =
        [
            .. references
                .SelectMany(reference => reference.Descriptors)
                .Where(descriptor => granted.Contains(descriptor.Permissions.ListRead))
                .OrderBy(descriptor => descriptor.SortOrder)
                .ThenBy(descriptor => descriptor.Kind, StringComparer.Ordinal),
        ];

        return Results.Json(
            new
            {
                kinds,
                languages = textSettings.Languages,
                max_length = textSettings.MaxLength,
                image_types = images.AllowedTypes,
                image_max_bytes = images.MaxBytes,
            },
            AdminReferenceJson.Options);
    }
}
