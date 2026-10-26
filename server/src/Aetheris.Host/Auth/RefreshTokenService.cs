using System.Security.Cryptography;
using System.Text;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Host.Security;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Auth;

/// <summary>
/// The outcome of a refresh.
/// </summary>
/// <param name="UserId">The account of the session, or <see langword="null"/> when the token is not valid.</param>
/// <param name="NewToken">The new refresh token, or <see langword="null"/> when the token is not valid.</param>
/// <param name="ExpiresAt">The expiry of the new token.</param>
/// <param name="Reused">Whether a revoked token was presented again.</param>
/// <param name="IsPersistent">Whether the session was started with "remember me".</param>
public sealed record RefreshResult(string? UserId, string? NewToken, DateTimeOffset ExpiresAt, bool Reused, bool IsPersistent = false);

/// <summary>
/// Issues, rotates and revokes refresh tokens (task 00002, section 2.1).
/// </summary>
/// <remarks>
/// A refresh token is used once. Presenting a revoked token means that a copy of it exists,
/// so every session of the account is revoked.
/// </remarks>
public sealed class RefreshTokenService
{
    /// <summary>
    /// The number of random bytes of a token.
    /// </summary>
    private const int TokenBytes = 32;

    private readonly AetherisDbContext _dbContext;
    private readonly SecuritySettings _settings;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshTokenService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="settings">The security settings.</param>
    /// <param name="timeProvider">The clock.</param>
    public RefreshTokenService(AetherisDbContext dbContext, SecuritySettings settings, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _settings = settings;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Issues a new token.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="persistent">Whether the session was started with "remember me".</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The token and its expiry.</returns>
    /// <remarks>
    /// A session with "remember me" lives <c>security.session.remember_days</c>; any other
    /// lives <c>security.session.refresh_days</c> on the server, and its cookie ends with the
    /// browser.
    /// </remarks>
    public async Task<(string Token, DateTimeOffset ExpiresAt)> IssueAsync(string userId, bool persistent, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        string token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));
        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now + (persistent ? _settings.SessionRememberLifetime : _settings.SessionRefreshLifetime);

        _dbContext.RefreshTokens.Add(new RefreshTokenEntity
        {
            UserId = userId,
            TokenHash = Hash(token),
            CreatedAt = now,
            ExpiresAt = expiresAt,
            IsPersistent = persistent,
        });

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (token, expiresAt);
    }

    /// <summary>
    /// Uses a token: revokes it and issues the next one.
    /// </summary>
    /// <param name="token">The presented token.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The outcome.</returns>
    public async Task<RefreshResult> RotateAsync(string token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        string hash = Hash(token);
        DateTimeOffset now = _timeProvider.GetUtcNow();

        RefreshTokenEntity? stored = await _dbContext.RefreshTokens
            .SingleOrDefaultAsync(entry => entry.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);

        if (stored is null)
        {
            return new RefreshResult(null, null, default, Reused: false);
        }

        if (stored.RevokedAt is not null)
        {
            await RevokeAllAsync(stored.UserId, cancellationToken).ConfigureAwait(false);

            return new RefreshResult(stored.UserId, null, default, Reused: true);
        }

        if (stored.ExpiresAt <= now)
        {
            return new RefreshResult(null, null, default, Reused: false);
        }

        stored.RevokedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        (string newToken, DateTimeOffset expiresAt) =
            await IssueAsync(stored.UserId, stored.IsPersistent, cancellationToken).ConfigureAwait(false);

        return new RefreshResult(stored.UserId, newToken, expiresAt, Reused: false, stored.IsPersistent);
    }

    /// <summary>
    /// Tells whether the session of a token was started with "remember me".
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> for a session with "remember me".</returns>
    public Task<bool> IsPersistentAsync(string token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        string hash = Hash(token);

        return _dbContext.RefreshTokens
            .AsNoTracking()
            .AnyAsync(entry => entry.TokenHash == hash && entry.IsPersistent, cancellationToken);
    }

    /// <summary>
    /// Revokes one token.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the token is revoked.</returns>
    public async Task RevokeAsync(string token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        string hash = Hash(token);
        DateTimeOffset now = _timeProvider.GetUtcNow();

        await _dbContext.RefreshTokens
            .Where(entry => entry.TokenHash == hash && entry.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.RevokedAt, now), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Revokes every token of an account, for example after a password change.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the tokens are revoked.</returns>
    public async Task RevokeAllAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        DateTimeOffset now = _timeProvider.GetUtcNow();

        await _dbContext.RefreshTokens
            .Where(entry => entry.UserId == userId && entry.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.RevokedAt, now), cancellationToken)
            .ConfigureAwait(false);
    }

    private static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
