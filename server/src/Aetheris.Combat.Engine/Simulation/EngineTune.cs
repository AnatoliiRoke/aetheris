namespace Aetheris.Combat.Engine;

/// <summary>Structural numbers of the formulas. They are not balance values.</summary>
internal static class EngineTune
{
    /// <summary>Keep-distance holds this share of the longest weapon.</summary>
    public const double KeepDistanceShare = 0.8;

    /// <summary>The grid is rebuilt on this period of ticks.</summary>
    public const int GridRebuildPeriod = 4;

    /// <summary>A cell is the longest range divided by this.</summary>
    public const int GridCellsPerRange = 2;

    /// <summary>Both the searcher and the target can move between rebuilds.</summary>
    public const int GridPadSides = 2;

    /// <summary>How an array grows when a pool is too small.</summary>
    public const int GrowthFactor = 2;
}
