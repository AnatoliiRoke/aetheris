using System.Net;
using Aetheris.Core.Errors;
using Aetheris.Tests.Infrastructure;

namespace Aetheris.Tests.Security;

/// <summary>
/// Tests that a new protected request gets every protection from one class and one
/// registration line.
/// </summary>
/// <remarks>
/// Acceptance criterion 10 of task 00002. <see cref="TestNoteCommand"/> is the whole
/// request; it is registered by one line in <see cref="AetherisTestFactory"/>. The README
/// section "Як додати новий захищений запит" describes the same steps.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class ProtectedRequestExampleTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtectedRequestExampleTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public ProtectedRequestExampleTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    /// <summary>
    /// The new request is published, protected by the session, the anti-forgery token, the
    /// validation rules, the ownership check and the request identifier.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_new_request_gets_every_protection_from_one_class_and_one_line()
    {
        TestAccount player = await TestAccounts.RegisterConfirmedAsync(_factory);
        TestAccount other = await TestAccounts.RegisterConfirmedAsync(_factory);
        object ownNote = new { playerId = player.UserId, text = "mine" };

        using HttpClient anonymous = _factory.CreateBrowserClient();
        HttpResponseMessage withoutSession = await TestAccounts.SendCommandAsync(anonymous, TestNoteCommand.RoutePath, ownNote, NewId());
        Assert.Equal(HttpStatusCode.Unauthorized, withoutSession.StatusCode);

        using HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, player);

        HttpResponseMessage accepted = await TestAccounts.SendCommandAsync(client, TestNoteCommand.RoutePath, ownNote, NewId());
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        HttpResponseMessage invalid = await TestAccounts.SendCommandAsync(
            client,
            TestNoteCommand.RoutePath,
            new { playerId = player.UserId, text = new string('x', TestNoteCommand.MaxTextLength + 1) },
            NewId());
        Assert.Equal(GameErrorCodes.ValidationFailed, await TestAccounts.ReadErrorAsync(invalid));

        HttpResponseMessage foreign = await TestAccounts.SendCommandAsync(
            client,
            TestNoteCommand.RoutePath,
            new { playerId = other.UserId, text = "theirs" },
            NewId());
        Assert.Equal(GameErrorCodes.NotYourObject, await TestAccounts.ReadErrorAsync(foreign));

        client.DefaultRequestHeaders.Remove(Aetheris.Host.Security.AuthCookies.CsrfHeader);
        HttpResponseMessage withoutCsrf = await TestAccounts.SendCommandAsync(client, TestNoteCommand.RoutePath, ownNote, NewId());
        Assert.Equal(GameErrorCodes.CsrfTokenInvalid, await TestAccounts.ReadErrorAsync(withoutCsrf));
    }

    private static string NewId() => Guid.NewGuid().ToString();
}
