using Aetheris.Core.Commands;
using Aetheris.Host.Data;
using Aetheris.World;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Auth;

/// <summary>
/// The error codes of the account gate.
/// </summary>
public static class AccountGateErrorCodes
{
    /// <summary>The player has not finished the first step of the game yet.</summary>
    public const string OnboardingRequired = "onboarding_required";
}

/// <summary>
/// Decides whether the account may run a command (task 00006, decisions 2 and 9).
/// </summary>
/// <remarks>
/// <para>
/// A command needs a confirmed e-mail address unless its action code is listed in
/// <c>unconfirmed_allowed_action</c>; the list is read for every command of an unconfirmed
/// account, so a change in the database applies at once, without a release.
/// </para>
/// <para>
/// A command that needs the finished onboarding is refused until the player has created a
/// civilization in the world.
/// </para>
/// </remarks>
public sealed class AccountGate
{
    private readonly AetherisDbContext _dbContext;
    private readonly IWorldModule _world;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountGate"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="world">The world module.</param>
    public AccountGate(AetherisDbContext dbContext, IWorldModule world)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(world);

        _dbContext = dbContext;
        _world = world;
    }

    /// <summary>
    /// Checks a command.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="null"/> when the command may run, otherwise the error code.</returns>
    public async Task<string?> CheckAsync(string playerId, ICommandDefinition command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        bool confirmed = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == playerId)
            .Select(user => user.EmailConfirmed)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!confirmed && !await IsAllowedUnconfirmedAsync(command.ActionCode, cancellationToken).ConfigureAwait(false))
        {
            return Core.Errors.GameErrorCodes.EmailNotConfirmed;
        }

        if (command.RequiresOnboarding && await _world.GetCivilizationAsync(playerId, cancellationToken).ConfigureAwait(false) is null)
        {
            return AccountGateErrorCodes.OnboardingRequired;
        }

        return null;
    }

    /// <summary>
    /// Tells whether an action is allowed before the e-mail confirmation.
    /// </summary>
    /// <param name="actionCode">The action code.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the action is listed.</returns>
    public Task<bool> IsAllowedUnconfirmedAsync(string actionCode, CancellationToken cancellationToken) =>
        _dbContext.UnconfirmedAllowedActions.AnyAsync(entry => entry.ActionCode == actionCode, cancellationToken);
}
