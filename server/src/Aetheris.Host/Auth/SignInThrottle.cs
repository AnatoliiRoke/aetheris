using System.Security.Cryptography;
using System.Text;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Host.Security;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Auth;

/// <summary>
/// Locks the sign-in after a series of failures, separately for the typed name and for the
/// IP address (task 00006, decision 4).
/// </summary>
/// <remarks>
/// <para>
/// The failures are counted since the last success of the same name or address, within
/// <c>security.login.lockout_minutes</c>. A name is locked after
/// <c>security.login.max_failed_attempts</c> failures, an address after
/// <c>security.login.ip_max_failed_attempts</c>. A refused attempt during the lockout is not
/// counted, so the lockout ends after the configured minutes.
/// </para>
/// <para>
/// An unknown name is counted like a registered one, so the lockout tells nothing about which
/// names exist. The account lockout of <see cref="SignInChecker"/> stays as a second line.
/// </para>
/// </remarks>
public sealed class SignInThrottle
{
    /// <summary>
    /// The address key of a request without a known remote address, so such requests share
    /// one counter instead of escaping the limit.
    /// </summary>
    public const string UnknownAddress = "unknown";

    private readonly AetherisDbContext _dbContext;
    private readonly SecuritySettings _settings;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignInThrottle"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="settings">The security settings.</param>
    /// <param name="timeProvider">The clock.</param>
    public SignInThrottle(AetherisDbContext dbContext, SecuritySettings settings, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _settings = settings;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Tells whether the sign-in is locked for a name or an address.
    /// </summary>
    /// <param name="login">The typed e-mail address or nickname.</param>
    /// <param name="ipAddress">The remote IP address.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the attempt must be refused.</returns>
    public async Task<bool> IsLockedAsync(string login, string? ipAddress, CancellationToken cancellationToken)
    {
        DateTimeOffset windowStart = _timeProvider.GetUtcNow() - _settings.LoginLockout;
        string loginHash = Hash(login);

        int loginFailures = await CountFailuresAsync(
            _dbContext.SignInAttempts.Where(attempt => attempt.LoginHash == loginHash),
            windowStart,
            cancellationToken).ConfigureAwait(false);

        if (loginFailures >= _settings.LoginMaxFailedAttempts)
        {
            return true;
        }

        string address = ipAddress ?? UnknownAddress;

        int ipFailures = await CountFailuresAsync(
            _dbContext.SignInAttempts.Where(attempt => attempt.IpAddress == address),
            windowStart,
            cancellationToken).ConfigureAwait(false);

        return ipFailures >= _settings.LoginIpMaxFailedAttempts;
    }

    /// <summary>
    /// Records an attempt.
    /// </summary>
    /// <param name="login">The typed e-mail address or nickname.</param>
    /// <param name="ipAddress">The remote IP address.</param>
    /// <param name="succeeded">Whether the attempt succeeded.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the attempt is stored.</returns>
    public async Task RecordAsync(string login, string? ipAddress, bool succeeded, CancellationToken cancellationToken)
    {
        _dbContext.SignInAttempts.Add(new SignInAttemptEntity
        {
            LoginHash = Hash(login),
            IpAddress = ipAddress ?? UnknownAddress,
            Succeeded = succeeded,
            At = _timeProvider.GetUtcNow(),
        });

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Hashes a typed name: trimmed and in lower case, so that the case does not split the count.
    /// </summary>
    /// <param name="login">The typed name.</param>
    /// <returns>The hex encoded SHA-256 hash.</returns>
    public static string Hash(string login)
    {
        ArgumentNullException.ThrowIfNull(login);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(login.Trim().ToLowerInvariant())));
    }

    private static async Task<int> CountFailuresAsync(
        IQueryable<SignInAttemptEntity> attempts,
        DateTimeOffset windowStart,
        CancellationToken cancellationToken)
    {
        DateTimeOffset? lastSuccess = await attempts
            .Where(attempt => attempt.Succeeded && attempt.At >= windowStart)
            .MaxAsync(attempt => (DateTimeOffset?)attempt.At, cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset since = lastSuccess ?? windowStart;

        return await attempts
            .CountAsync(attempt => !attempt.Succeeded && attempt.At >= since, cancellationToken)
            .ConfigureAwait(false);
    }
}
