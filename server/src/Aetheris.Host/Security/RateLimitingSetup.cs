using System.Globalization;
using System.Threading.RateLimiting;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Microsoft.AspNetCore.RateLimiting;

namespace Aetheris.Host.Security;

/// <summary>
/// The rate limits of the server (task 00002, section 2.3).
/// </summary>
/// <remarks>
/// <para>
/// Every request counts against the per-IP limit. An endpoint also names one of the
/// <see cref="RateLimitGroups"/>: the commands and the admin requests are counted per
/// account, sign-in and registration per IP with a stricter limit.
/// </para>
/// <para>
/// The limits are read from the configuration for every request. The current limit is part
/// of the partition key, so a changed value starts a fresh window at once and the old
/// partitions expire on their own.
/// </para>
/// <para>
/// A rejected request gets <c>429</c> with the <c>rate_limited</c> code and is written to the
/// security journal.
/// </para>
/// </remarks>
public static class RateLimitingSetup
{
    /// <summary>
    /// Registers the rate limiter with the global per-IP limit and one policy per group.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    public static IServiceCollection AddAetherisRateLimiting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = OnRejectedAsync;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                IsExempt(httpContext)
                    ? RateLimitPartition.GetNoLimiter(string.Empty)
                    : CreatePartition(httpContext, RateLimitGroups.Ip, GetIpAddress(httpContext)));

            foreach (string group in RateLimitGroups.EndpointGroups)
            {
                options.AddPolicy(group, new GroupPolicy(group));
            }
        });

        return services;
    }

    private static RateLimitPartition<string> CreatePartition(HttpContext httpContext, string group, string caller)
    {
        SecuritySettings settings = httpContext.RequestServices.GetRequiredService<SecuritySettings>();
        (int permitLimit, TimeSpan window) = settings.RateLimit(group);

        string key = string.Create(
            CultureInfo.InvariantCulture,
            $"{group}|{caller}|{permitLimit}|{window.TotalSeconds}");

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    }

    private static async ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        HttpContext httpContext = context.HttpContext;

        string group = httpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
            ?? RateLimitGroups.Ip;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            httpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        SecurityEventLog securityLog = httpContext.RequestServices.GetRequiredService<SecurityEventLog>();
        await securityLog
            .WriteAsync(SecurityEventTypes.RateLimitExceeded, httpContext, httpContext.User.GetPlayerId(), group)
            .ConfigureAwait(false);

        await httpContext.Response
            .WriteAsJsonAsync(new ApiError(GameErrorCodes.RateLimited), cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsExempt(HttpContext httpContext) =>
        httpContext.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
        || httpContext.Request.Path.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase);

    private static string GetIpAddress(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>
    /// The policy of one rate limit group: per account when the caller is signed in,
    /// per IP otherwise. The sign-in groups are always counted per IP.
    /// </summary>
    private sealed class GroupPolicy : IRateLimiterPolicy<string>
    {
        private readonly string _group;

        public GroupPolicy(string group)
        {
            _group = group;
        }

        // The global OnRejected handler answers every rejection.
        public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

        public RateLimitPartition<string> GetPartition(HttpContext httpContext)
        {
            string? accountId = _group is RateLimitGroups.Auth or RateLimitGroups.AdminAuth
                ? null
                : httpContext.User.GetPlayerId();

            string caller = accountId is not null
                ? "account:" + accountId
                : "ip:" + GetIpAddress(httpContext);

            return CreatePartition(httpContext, _group, caller);
        }
    }
}
