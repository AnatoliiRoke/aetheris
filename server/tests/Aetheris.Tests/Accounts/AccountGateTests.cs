using System.Net;
using Aetheris.Core.Errors;
using Aetheris.Host.Auth;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Accounts;

/// <summary>
/// Tests of the account gate of the command pipeline (task 00006, acceptance criteria 2
/// and 8).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AccountGateTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountGateTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public AccountGateTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// Before the confirmation a game action is refused with a clear code, while an account
    /// setting works.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_unconfirmed_account_runs_only_the_allowed_actions()
    {
        using HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, await TestAccounts.RegisterAsync(_factory));

        HttpResponseMessage gameAction = await TestAccounts.SendCommandAsync(client, TestGameActionCommand.RoutePath, new { }, "gate-1");
        HttpResponseMessage setting = await TestAccounts.SendCommandAsync(
            client,
            AccountActions.Prefix + "/change-nickname",
            new { nickname = TestAccounts.NewNickname() },
            "gate-2");

        Assert.Equal(HttpStatusCode.Forbidden, gameAction.StatusCode);
        Assert.Equal(GameErrorCodes.EmailNotConfirmed, await TestAccounts.ReadErrorAsync(gameAction));
        Assert.Equal(HttpStatusCode.OK, setting.StatusCode);
    }

    /// <summary>
    /// The list of the allowed actions lives in the database: a new row lets the action pass
    /// the confirmation check at once, without a release.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_allowed_actions_change_in_the_database_without_a_release()
    {
        using HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, await TestAccounts.RegisterAsync(_factory));

        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

        HttpResponseMessage before = await TestAccounts.SendCommandAsync(client, TestGameActionCommand.RoutePath, new { }, "list-1");
        Assert.Equal(GameErrorCodes.EmailNotConfirmed, await TestAccounts.ReadErrorAsync(before));

        dbContext.UnconfirmedAllowedActions.Add(new UnconfirmedAllowedActionEntity { ActionCode = TestGameActionCommand.Code });
        await dbContext.SaveChangesAsync(Token);

        try
        {
            // The confirmation no longer stops it; the unfinished onboarding still does.
            HttpResponseMessage after = await TestAccounts.SendCommandAsync(client, TestGameActionCommand.RoutePath, new { }, "list-2");

            Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
            Assert.Equal(AccountGateErrorCodes.OnboardingRequired, await TestAccounts.ReadErrorAsync(after));
        }
        finally
        {
            await dbContext.UnconfirmedAllowedActions.Where(entry => entry.ActionCode == TestGameActionCommand.Code).ExecuteDeleteAsync(Token);
        }
    }

    /// <summary>
    /// A confirmed account without the finished onboarding gets only the onboarding step.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_game_action_needs_the_finished_onboarding()
    {
        using HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, await TestAccounts.RegisterConfirmedAsync(_factory));

        HttpResponseMessage gameAction = await TestAccounts.SendCommandAsync(client, TestGameActionCommand.RoutePath, new { }, "onboarding-1");

        Assert.Equal(HttpStatusCode.Forbidden, gameAction.StatusCode);
        Assert.Equal(AccountGateErrorCodes.OnboardingRequired, await TestAccounts.ReadErrorAsync(gameAction));
    }
}
