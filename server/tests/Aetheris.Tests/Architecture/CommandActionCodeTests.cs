using System.Text.RegularExpressions;
using Aetheris.Core.Commands;
using Aetheris.Host.Data;
using Aetheris.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Architecture;

/// <summary>
/// Tests that every command carries its action code (task 00006, section 3.1).
/// </summary>
/// <remarks>
/// The account gate decides by the action code whether an unconfirmed account may run a
/// command. A command class cannot be compiled without the code; this test checks the format
/// of every registered code and that a game command needs the confirmation by default.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed partial class CommandActionCodeTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandActionCodeTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public CommandActionCodeTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    /// <summary>
    /// Every registered command has an action code of the form <c>area.action</c>.
    /// </summary>
    [Fact]
    public void Every_command_declares_a_well_formed_action_code()
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        List<CommandRegistration> registrations = [.. _factory.Services.GetServices<CommandRegistration>()];
        Assert.NotEmpty(registrations);

        List<string> wrong = registrations
            .Select(registration => (ICommandDefinition)scope.ServiceProvider.GetRequiredService(registration.CommandType))
            .Where(command => string.IsNullOrEmpty(command.ActionCode) || !ActionCodePattern().IsMatch(command.ActionCode))
            .Select(command => command.GetType().Name)
            .ToList();

        Assert.True(wrong.Count == 0, $"Commands without a well-formed action code: {string.Join(", ", wrong)}.");
    }

    /// <summary>
    /// A game command needs the confirmation and the onboarding unless it says otherwise, and
    /// the seed allows only the account settings and the onboarding step before the confirmation.
    /// </summary>
    [Fact]
    public void A_new_game_command_is_closed_by_default()
    {
        TestGameActionCommand command = new();

        Assert.True(command.RequiresOnboarding);
        Assert.DoesNotContain(command.ActionCode, AccountListsSeed.Load().UnconfirmedAllowedActions);
        Assert.Equal(["account.settings", "civilization.setup"], AccountListsSeed.Load().UnconfirmedAllowedActions.Order(StringComparer.Ordinal));
    }

    [GeneratedRegex("^[a-z][a-z_]*(\\.[a-z][a-z_]*)+$")]
    private static partial Regex ActionCodePattern();
}
