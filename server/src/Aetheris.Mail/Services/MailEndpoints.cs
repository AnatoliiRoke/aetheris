using System.Security.Claims;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Mail.Api;
using Aetheris.Mail.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aetheris.Mail.Services;

/// <summary>
/// The reads of the mail for a player (task 00060).
/// </summary>
/// <remarks>
/// The reads need a signed-in player and change nothing, so they carry no journal and no
/// idempotency key; the changes go through the commands of the module.
/// </remarks>
public static class MailEndpoints
{
    /// <summary>
    /// The route prefix of the reads of the module.
    /// </summary>
    public const string Prefix = "/api/v1/mail";

    /// <summary>
    /// Maps the reads of the module.
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder mail = endpoints.MapGroup(Prefix)
            .RequireAuthorization(SecurityPolicies.Player)
            .RequireRateLimiting(RateLimitGroups.Command);

        // The list of the letters with the folders and the categories.
        mail.MapGet("/", async (
            HttpContext context,
            ClaimsPrincipal user,
            MailService service,
            CancellationToken cancellationToken) =>
        {
            IQueryCollection query = context.Request.Query;

            MailViewDto view = await service.ReadAsync(
                PlayerId(user),
                Text(query, "folder"),
                Number(query, "category_id"),
                Text(query, "search"),
                int.TryParse(Text(query, "page"), out int page) ? page : 1,
                cancellationToken).ConfigureAwait(false);

            return Results.Json(view, MailJson.Options);
        });

        // One letter with its whole text. The letter the player opens is marked as read.
        mail.MapGet("/letters/{messageId:long}", async (
            long messageId,
            ClaimsPrincipal user,
            MailService service,
            CancellationToken cancellationToken) =>
        {
            MailLetterDetailDto? letter = await service
                .ReadOneAsync(PlayerId(user), messageId, cancellationToken)
                .ConfigureAwait(false);

            return letter is null
                ? Results.NotFound()
                : Results.Json(letter, MailJson.Options);
        });

        // The drafts of the player.
        mail.MapGet("/drafts", async (
            ClaimsPrincipal user,
            MailSendService service,
            CancellationToken cancellationToken) =>
            Results.Json(
                await service.ReadDraftsAsync(PlayerId(user), cancellationToken).ConfigureAwait(false),
                MailJson.Options));

        // The players a letter may be written to.
        mail.MapGet("/recipients", async (
            HttpContext context,
            ClaimsPrincipal user,
            MailSendService service,
            CancellationToken cancellationToken) =>
            Results.Json(
                await service.ReadRecipientsAsync(PlayerId(user), Text(context.Request.Query, "text"), cancellationToken).ConfigureAwait(false),
                MailJson.Options));

        // The number of the letters that are not read: the counter of the top bar.
        mail.MapGet("/unread", async (
            ClaimsPrincipal user,
            MailService service,
            CancellationToken cancellationToken) =>
            Results.Json(
                new MailUnreadDto(await service.ReadUnreadCountAsync(PlayerId(user), cancellationToken).ConfigureAwait(false)),
                MailJson.Options));

        return endpoints;
    }

    /// <summary>
    /// Reads a text out of the query of a request.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="name">The name of the parameter.</param>
    /// <returns>The text, or <see langword="null"/> when the parameter is missing.</returns>
    private static string? Text(IQueryCollection query, string name)
    {
        string value = query[name].ToString();

        return value.Length == 0 ? null : value;
    }

    /// <summary>
    /// Reads a number out of the query of a request.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="name">The name of the parameter.</param>
    /// <returns>The number, or <see langword="null"/> when the parameter is missing or not a number.</returns>
    private static long? Number(IQueryCollection query, string name) =>
        long.TryParse(query[name], out long value) ? value : null;

    /// <summary>
    /// Reads the identifier of the player out of the session.
    /// </summary>
    /// <param name="user">The session.</param>
    /// <returns>The identifier of the player.</returns>
    private static string PlayerId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new GameException(GameErrorCodes.NotAuthenticated, "The request has no player.");
}
