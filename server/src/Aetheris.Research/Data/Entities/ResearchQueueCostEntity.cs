namespace Aetheris.Research.Data.Entities;

/// <summary>
/// The resources a record of the queue paid for (task 00012, decision 5a).
/// </summary>
/// <remarks>
/// The amounts are kept, so the cancellation of a record returns exactly what it paid.
/// </remarks>
public sealed class ResearchQueueCostEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the record of the queue.</summary>
    public int ResearchQueueItemId { get; set; }

    /// <summary>Gets or sets the resource of the cost.</summary>
    public int ResourceTypeId { get; set; }

    /// <summary>Gets or sets the amount of the resource.</summary>
    public long Amount { get; set; }
}
