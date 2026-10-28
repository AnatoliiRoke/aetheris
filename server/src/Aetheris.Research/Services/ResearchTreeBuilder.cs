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

        List<TechnologyDto> technologies =
        [
            .. snapshot.Catalog.Technologies.Select(technology => BuildTechnology(snapshot, technology)),
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

        return new ResearchTreeDto(
            [.. BuildBranches(snapshot)],
            technologies,
            queue,
            snapshot.QueueSlots,
            snapshot.Queue.Count,
            snapshot.SelectedPlanetId,
            snapshot.AvailableCenterLevel);
    }

    private static TechnologyDto BuildTechnology(ResearchTreeSnapshot snapshot, TechnologyInfo technology)
    {
        int learned = snapshot.LearnedLevels.GetValueOrDefault(technology.Id);
        ResearchQueueItemEntity? queued = snapshot.Queue.FirstOrDefault(item => item.TechnologyId == technology.Id);

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
        else if (snapshot.AvailableCenterLevel < technology.ResearchCenterLevel)
        {
            // The research of the technology starts on a chosen body, and the science centre of
            // that body must be of the level the technology needs.
            status = ResearchTechnologyStatuses.Unavailable;
            reason = ResearchErrorCodes.ResearchCenterLevel;
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
            technology.Branch,
            technology.SortOrder,
            technology.MaxLevel,
            learned,
            status,
            technology.ResearchCenterLevel,
            snapshot.AvailableCenterLevel,
            reason,
            [.. technology.Levels.Keys.Order().Select(level => BuildLevel(technology, level))]);
    }

    private static TechnologyLevelDto BuildLevel(TechnologyInfo technology, int level)
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
                effect.UnlockKind is null ? null : string.Empty,
                effect.Value))],
            []);
    }

    private static IEnumerable<ResearchBranchDto> BuildBranches(ResearchTreeSnapshot snapshot)
    {
        foreach (string branch in snapshot.Catalog.Technologies
            .Select(technology => technology.Branch)
            .Distinct()
            .Order(StringComparer.Ordinal))
        {
            yield return new ResearchBranchDto(branch, ResearchBranchTextKeys.Name(branch));
        }
    }
}
