using System.Text.Json.Serialization;

namespace Aetheris.Economy.Api;

/// <summary>
/// The change of a balance of a civilization by an administrator (task 00015).
/// </summary>
/// <param name="ResourceTypeId">The currency of the balance.</param>
/// <param name="Amount">How much to give; a negative amount takes it away.</param>
/// <param name="Reason">Why the administrator changed the balance; it is required.</param>
public sealed record AdminBalanceUpdate(
    [property: JsonPropertyName("resource_type_id")] int ResourceTypeId,
    [property: JsonPropertyName("amount")] long Amount,
    [property: JsonPropertyName("reason")] string? Reason);

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
