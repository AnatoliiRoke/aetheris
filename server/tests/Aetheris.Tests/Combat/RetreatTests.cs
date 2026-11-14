using Aetheris.Combat.Engine;

namespace Aetheris.Tests.Combat;

/// <summary>A retreating ship crosses the nearest edge and leaves (task 00063).</summary>
public sealed class RetreatTests
{
    [Fact]
    public void A_ship_that_crosses_the_edge_leaves()
    {
        ShipState ship = Engagement.Ship(
            1, 1, 1, 10, 1000,
            [Engagement.Module(1, ModuleKinds.Engine, Engagement.Facts(speed: 100), mass: 0)]);
        SegmentResult finished = Engagement.Run(Engagement.Solo(ship, retreat: true));
        ShipState after = Engagement.Find(finished, 1);

        Assert.True(after.Left);
        Assert.True(after.Position.X < 0);
        Assert.True(Engagement.Logged(finished, LogTypes.SquadronLeft));
        Assert.Equal(OutcomeKinds.Draw, finished.Outcome.Kind);
    }
}
