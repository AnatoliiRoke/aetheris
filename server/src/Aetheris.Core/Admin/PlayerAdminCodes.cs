namespace Aetheris.Core.Admin;

/// <summary>
/// The error codes of the player screen of the admin panel.
/// </summary>
public static class PlayerAdminCodes
{
    /// <summary>An amount, a population or a queue slot count is below zero.</summary>
    public const string AmountNegative = "admin_amount_negative";

    /// <summary>A building or technology level is outside the catalog.</summary>
    public const string LevelOutOfRange = "admin_level_out_of_range";

    /// <summary>A queue record still targets the subject, so the level cannot be replaced.</summary>
    public const string QueueBlocks = "admin_queue_blocks";

    /// <summary>The resource is a currency, a population or a balance, not a stock of a body.</summary>
    public const string NotAStock = "admin_not_a_stock";
}
