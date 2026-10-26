using Aetheris.World.Configuration;
using Aetheris.World.Data.Entities;
using Aetheris.World.Geometry;

namespace Aetheris.World.Generation;

/// <summary>
/// The pure generator of a sector: <c>Generate(seed, config) → description</c>
/// (task 00004, section 2.2).
/// </summary>
/// <remarks>
/// The generator reads nothing and writes nothing: the same seed and the same configuration
/// always give the same description. Every number comes from <see cref="WorldSettings"/>,
/// every type, resource and rule from <see cref="WorldReferenceData"/>.
/// </remarks>
public static class SectorGenerator
{
    /// <summary>
    /// The number of decimal places of a stored coefficient, the scale of its column.
    /// </summary>
    public const int CoefficientDecimals = 6;

    /// <summary>
    /// Generates a sector.
    /// </summary>
    /// <param name="seed">The seed of the sector.</param>
    /// <param name="sectorName">The name of the sector, used by the name templates.</param>
    /// <param name="config">The parameters and the reference data.</param>
    /// <returns>The description of the sector.</returns>
    /// <exception cref="WorldConfigurationException">The configuration is not consistent.</exception>
    public static SectorPlan Generate(long seed, string sectorName, WorldGenerationConfig config)
    {
        ArgumentNullException.ThrowIfNull(sectorName);
        ArgumentNullException.ThrowIfNull(config);

        IReadOnlyList<string> errors = WorldConfigValidator.Validate(config);

        if (errors.Count > 0)
        {
            throw new WorldConfigurationException(errors);
        }

        WorldSettings settings = config.Settings;
        WorldRandom random = new(seed);

        List<Point3> positions = PlaceSystems(random, settings);
        List<NebulaPlan> nebulae = [];

        for (int number = 1; number <= settings.NebulaCount; number++)
        {
            nebulae.Add(CreateNebula(random, config, sectorName, number));
        }

        List<SystemPlan> systems = [];
        int fieldNumber = 0;

        for (int index = 0; index < positions.Count; index++)
        {
            Point3 position = positions[index];
            int systemNumber = index + 1;
            string systemName = NameTemplate.Format(
                settings.Names.System,
                (NameTemplate.Sector, sectorName),
                (NameTemplate.Number, systemNumber));

            bool inNebula = nebulae.Any(nebula => WorldGeometry.Distance(nebula.Position, position) <= nebula.Radius);
            int starTypeId = PickWeighted(random, config.Reference.TypesOf(WorldObjectClasses.Star), type => type.GenerationWeight).Id;
            List<BodyPlan> bodies = CreateBodies(random, config, systemName);

            FieldPlan? field = null;

            if (random.Chance(settings.SystemFieldProbability))
            {
                fieldNumber++;
                field = CreateField(random, config, FieldName(settings, sectorName, fieldNumber), settings.OrbitCount, position);
            }

            systems.Add(new SystemPlan(systemNumber, systemName, position, inNebula, starTypeId, bodies, field));
        }

        List<FieldPlan> freeFields = [];
        int freeFieldCount = random.NextInclusive(settings.FreeFieldsMin, settings.FreeFieldsMax);
        int systemFieldCount = fieldNumber;

        while (freeFields.Count < freeFieldCount || systemFieldCount + freeFields.Count < settings.MinFieldsPerSector)
        {
            fieldNumber++;
            freeFields.Add(CreateField(random, config, FieldName(settings, sectorName, fieldNumber), null, RandomPoint(random, settings)));
        }

        return new SectorPlan(seed, sectorName, systems, freeFields, nebulae);
    }

    /// <summary>
    /// Creates one asteroid field: its type, mass and constant composition.
    /// </summary>
    /// <param name="random">The random source.</param>
    /// <param name="config">The parameters and the reference data.</param>
    /// <param name="name">The name.</param>
    /// <param name="orbit">The outermost orbit of a system, or <see langword="null"/> for a free field.</param>
    /// <param name="position">The position within the sector.</param>
    /// <returns>The field.</returns>
    public static FieldPlan CreateField(WorldRandom random, WorldGenerationConfig config, string name, int? orbit, Point3 position)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(config);

        ReferenceObjectType type = PickWeighted(
            random,
            config.Reference.TypesOf(WorldObjectClasses.AsteroidField),
            candidate => candidate.GenerationWeight);

        long mass = random.NextInclusive(type.MassMin ?? 0, type.MassMax ?? 0);
        IReadOnlyList<ResourceShare> composition = LimitShares(
            RollComposition(random, config.Reference, type.Id),
            config.Settings.FieldMaxCompositionShare);

