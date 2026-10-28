using Aetheris.Requirements.Services;
using Aetheris.Research.Api;
using Aetheris.Research.Data.Entities;
using Aetheris.World.Api;

namespace Aetheris.Research.Services;

/// <summary>
/// Builds the tree of the technologies and the queue for the client (task 00012, section 3.2).
/// </summary>
public sealed class ResearchTreeBuilder
{
    /// <summary>
    /// Builds the answer of the tree of the technologies.
    /// </summary>
    /// <param name="snapshot">The tree and the queue of the player.</param>
    /// <returns>The answer.</returns>
    public ResearchTreeDto Build(ResearchTreeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // An archived technology, or one of an archived branch, is not offered for a new
        // research; the player who learned it or has it in the queue still sees it (task 00023,
        // decision 5).
        List<TechnologyInfo> shown =
        [
            .. snapshot.Catalog.Technologies.Where(technology => snapshot.Catalog.IsOffered(technology)
                || snapshot.LearnedLevels.GetValueOrDefault(technology.Id) > 0
                || snapshot.Queue.Any(item => item.TechnologyId == technology.Id)),
        ];

        List<TechnologyDto> technologies =
        [
            .. shown.Select(technology => BuildTechnology(snapshot, technology)),
        ];

        List<ResearchQueueItemDto> queue =
        [
            .. snapshot.Queue.Select(item => new ResearchQueueItemDto(
                item.Id,
                item.TechnologyId,
                snapshot.Catalog.FindTechnology(item.TechnologyId)?.NameKey ?? string.Empty,
                item.TargetLevel,
                item.PlanetId,
                item.Position,
                item.StartsAt,
                item.FinishesAt,
                [.. item.CostIds.OrderBy(cost => cost.ResourceTypeId).Select(cost => new ResourceAmountDto(cost.ResourceTypeId, cost.Amount))])),
        ];

        List<ResearchPlanetDto> planets =
        [
            .. snapshot.Planets.Select(planet => new ResearchPlanetDto(
                planet.PlanetId,
                planet.Name,
                planet.Level,
                planet.Level > 0,
                planet.Level > 0 ? null : ResearchErrorCodes.ResearchCenterLevel)),
        ];

        return new ResearchTreeDto(
            [.. BuildBranches(snapshot, shown)],
            technologies,
            queue,
            snapshot.QueueSlots,
            snapshot.Queue.Count,
            snapshot.SelectedPlanetId,
            snapshot.AvailableCenterLevel,
            planets,
            snapshot.GameNow,
            snapshot.TimeMultiplier);
    }

    private static TechnologyDto BuildTechnology(ResearchTreeSnapshot snapshot, TechnologyInfo technology)
    {
        int learned = snapshot.LearnedLevels.GetValueOrDefault(technology.Id);
        int nextLevel = Math.Min(learned + 1, technology.MaxLevel);
        ResearchQueueItemEntity? queued = snapshot.Queue.FirstOrDefault(item => item.TechnologyId == technology.Id);

        // The requirements on other technologies are checked here against the learned levels;
        // the requirements on a body are checked by the command on the chosen body.
        HashSet<int> unmet =
        [
            .. snapshot.Requirements
                .Where(requirement => requirement.SubjectId == technology.Id
                    && requirement.Requirement.RequirementType == RequirementTypes.TechnologyLevel
                    && snapshot.LearnedLevels.GetValueOrDefault(requirement.Requirement.TargetId) < requirement.Requirement.TargetLevel)
                .Select(requirement => requirement.Requirement.Id),
        ];

        string status;
        string? reason = null;

        if (learned >= technology.MaxLevel)
        {
            status = ResearchTechnologyStatuses.Learned;
        }
        else if (queued is not null)
        {
            status = queued.FinishesAt is not null
                ? ResearchTechnologyStatuses.Researching
                : ResearchTechnologyStatuses.Queued;
        }
        else if (!snapshot.Catalog.IsOffered(technology))
        {
            status = ResearchTechnologyStatuses.Unavailable;
            reason = ResearchErrorCodes.TechnologyArchived;
        }
        else if (snapshot.AvailableCenterLevel <= 0 || snapshot.AvailableCenterLevel < technology.ResearchCenterLevel)
        {
            // The research of the technology starts on a chosen body, and the science centre of
            // that body must be of the level the technology needs.
            status = ResearchTechnologyStatuses.Unavailable;
            reason = ResearchErrorCodes.ResearchCenterLevel;
        }
        else if (unmet.Count > 0)
        {
            status = ResearchTechnologyStatuses.Unavailable;
            reason = ResearchErrorCodes.RequirementUnmet;
        }
        else
        {
            status = ResearchTechnologyStatuses.Available;
        }

        return new TechnologyDto(
            technology.Id,
            technology.Code,
            technology.NameKey,
            technology.DescriptionKey,
            technology.BranchId,
            snapshot.Catalog.FindBranch(technology.BranchId)?.Code ?? string.Empty,
            technology.SortOrder,
            technology.MaxLevel,
            learned,
            status,
            technology.ResearchCenterLevel,
            snapshot.AvailableCenterLevel,
            reason,
            [.. technology.Levels.Keys.Order().Select(level => BuildLevel(snapshot, technology, level, level == nextLevel ? unmet : []))]);
    }

    private static TechnologyLevelDto BuildLevel(ResearchTreeSnapshot snapshot, TechnologyInfo technology, int level, IReadOnlySet<int> unmet)
    {
        TechnologyLevelInfo? info = technology.Levels.GetValueOrDefault(level);

        return new TechnologyLevelDto(
            level,
            info?.DurationSeconds ?? 0,
            [.. (info?.Costs ?? new Dictionary<int, long>()).OrderBy(cost => cost.Key).Select(cost => new ResourceAmountDto(cost.Key, cost.Value))],
            [.. (info?.Effects ?? []).Select(effect => new TechnologyEffectDto(
                effect.EffectType,
                effect.UnlockKind,
                effect.UnlockTargetId,
                UnlockNameKey(snapshot, effect),
                effect.Value))],
            [.. unmet.Order()]);
    }

    private static string? UnlockNameKey(ResearchTreeSnapshot snapshot, TechnologyEffectInfo effect) =>
        effect.UnlockKind == TechnologyUnlockKinds.BuildingType && effect.UnlockTargetId is int buildingTypeId
            ? snapshot.BuildingNameKeys.GetValueOrDefault(buildingTypeId)
            : null;

    private static IEnumerable<ResearchBranchDto> BuildBranches(ResearchTreeSnapshot snapshot, IReadOnlyList<TechnologyInfo> shown)
    {
        // Every branch of the table is listed in its order, an empty one too (task 00012,
        // decision 11); an archived branch only while the player still sees a technology of it.
        HashSet<int> shownBranchIds = [.. shown.Select(technology => technology.BranchId)];

        foreach (TechnologyBranchInfo branch in snapshot.Catalog.Branches.Where(branch => !branch.IsArchived || shownBranchIds.Contains(branch.Id)))
        {
            yield return new ResearchBranchDto(branch.Id, branch.Code, branch.NameKey);
        }
    }
}
