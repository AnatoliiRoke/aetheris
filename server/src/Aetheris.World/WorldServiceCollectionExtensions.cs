using Aetheris.Core.Commands;
using Aetheris.Core.Data;
using Aetheris.Core.Scheduling;
using Aetheris.Core.Security;
using Aetheris.World.Api;
using Aetheris.World.Data;
using Aetheris.World.Events;
using Aetheris.World.Services;
using Aetheris.World.Visibility;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.World;

/// <summary>
/// The registration of the World module.
/// </summary>
public static class WorldServiceCollectionExtensions
{
    /// <summary>
    /// Registers the World module: its database context on the connection of the request, its
    /// services, its command, its event handler and its owned object type.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    /// <remarks>
    /// The host provides <see cref="IDatabaseSession"/>, <see cref="IEventScheduler"/>,
    /// <see cref="Core.Text.ITextModeration"/>, the game configuration and the game clock.
    /// </remarks>
    public static IServiceCollection AddWorldModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<WorldDbContext>((serviceProvider, options) =>
            WorldDatabase.Configure(options, serviceProvider.GetRequiredService<IDatabaseSession>().Connection));

        services.AddScoped<WorldUnitOfWork>();
        services.AddScoped<WorldReferenceRepository>();
        services.AddScoped<WorldGenerationService>();
        services.AddScoped<StartPlanetAllocator>();
        services.AddScoped<CivilizationService>();
        services.AddScoped<IStartBuildingPlacer, PendingStartBuildingPlacer>();
        services.AddScoped<FieldService>();
        services.AddScoped<WorldQueryService>();
        services.AddScoped<IWorldVisibility, OwnBodiesVisibility>();
        services.AddScoped<IWorldModule, WorldModule>();
        services.AddScoped<IScheduledEventHandler, FieldRespawnHandler>();

        services.AddCommand<FoundCivilizationCommand>();
        services.AddOwnedObjectType<PlanetOwnerResolver>();

        return services;
    }
}

/// <summary>
/// Tells who owns a body, for the commands and subscriptions that target one.
/// </summary>
public sealed class PlanetOwnerResolver : IOwnedObjectResolver
{
    /// <summary>
    /// The object type of a body.
    /// </summary>
    public const string Type = "planet";

    private readonly WorldQueryService _queries;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanetOwnerResolver"/> class.
    /// </summary>
    /// <param name="queries">The read service.</param>
    public PlanetOwnerResolver(WorldQueryService queries)
    {
        ArgumentNullException.ThrowIfNull(queries);

        _queries = queries;
    }

    /// <inheritdoc />
    public string ObjectType => Type;

    /// <inheritdoc />
    public Task<string?> GetOwnerIdAsync(string objectId, CancellationToken cancellationToken) =>
        long.TryParse(objectId, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long planetId)
            ? _queries.GetPlanetOwnerAsync(planetId, cancellationToken)
            : Task.FromResult<string?>(null);
}
