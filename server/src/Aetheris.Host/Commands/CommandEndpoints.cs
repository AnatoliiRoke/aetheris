using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aetheris.Core.Commands;
using Aetheris.Core.Errors;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Host.Security;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Aetheris.Host.Commands;

/// <summary>
/// Publishes the registered commands as endpoints and runs them through the command
/// pipeline (task 00002, section 1a).
/// </summary>
/// <remarks>
/// The order of the checks: authentication and the policy, the rate limit group (both on
/// the endpoint), the request identifier, the body size, the validation rules, the
/// ownership of the target object, and finally the execution inside a transaction that
/// also stores the request identifier and the response. A second request with the same
/// identifier waits for the first one and gets its stored response.
/// </remarks>
public static partial class CommandEndpoints
{
    /// <summary>
    /// The size of the chunks in which a request body is read.
    /// </summary>
    private const int ReadBufferSize = 8 * 1024;

    /// <summary>
    /// The response header that marks a replayed response.
    /// </summary>
    public const string ReplayedHeader = "Idempotent-Replayed";

    private static readonly MethodInfo ExecuteMethod = typeof(CommandEndpoints)
        .GetMethod(nameof(ExecuteAsync), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("The command executor is missing.");

    /// <summary>
    /// Maps every registered command.
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapCommands(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        using IServiceScope scope = endpoints.ServiceProvider.CreateScope();

        foreach (CommandRegistration registration in endpoints.ServiceProvider.GetServices<CommandRegistration>())
        {
            ICommandDefinition definition =
                (ICommandDefinition)scope.ServiceProvider.GetRequiredService(registration.CommandType);

            RequestDelegate handler = ExecuteMethod
                .MakeGenericMethod(registration.CommandType, registration.RequestType, registration.ResponseType)
                .CreateDelegate<RequestDelegate>();

            endpoints.MapPost(definition.Route, handler)
                .WithName(registration.CommandType.Name)
                .RequireAuthorization(definition.Policy)
                .RequireRateLimiting(definition.RateLimitGroup);
        }

        return endpoints;
    }

    private static async Task ExecuteAsync<TCommand, TRequest, TResponse>(HttpContext httpContext)
        where TCommand : Command<TRequest, TResponse>
        where TRequest : class
    {
        IServiceProvider services = httpContext.RequestServices;
        CancellationToken cancellationToken = httpContext.RequestAborted;
        SecuritySettings settings = services.GetRequiredService<SecuritySettings>();

        string? playerId = httpContext.User.GetPlayerId();

        if (playerId is null)
        {
            await ApiErrors.WriteAsync(httpContext, StatusCodes.Status401Unauthorized, GameErrorCodes.NotAuthenticated);
            return;
        }

        string requestId = httpContext.Request.Headers[AuthCookies.IdempotencyHeader].ToString();

        if (requestId.Length == 0
            || requestId.Length > settings.IdempotencyKeyMaxLength
            || !RequestIdPattern().IsMatch(requestId))
        {
            await WriteResultAsync(httpContext, ApiErrors.Validation(new Dictionary<string, string[]>
            {
                [AuthCookies.IdempotencyHeader] = ["invalid_request_id"],
            }));
            return;
        }

        byte[]? body = await ReadBodyAsync(httpContext.Request, settings.RequestMaxBodyBytes, cancellationToken);

        if (body is null)
        {
            await ApiErrors.WriteAsync(httpContext, StatusCodes.Status413PayloadTooLarge, GameErrorCodes.PayloadTooLarge);
            return;
        }

        TRequest? request = Deserialize<TRequest>(body);

        if (request is null)
        {
            await WriteResultAsync(httpContext, ApiErrors.Validation(new Dictionary<string, string[]>
            {
                ["body"] = ["invalid_json"],
            }));
            return;
        }

        TCommand command = services.GetRequiredService<TCommand>();

        InlineValidator<TRequest> validator = new();
        command.DefineRules(validator);
        ValidationResult validation = await validator.ValidateAsync(request, cancellationToken);

        if (!validation.IsValid)
        {
            await WriteResultAsync(httpContext, ApiErrors.Validation(ValidationFilter.ToDetails(validation)));
            return;
        }

        if (command.GetTarget(request) is { } target)
        {
            OwnershipChecker ownership = services.GetRequiredService<OwnershipChecker>();

            if (!await ownership.IsOwnerAsync(playerId, target.ObjectType, target.ObjectId, cancellationToken))
            {
                await services.GetRequiredService<SecurityEventLog>().WriteAsync(
                    SecurityEventTypes.OwnershipDenied,
                    httpContext,
                    playerId,
                    target.ObjectType);

                await ApiErrors.WriteAsync(httpContext, StatusCodes.Status403Forbidden, GameErrorCodes.NotYourObject);
                return;
            }
        }

        await ExecuteOnceAsync<TCommand, TRequest, TResponse>(httpContext, command, request, playerId, requestId, body);
    }

    private static async Task ExecuteOnceAsync<TCommand, TRequest, TResponse>(
        HttpContext httpContext,
        TCommand command,
        TRequest request,
        string playerId,
        string requestId,
        byte[] body)
        where TCommand : Command<TRequest, TResponse>
        where TRequest : class
    {
        IServiceProvider services = httpContext.RequestServices;
        CancellationToken cancellationToken = httpContext.RequestAborted;
        AetherisDbContext dbContext = services.GetRequiredService<AetherisDbContext>();
        TimeProvider timeProvider = services.GetRequiredService<TimeProvider>();

        CommandExecutionScope executionScope = services.GetRequiredService<CommandExecutionScope>();
        executionScope.ActorId = playerId;
        executionScope.RequestId = requestId;

        string route = command.Route;
        string requestHash = Convert.ToHexStringLower(SHA256.HashData(body));
        DateTimeOffset now = timeProvider.GetUtcNow();

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // A concurrent request with the same identifier waits here until the first one
        // commits or rolls back.
        int inserted = await dbContext.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO request_idempotency (user_id, key, route, request_hash, created_at)
            VALUES ({playerId}, {requestId}, {route}, {requestHash}, {now})
            ON CONFLICT (user_id, key) DO NOTHING
            """,
            cancellationToken);

        if (inserted == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            await ReplayAsync(httpContext, dbContext, playerId, requestId, route, requestHash);
            return;
        }

        TResponse response;

        try
        {
            response = await command.ExecuteAsync(request, new CommandContext(playerId, requestId), cancellationToken);
        }
        catch (GameException exception)
        {
            // The request identifier is rolled back together with the change, so the client
            // may retry after fixing the cause.
            await transaction.RollbackAsync(CancellationToken.None);
            await WriteResultAsync(
                httpContext,
                ApiErrors.Result(ApiErrors.StatusFor(exception.Code), exception.Code, exception.Details));
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        string responseBody = JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
        int statusCode = StatusCodes.Status200OK;

        await dbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE request_idempotency
            SET status_code = {statusCode}, response_body = {responseBody}
            WHERE user_id = {playerId} AND key = {requestId}
            """,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        await WriteJsonAsync(httpContext, statusCode, responseBody);
    }

    private static async Task ReplayAsync(
        HttpContext httpContext,
        AetherisDbContext dbContext,
        string playerId,
        string requestId,
        string route,
        string requestHash)
    {
        RequestIdempotencyEntity? stored = await dbContext.RequestIdempotency
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entry => entry.UserId == playerId && entry.Key == requestId,
                httpContext.RequestAborted);

        // The same identifier with another command or another body is a client error, not a
        // repetition: the stored response would not answer it.
        if (stored?.StatusCode is null
            || stored.ResponseBody is null
            || !string.Equals(stored.Route, route, StringComparison.Ordinal)
            || !string.Equals(stored.RequestHash, requestHash, StringComparison.Ordinal))
        {
            await ApiErrors.WriteAsync(httpContext, StatusCodes.Status409Conflict, GameErrorCodes.DuplicateRequest);
            return;
        }

        httpContext.Response.Headers[ReplayedHeader] = "true";

        await WriteJsonAsync(httpContext, stored.StatusCode.Value, stored.ResponseBody);
    }

    private static async Task<byte[]?> ReadBodyAsync(HttpRequest request, long maxBytes, CancellationToken cancellationToken)
    {
        using MemoryStream buffer = new();
        byte[] chunk = new byte[ReadBufferSize];

        int read;

        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static TRequest? Deserialize<TRequest>(byte[] body)
        where TRequest : class
    {
        if (body.Length == 0)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TRequest>(body, JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Task WriteResultAsync(HttpContext httpContext, IResult result) => result.ExecuteAsync(httpContext);

    private static async Task WriteJsonAsync(HttpContext httpContext, int statusCode, string json)
    {
        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json; charset=utf-8";

        await httpContext.Response.WriteAsync(json, httpContext.RequestAborted);
    }

    /// <summary>
    /// A request identifier is made of letters, digits, dashes and underscores, for example a UUID.
    /// </summary>
    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex RequestIdPattern();
}
