using System.Collections.Concurrent;
using Aetheris.Host.Auth;

namespace Aetheris.Tests.Infrastructure;

/// <summary>
/// Keeps the account e-mails instead of sending them, so that a test can read the tokens.
/// </summary>
public sealed class CapturingEmailSender : IAccountEmailSender
{
    private readonly ConcurrentQueue<AccountEmail> _emails = new();

    /// <summary>
    /// Gets every e-mail sent so far.
    /// </summary>
    public IReadOnlyCollection<AccountEmail> Sent => _emails;

    /// <inheritdoc />
    public Task SendAsync(AccountEmail email, CancellationToken cancellationToken)
    {
        _emails.Enqueue(email);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Finds the last e-mail of a kind sent to an address.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <param name="kind">The kind of the e-mail.</param>
    /// <returns>The e-mail.</returns>
    public AccountEmail Last(string address, string kind) =>
        _emails.Last(email => email.Address == address && email.Kind == kind);
}
