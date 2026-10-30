using Aetheris.Core.Admin;
using Aetheris.Core.Errors;
using Aetheris.Economy.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aetheris.Economy.Api;

/// <summary>
/// The admin panel of the balances of the civilizations (task 00015).
/// </summary>
/// <remarks>
/// The trade currency comes only from the trade until the market arrives, so an
/// administrator gives it and takes it with a reason. The journal of the admin panel is written
/// by the filter of the admin group after the answer, from the target, the old and the new amount
/// the endpoint hands it; a refused change is journaled as rejected.
/// </remarks>
public static class EconomyAdminEndpoints
{
    /// <summary>
    /// The route prefix of the module inside the group of the admin panel.
    /// </summary>
    public const string Prefix = "/economy";

    /// <summary>
    /// Maps the endpoints of the admin panel.
    /// </summary>
    /// <param name="secured">The secured group of the admin panel.</param>
    /// <returns>The same group.</returns>
    public static RouteGroupBuilder Map(RouteGroupBuilder secured)
    {
        ArgumentNullException.ThrowIfNull(secured);

        RouteGroupBuilder economy = secured.MapGroup(Prefix);

        // The balance of a currency of a player.
        economy.MapGet("/balances/{playerId}", async (
            string playerId,
            EconomyService service,
            CancellationToken cancellationToken) =>
        {
            IReadOnlyList<AdminBalanceFacts> balances = await service
                .ReadBalancesAsync(playerId, cancellationToken)
                .ConfigureAwait(false);

            return Results.Json(balances, EconomyJson.Options);
        })
            .WithName("admin.economy_balances_read");

        // The administrator gives or takes a currency of a player.
        economy.MapPut("/balances/{playerId}", async (
            string playerId,
            AdminBalanceUpdate request,
            EconomyService service,
            IAdminAuditRecorder audit,
            IAdminQueryLimits limits,
            CancellationToken cancellationToken) =>
        {
            audit.Record(Target(playerId, request.ResourceTypeId), null, null);

            if (AdminReason.Check(request.Reason, limits) is { } reasonError)
            {
                return Results.Json(
                    new { error = GameErrorCodes.ValidationFailed, details = new Dictionary<string, string[]> { ["reason"] = [reasonError] } },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                AdminBalanceChange applied = await service
                    .ChangeBalanceAsync(playerId, request, cancellationToken)
                    .ConfigureAwait(false);

                audit.Record(applied.Target, applied.OldValue, applied.NewValue);
                audit.RecordReason(request.Reason!.Trim());

                return Results.Json(new { id = applied.Id, version = applied.Version }, EconomyJson.Options);
            }
            catch (EconomyAdminException exception)
            {
                return Results.Json(
                    new { error = exception.Code },
                    statusCode: exception.Code switch
                    {
                        GameErrorCodes.NotFound => StatusCodes.Status404NotFound,
                        AdminReferenceErrors.VersionConflict => StatusCodes.Status409Conflict,
                        _ => StatusCodes.Status400BadRequest,
                    });
            }
        })
            .WithName("admin.economy_balance_updated");

        return economy;
    }

    private static string Target(string playerId, int resourceTypeId) =>
        $"economy_balance:{playerId}:{resourceTypeId}";
}
