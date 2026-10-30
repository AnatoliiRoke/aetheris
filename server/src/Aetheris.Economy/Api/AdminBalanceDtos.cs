using System.Text.Json.Serialization;

namespace Aetheris.Economy.Api;

/// <summary>
/// The change of a balance of a civilization by an administrator (task 00015).
/// </summary>
/// <param name="ResourceTypeId">The currency of the balance.</param>
/// <param name="Amount">How much to give; a negative amount takes it away.</param>
/// <param name="Reason">Why the administrator changed the balance; it is required.</param>
/// <param name="Version">The version of the balance the administrator read; a balance that has another version is not changed (409). A change without it is not checked.</param>
public sealed record AdminBalanceUpdate(
    [property: JsonPropertyName("resource_type_id")] int ResourceTypeId,
    [property: JsonPropertyName("amount")] long Amount,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("version")] int? Version = null);

/// <summary>
/// A balance of a civilization for the admin panel (task 00015).
/// </summary>
/// <param name="PlayerId">The player.</param>
/// <param name="ResourceTypeId">The currency.</param>
/// <param name="Amount">The whole balance now, with the income that has not been written yet.</param>
/// <param name="IncomePerHour">How much the balance grows with in an hour.</param>
/// <param name="Version">The version of the row, to send back with a change.</param>
public sealed record AdminBalanceFacts(
    string PlayerId,
    int ResourceTypeId,
    long Amount,
    decimal IncomePerHour,
    int Version);

/// <summary>
/// The change of a balance that was applied (task 00015).
/// </summary>
/// <param name="Id">The identifier of the balance.</param>
/// <param name="Target">What the journal shows as the changed object.</param>
/// <param name="OldValue">The amount before the change.</param>
/// <param name="NewValue">The amount after the change.</param>
/// <param name="Version">The version of the balance after the change.</param>
public sealed record AdminBalanceChange(
    int Id,
    string Target,
    string OldValue,
    string NewValue,
    int Version);
