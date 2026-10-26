using Aetheris.Core.Configuration;
using Aetheris.Core.Time;
using Aetheris.Host.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aetheris.Tests.Infrastructure;

/// <summary>
/// A game clock that runs ahead of the real one by a fixed span, so a test can let the
/// scheduler handle the events of a later day without waiting for it.
/// </summary>
/// <param name="inner">The clock of the server.</param>
/// <param name="shift">How far the game time runs ahead.</param>
public sealed class ShiftedGameClock(IGameClock inner, TimeSpan shift) : IGameClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => inner.UtcNow;

    /// <inheritdoc />
    public DateTimeOffset GameNow => inner.GameNow + shift;

    /// <inheritdoc />
    public double Multiplier => inner.Multiplier;

    /// <inheritdoc />
    public DateTimeOffset ToGameTime(DateTimeOffset realTime) => inner.ToGameTime(realTime) + shift;

    /// <inheritdoc />
    public DateTimeOffset ToRealTime(DateTimeOffset gameTime) => inner.ToRealTime(gameTime - shift);

    /// <summary>
    /// Runs every event that is due at the shifted game time, batch after batch.
    /// </summary>
    /// <param name="factory">The test host.</param>
    /// <param name="shift">How far the game time runs ahead.</param>
    /// <returns>The number of applied events.</returns>
    public static async Task<int> RunDueEventsAsync(AetherisTestFactory factory, TimeSpan shift)
    {
        ArgumentNullException.ThrowIfNull(factory);

        IServiceProvider services = factory.Services;
        ScheduledEventRunner runner = new(
            services.GetRequiredService<IServiceScopeFactory>(),
            new ShiftedGameClock(services.GetRequiredService<IGameClock>(), shift),
            services.GetRequiredService<IGameConfig>(),
            services.GetRequiredService<ILogger<ScheduledEventRunner>>());

        int total = 0;
        int applied;

        do
        {
            applied = await runner.RunDueEventsAsync(TestContext.Current.CancellationToken);
            total += applied;
        }
        while (applied > 0);

        return total;
    }
}
