using Aetheris.World.Configuration;
using Aetheris.World.Data.Entities;

namespace Aetheris.World.Generation;

/// <summary>
/// Checks the parameters and the reference data before a generation
/// (task 00004, section 2.4, rule 8).
/// </summary>
/// <remarks>
/// The checks are about consistency, not balance: ranges are ordered, probabilities lie in
/// <c>[0, 1]</c>, every class the generator needs has a type with a positive weight, every
/// rule group can make its picks, and no body gets a resource its group forbids there.
/// </remarks>
public static class WorldConfigValidator
{
    /// <summary>
    /// Validates the generator input.
    /// </summary>
    /// <param name="config">The input.</param>
    /// <returns>The problems found; empty when the input is consistent.</returns>
    public static IReadOnlyList<string> Validate(WorldGenerationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        List<string> errors = [];
        WorldSettings settings = config.Settings;
        WorldReferenceData reference = config.Reference;

        CheckRange(errors, WorldConfigKeys.SystemsMin, settings.SystemsMin, WorldConfigKeys.SystemsMax, settings.SystemsMax);
        CheckRange(errors, WorldConfigKeys.BodiesMin, settings.BodiesMin, WorldConfigKeys.BodiesMax, settings.BodiesMax);
        CheckRange(errors, WorldConfigKeys.FreeFieldsMin, settings.FreeFieldsMin, WorldConfigKeys.FreeFieldsMax, settings.FreeFieldsMax);
        CheckRange(errors, WorldConfigKeys.NebulaRadiusMin, settings.NebulaRadiusMin, WorldConfigKeys.NebulaRadiusMax, settings.NebulaRadiusMax);

        if (settings.SystemsMin < 1)
        {
            errors.Add($"{WorldConfigKeys.SystemsMin} must be positive.");
        }

        if (settings.BodiesMax > settings.OrbitCount - 1)
        {
            errors.Add($"{WorldConfigKeys.BodiesMax} must leave the outermost of {WorldConfigKeys.OrbitCount} orbits free.");
        }

        if (settings.SectorSize <= 0)
        {
            errors.Add($"{WorldConfigKeys.SectorSize} must be positive.");
        }

        CheckProbability(errors, WorldConfigKeys.GasGiantProbability, settings.GasGiantProbability);
        CheckProbability(errors, WorldConfigKeys.SystemFieldProbability, settings.SystemFieldProbability);
        CheckProbability(errors, WorldConfigKeys.FieldRespawnInSystemProbability, settings.FieldRespawnInSystemProbability);
        CheckProbability(errors, WorldConfigKeys.RichPlanetProbability, settings.RichPlanetProbability);
        CheckRange(errors, WorldConfigKeys.DepositCoefficientMin, settings.DepositCoefficientMin, WorldConfigKeys.DepositCoefficientMax, settings.DepositCoefficientMax);

        CheckClass(errors, reference, WorldObjectClasses.Star, required: true);
        CheckClass(errors, reference, WorldObjectClasses.Planet, required: settings.BodiesMax > 0);
        CheckClass(errors, reference, WorldObjectClasses.GasGiant, required: settings.GasGiantProbability > 0 && settings.GasGiantMax > 0);
        CheckClass(errors, reference, WorldObjectClasses.AsteroidField, required: true);
        CheckClass(errors, reference, WorldObjectClasses.Nebula, required: settings.NebulaCount > 0);

        if (!reference.SizeClasses.Any(size => size.GenerationWeight > 0))
        {
            errors.Add("planet_size_class needs a class with a positive weight.");
        }

        foreach (ReferenceSizeClass size in reference.SizeClasses)
        {
            CheckRange(errors, $"planet_size_class {size.Code} size_min", size.SizeMin, "size_max", size.SizeMax);
        }

        foreach (ReferenceObjectType field in reference.TypesOf(WorldObjectClasses.AsteroidField))
        {
            if (field.MassMin is null || field.MassMax is null || field.MassMin <= 0)
            {
                errors.Add($"celestial_object_type {field.Code} needs a positive mass range.");
            }
            else
            {
                CheckRange(errors, $"celestial_object_type {field.Code} mass_min", field.MassMin.Value, "mass_max", field.MassMax.Value);
            }
        }

        foreach (ReferenceRuleGroup group in reference.RuleGroups)
        {
            CheckRuleGroup(errors, reference, group);
        }

        return errors;
    }

    private static void CheckRuleGroup(List<string> errors, WorldReferenceData reference, ReferenceRuleGroup group)
    {
        string name = $"generation_rule_group {group.Code} ({group.ObjectTypeId})";
        int usableRules = group.RuleIds.Count(rule => rule.Weight > 0);

        CheckRange(errors, name + " min_picks", group.MinPicks, "max_picks", group.MaxPicks);
        CheckProbability(errors, name + " extra_pick_probability", group.ExtraPickProbability);

        if (group.MinPicks < 0)
        {
            errors.Add($"{name} min_picks must not be negative.");
        }

        if (group.MinPicks > usableRules)
        {
            errors.Add($"{name} picks more resources than it has rules with a positive weight.");
        }

        ReferenceObjectType? type = reference.FindType(group.ObjectTypeId);
        bool isBody = type is not null
            && (type.ObjectClass == WorldObjectClasses.Planet || type.ObjectClass == WorldObjectClasses.GasGiant);

        foreach (ReferenceRule rule in group.RuleIds)
        {
            CheckRange(errors, $"{name} rule {rule.Id} coefficient_min", rule.CoefficientMin, "coefficient_max", rule.CoefficientMax);

            if (rule.CoefficientMin < 0 || rule.Weight < 0)
            {
                errors.Add($"{name} rule {rule.Id} must not have a negative weight or coefficient.");
            }

            if (isBody && !reference.IsAllowedOnPlanet(rule.ResourceId))
            {
                errors.Add($"{name} rule {rule.Id} puts a resource on a body that its group forbids there.");
            }

            CheckRichRange(errors, name, rule);
        }
    }

    private static void CheckRichRange(List<string> errors, string name, ReferenceRule rule)
    {
        if (rule.RichCoefficientMin is null != rule.RichCoefficientMax is null)
        {
            errors.Add($"{name} rule {rule.Id} needs both bounds of the rich range or none.");
        }
        else if (rule.RichCoefficientMin is { } richMinimum && rule.RichCoefficientMax is { } richMaximum)
        {
            CheckRange(errors, $"{name} rule {rule.Id} rich_coefficient_min", richMinimum, "rich_coefficient_max", richMaximum);

            if (richMinimum < 0)
            {
                errors.Add($"{name} rule {rule.Id} must not have a negative rich coefficient.");
            }
        }
    }

    private static void CheckClass(List<string> errors, WorldReferenceData reference, string objectClass, bool required)
    {
        if (required && !reference.TypesOf(objectClass).Any(type => type.GenerationWeight > 0))
        {
            errors.Add($"celestial_object_type needs a type of class {objectClass} with a positive weight.");
        }
    }

    private static void CheckRange<T>(List<string> errors, string minimumName, T minimum, string maximumName, T maximum)
        where T : IComparable<T>
    {
        if (minimum.CompareTo(maximum) > 0)
        {
            errors.Add($"{minimumName} must not exceed {maximumName}.");
        }
    }

    private static void CheckProbability(List<string> errors, string name, double probability)
    {
        if (probability is < 0d or > 1d || double.IsNaN(probability))
        {
            errors.Add($"{name} must lie between 0 and 1.");
        }
    }
}