        return new FieldPlan(type.Id, name, orbit, position, mass, composition);
    }

    /// <summary>
    /// Gets a random point of the sector.
    /// </summary>
    /// <param name="random">The random source.</param>
    /// <param name="settings">The parameters.</param>
    /// <returns>The point; z stays within the depth of the sector.</returns>
    public static Point3 RandomPoint(WorldRandom random, WorldSettings settings)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(settings);

        return new Point3(
            random.NextDouble(0d, settings.SectorSize),
            random.NextDouble(0d, settings.SectorSize),
            random.NextDouble(0d, settings.SectorDepth));
    }

    /// <summary>
    /// Fills the name template of a field.
    /// </summary>
    /// <param name="settings">The parameters.</param>
    /// <param name="sectorName">The name of the sector.</param>
    /// <param name="number">The number of the field within the sector.</param>
    /// <returns>The name.</returns>
    public static string FieldName(WorldSettings settings, string sectorName, int number)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return NameTemplate.Format(settings.Names.Field, (NameTemplate.Sector, sectorName), (NameTemplate.Number, number));
    }

    private static List<Point3> PlaceSystems(WorldRandom random, WorldSettings settings)
    {
        int count = random.NextInclusive(settings.SystemsMin, settings.SystemsMax);
        List<Point3> positions = new(count);

        for (int index = 0; index < count; index++)
        {
            for (int attempt = 0; attempt < settings.PlacementAttempts; attempt++)
            {
                Point3 candidate = RandomPoint(random, settings);

                if (positions.TrueForAll(placed => WorldGeometry.Distance(placed, candidate) >= settings.MinSystemDistance))
                {
                    positions.Add(candidate);
                    break;
                }
            }
        }

        return positions;
    }

    private static NebulaPlan CreateNebula(WorldRandom random, WorldGenerationConfig config, string sectorName, int number)
    {
        WorldSettings settings = config.Settings;
        ReferenceObjectType type = PickWeighted(
            random,
            config.Reference.TypesOf(WorldObjectClasses.Nebula),
            candidate => candidate.GenerationWeight);

        Point3 position = RandomPoint(random, settings);
        double radius = random.NextDouble(settings.NebulaRadiusMin, settings.NebulaRadiusMax);
        string name = NameTemplate.Format(settings.Names.Nebula, (NameTemplate.Sector, sectorName), (NameTemplate.Number, number));

        return new NebulaPlan(
            type.Id,
            name,
            position,
            radius,
            settings.NebulaBlocksJumpDrive,
            RollComposition(random, config.Reference, type.Id));
    }

    private static List<BodyPlan> CreateBodies(WorldRandom random, WorldGenerationConfig config, string systemName)
    {
        WorldSettings settings = config.Settings;
        WorldReferenceData reference = config.Reference;

        // The outermost orbit is kept for a field (task 00004, decision 4).
        List<int> orbits = [.. Enumerable.Range(1, settings.OrbitCount - 1)];
        int bodyCount = Math.Min(random.NextInclusive(settings.BodiesMin, settings.BodiesMax), orbits.Count);

        Shuffle(random, orbits, bodyCount);
        List<int> chosenOrbits = [.. orbits.Take(bodyCount).Order()];

        int gasGiantCount = 0;

        if (bodyCount > 0 && settings.GasGiantMax > 0 && random.Chance(settings.GasGiantProbability))
        {
            gasGiantCount = random.NextInclusive(1, Math.Min(settings.GasGiantMax, bodyCount));
        }

        List<int> slots = [.. Enumerable.Range(0, bodyCount)];
        Shuffle(random, slots, gasGiantCount);
        HashSet<int> gasGiantSlots = [.. slots.Take(gasGiantCount)];

        IReadOnlyList<ReferenceObjectType> planetTypes = reference.TypesOf(WorldObjectClasses.Planet);
        IReadOnlyList<ReferenceObjectType> gasGiantTypes = reference.TypesOf(WorldObjectClasses.GasGiant);
        List<BodyPlan> bodies = new(bodyCount);

        for (int slot = 0; slot < bodyCount; slot++)
        {
            int orbit = chosenOrbits[slot];
            ReferenceObjectType type = gasGiantSlots.Contains(slot)
                ? PickWeighted(random, gasGiantTypes, candidate => candidate.GenerationWeight)
                : PickWeighted(random, planetTypes, candidate => candidate.GenerationWeight);

            ReferenceSizeClass sizeClass = PickWeighted(random, reference.SizeClasses, candidate => candidate.GenerationWeight);
            int size = random.NextInclusive(sizeClass.SizeMin, sizeClass.SizeMax);

            // A rare rich planet takes the rich range of every rule (task 00009, decision 12).
            bool rich = type.ObjectClass == WorldObjectClasses.Planet && random.Chance(settings.RichPlanetProbability);

            // The groups of a resource say whether it may lie on a body; ether never does.
            List<ResourceShare> deposits =
            [
                .. RollComposition(random, reference, type.Id, rich).Where(share => reference.IsAllowedOnPlanet(share.ResourceId)),
            ];

            string name = NameTemplate.Format(settings.Names.Body, (NameTemplate.System, systemName), (NameTemplate.Orbit, orbit));

            bodies.Add(new BodyPlan(orbit, type.Id, name, sizeClass.Id, size, deposits));
        }

        return bodies;
    }

    /// <summary>
    /// Picks the resources of an object by the rule groups of its type.
    /// </summary>
    /// <param name="random">The random source.</param>
    /// <param name="reference">The reference data.</param>
    /// <param name="objectTypeId">The object type.</param>
    /// <param name="rich">Whether the object is rich: a rule with a rich range uses it instead of the ordinary one.</param>
    /// <returns>The resources with their coefficients; a resource appears once.</returns>
    public static IReadOnlyList<ResourceShare> RollComposition(WorldRandom random, WorldReferenceData reference, int objectTypeId, bool rich = false)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(reference);

        List<ResourceShare> shares = [];

        foreach (ReferenceRuleGroup group in reference.GroupsOf(objectTypeId))
        {
            int picks = group.MinPicks;

            while (picks < group.MaxPicks && random.Chance(group.ExtraPickProbability))
            {
                picks++;
            }

            List<ReferenceRule> available = [.. group.RuleIds.Where(rule => rule.Weight > 0)];

            for (int pick = 0; pick < picks && available.Count > 0; pick++)
            {
                ReferenceRule rule = PickWeighted(random, available, candidate => candidate.Weight);
                available.Remove(rule);

                bool useRich = rich && rule.RichCoefficientMin is not null && rule.RichCoefficientMax is not null;
                decimal minimum = useRich ? rule.RichCoefficientMin!.Value : rule.CoefficientMin;
                decimal maximum = useRich ? rule.RichCoefficientMax!.Value : rule.CoefficientMax;
                decimal span = maximum - minimum;
                decimal coefficient = decimal.Round(
                    minimum + (span * (decimal)random.NextDouble()),
                    CoefficientDecimals,
                    MidpointRounding.ToZero);

                if (!shares.Exists(share => share.ResourceId == rule.ResourceId))
                {
                    shares.Add(new ResourceShare(rule.ResourceId, coefficient));
                }
            }
        }

        return shares;
    }

    /// <summary>
    /// Scales a composition down so that its shares never exceed the largest total share; the
    /// rest of the mass is empty rock.
    /// </summary>
    /// <param name="shares">The shares.</param>
    /// <param name="maximumTotal">The largest total share.</param>
    /// <returns>The limited shares.</returns>
    public static IReadOnlyList<ResourceShare> LimitShares(IReadOnlyList<ResourceShare> shares, decimal maximumTotal)
    {
        ArgumentNullException.ThrowIfNull(shares);

        decimal total = shares.Sum(share => share.Coefficient);

        if (total <= maximumTotal || total <= 0)
        {
            return shares;
        }

        return
        [
            .. shares.Select(share => share with
            {
                Coefficient = decimal.Round(share.Coefficient * maximumTotal / total, CoefficientDecimals, MidpointRounding.ToZero),
            }),
        ];
    }

    private static T PickWeighted<T>(WorldRandom random, IReadOnlyList<T> items, Func<T, int> weight)
    {
        long total = items.Sum(item => (long)Math.Max(0, weight(item)));

        if (total <= 0)
        {
            throw new WorldConfigurationException([$"Nothing to pick among {typeof(T).Name}: every weight is zero."]);
        }

        long roll = random.NextInclusive(0, total - 1);

        foreach (T item in items)
        {
            roll -= Math.Max(0, weight(item));

            if (roll < 0)
            {
                return item;
            }
        }

        return items[^1];
    }

    private static void Shuffle<T>(WorldRandom random, List<T> items, int count)
    {
        for (int index = 0; index < count && index < items.Count - 1; index++)
        {
            int swap = random.NextInclusive(index, items.Count - 1);
            (items[index], items[swap]) = (items[swap], items[index]);
        }
    }
}

/// <summary>
/// The configuration of the world is not consistent; the generator refuses to run.
/// </summary>
public sealed class WorldConfigurationException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorldConfigurationException"/> class.
    /// </summary>
    /// <param name="errors">The problems found.</param>
    public WorldConfigurationException(IReadOnlyList<string> errors)
        : base("The world configuration is not consistent: " + string.Join(" ", errors ?? []))
    {
        Errors = errors ?? [];
    }

    /// <summary>
    /// Gets the problems found.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }
}
