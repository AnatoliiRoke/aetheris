using Aetheris.Core.Configuration;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Aetheris.World.Generation;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.World.Services;

/// <summary>
/// Reads the reference tables and the parameters into the generator input.
/// </summary>
public sealed class WorldReferenceRepository
{
    private readonly WorldDbContext _dbContext;
    private readonly IGameConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldReferenceRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="config">The game configuration.</param>
    public WorldReferenceRepository(WorldDbContext dbContext, IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(config);

        _dbContext = dbContext;
        _config = config;
    }

    /// <summary>
    /// Gets the parameters of the world.
    /// </summary>
    public WorldSettings Settings => WorldSettings.From(_config);

    /// <summary>
    /// Reads the generator input as it is now.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The input.</returns>
    public async Task<WorldGenerationConfig> LoadAsync(CancellationToken cancellationToken)
    {
        List<ReferenceResourceGroup> groups = await _dbContext.ResourceGroups.AsNoTracking()
            .Select(group => new ReferenceResourceGroup(group.Id, group.Code, group.AllowedOnPlanet))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        List<ReferenceResource> resources = await _dbContext.ResourceTypes.AsNoTracking()
            .Select(resource => new ReferenceResource(resource.Id, resource.Code, resource.ResourceGroupId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        List<ReferenceObjectType> types = await _dbContext.ObjectTypes.AsNoTracking()
            .Select(type => new ReferenceObjectType(
                type.Id,
                type.Code,
                type.ObjectClass,
                type.IsColonizable,
                type.GenerationWeight,
                type.MassMin,
                type.MassMax))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        List<ReferenceSizeClass> sizes = await _dbContext.SizeClasses.AsNoTracking()
            .Select(size => new ReferenceSizeClass(size.Id, size.Code, size.GenerationWeight, size.SizeMin, size.SizeMax))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var ruleGroups = await _dbContext.RuleGroups.AsNoTracking()
            .Include(group => group.RuleIds)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        WorldReferenceData reference = new(
            groups,
            resources,
            types,
            sizes,
            ruleGroups.Select(group => new ReferenceRuleGroup(
                group.Id,
                group.ObjectTypeId,
                group.Code,
                group.MinPicks,
                group.MaxPicks,
                group.ExtraPickProbability,
                [
                    .. group.RuleIds.Select(rule => new ReferenceRule(
                        rule.Id,
                        rule.ResourceId,
                        rule.Weight,
                        rule.CoefficientMin,
                        rule.CoefficientMax)),
                ])));

        return new WorldGenerationConfig(Settings, reference);
    }
}
