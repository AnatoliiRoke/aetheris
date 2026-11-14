namespace Aetheris.Combat.Engine;

/// <summary>
/// Runs one segment. The same state, the same input and the same settings always
/// produce the same result. The cycle number is not advanced.
/// </summary>
public static class SegmentSimulator
{
    /// <summary>Runs one segment.</summary>
    /// <param name="state">The state at the start of the segment.</param>
    /// <param name="input">The length of the segment.</param>
    /// <param name="settings">The combat parameters.</param>
    /// <returns>The state after the segment, the tracks, the log and the outcome.</returns>
    public static SegmentResult Run(BattleState state, SegmentInput input, CombatSettings settings)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(settings);
        settings.EnsureValid();

        return SegmentRun.Execute(state, input, settings);
    }
}
