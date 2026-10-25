using Aetheris.World.Generation;

namespace Aetheris.World.Extraction;

/// <summary>
/// The yield of one processing cycle on a field.
/// </summary>
/// <param name="ProcessedMass">The mass taken from the field, rock included.</param>
/// <param name="YieldIds">The resources that go to the hold.</param>
/// <param name="RockMass">The processed mass that stays behind as empty rock, rounding included.</param>
/// <param name="RemainingMass">The mass of the field after the cycle.</param>
/// <param name="Depleted">Whether the field is empty and disappears.</param>
public sealed record ExtractionResult(
    long ProcessedMass,
    IReadOnlyList<ResourceAmount> YieldIds,
    long RockMass,
    long RemainingMass,
    bool Depleted);

/// <summary>
/// An amount of one resource.
/// </summary>
/// <param name="ResourceId">The resource.</param>
/// <param name="Amount">The amount, a whole number.</param>
public sealed record ResourceAmount(int ResourceId, long Amount);

/// <summary>
/// The pure rule of processing an asteroid field (task 00004, decisions 14 and 18).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>The processed mass is split by the constant composition; the rest is empty rock.</item>
/// <item>The field loses the whole processed mass, rock included.</item>
/// <item>Amounts are whole (rounded down); the remainder of the rounding goes to the rock.</item>
/// <item>A full hold stops the cycle: only what fits is taken, and the field loses only the
/// matching share of the processed mass.</item>
/// <item>First come, first served: a cycle never takes more than is left; the next one gets
/// the rest, and an empty field disappears.</item>
/// </list>
/// </remarks>
public static class FieldExtraction
{
    /// <summary>
    /// Computes one processing cycle.
    /// </summary>
    /// <param name="fieldMass">The mass left in the field.</param>
    /// <param name="composition">The composition of the field, shares from 0 to 1.</param>
    /// <param name="processingMass">The mass the module processes per cycle.</param>
    /// <param name="freeHold">The free room of the hold.</param>
    /// <returns>The yield of the cycle.</returns>
    public static ExtractionResult Extract(
        long fieldMass,
        IReadOnlyList<ResourceShare> composition,
        long processingMass,
        long freeHold)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentOutOfRangeException.ThrowIfNegative(fieldMass);
        ArgumentOutOfRangeException.ThrowIfNegative(processingMass);
        ArgumentOutOfRangeException.ThrowIfNegative(freeHold);

        long processed = Math.Min(fieldMass, processingMass);
        decimal totalShare = composition.Sum(share => share.Coefficient);

        // When the hold cannot take the whole yield, the cycle processes only the mass whose
        // yield fits: the yield of a mass m is at most m × totalShare.
        if (totalShare > 0 && processed * totalShare > freeHold)
        {
            processed = (long)decimal.Floor(freeHold / totalShare);
        }

        List<ResourceAmount> yields = [];

        foreach (ResourceShare share in composition)
        {
            long amount = (long)decimal.Floor(processed * share.Coefficient);

            if (amount > 0)
            {
                yields.Add(new ResourceAmount(share.ResourceId, amount));
            }
        }

        long rock = processed - yields.Sum(entry => entry.Amount);
        long remaining = fieldMass - processed;

        return new ExtractionResult(processed, yields, rock, remaining, remaining == 0);
    }
}
