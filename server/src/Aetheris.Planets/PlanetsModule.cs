using Aetheris.Core.Commands;
using Aetheris.Core.Configuration;
using Aetheris.Core.Data;
using Aetheris.Core.Scheduling;
using Aetheris.Planets.Api;
using Aetheris.Planets.Configuration;
using Aetheris.Planets.Data;
using Aetheris.Planets.Events;
using Aetheris.Planets.Services;
using Aetheris.World.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aetheris.Planets;

/// <summary>
/// The implementation of <see cref="IPlanetsModule"/> and the registration of the module.
/// </summary>
/// <remarks>
/// The host calls the static members: <see cref="Register"/>, <see cref="MapEndpoints"/>,
/// <see cref="MigrateAsync"/> and <see cref="ConfigDefinitions"/>.
/// </remarks>
public sealed class PlanetsModule : IPlanetsModule
{
    private readonly PlanetsBootstrapService _bootstrap;
    private readonly PlanetQueryService _queries;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanetsModule"/> class.
    /// </summary>
    /// <param name="bootstrap">The start of the module.</param>
    /// <param name="queries">The reads of a body.</param>
    public PlanetsModule(PlanetsBootstrapService bootstrap, PlanetQueryService queries)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(queries);

        _bootstrap = bootstrap;
        _queries = queries;
    }

    /// <summary>
    /// Gets the parameters of the module, added to the seed of the host.
    /// </summary>
    public static IReadOnlyList<ConfigParameterDefinition> ConfigDefinitions => PlanetsConfigKeys.Definitions;

    /// <inheritdoc />
    public string ModuleName => "Planets";

    /// <summary>
    /// Registers the module: its context on the connection of the request, its services, its
    /// commands, its event handler and its implementations of the extension points of the world.
    /// </summary>
    /// <param name="services">The service collection; the world module is registered before.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<PlanetsDbContext>((serviceProvider, options) =>
            PlanetsDatabase.Configure(options, serviceProvider.GetRequiredService<IDatabaseSession>().Connection));

        services.AddSingleton<BuildingCatalogCache>();
        services.AddScoped<PlanetsUnitOfWork>();
        services.AddScoped<ConstructionService>();
        services.AddScoped<PlanetQueryService>();
        services.AddScoped<PlanetsBootstrapService>();
        services.AddScoped<PlanetStartPackage>();
        services.TryAddScoped<ITechnologyLevels, NoTechnologyLevels>();
        services.AddScoped<IPlanetsModule, PlanetsModule>();
        services.AddScoped<IScheduledEventHandler, ConstructionCompleteHandler>();

        // The start package of a new civilization lands in this module instead of the old
        // storage of the world.
        services.RemoveAll<IHomePlanetStore>();
        services.RemoveAll<IStartBuildingPlacer>();
        services.AddScoped<IHomePlanetStore>(serviceProvider => serviceProvider.GetRequiredService<PlanetStartPackage>());
        services.AddScoped<IStartBuildingPlacer>(serviceProvider => serviceProvider.GetRequiredService<PlanetStartPackage>());

        services.AddCommand<EnqueueConstructionCommand>();
        services.AddCommand<CancelConstructionCommand>();

        return services;
    }

    /// <summary>
    /// Maps the reads of the module.
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints) => PlanetsEndpoints.Map(endpoints);

    /// <summary>
    /// Applies the migrations of the module.
    /// </summary>
    /// <param name="connectionString">The connection string of an account that may change the schema.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the schema is up to date.</returns>
    public static Task MigrateAsync(string connectionString, CancellationToken cancellationToken = default) =>
        PlanetsDatabase.MigrateAsync(connectionString, cancellationToken);

    /// <inheritdoc />
    public Task<PlanetsBootstrapResult> EnsureAsync(CancellationToken cancellationToken = default) =>
        _bootstrap.EnsureAsync(cancellationToken);

    /// <inheritdoc />
    public Task<PlanetOverviewDto?> GetPlanetAsync(string playerId, long planetId, CancellationToken cancellationToken = default) =>
        _queries.GetAsync(playerId, planetId, cancellationToken);

    /// <inheritdoc />
    public Task<AvailabilityDto> CheckRequirementsAsync(
        string subjectType,
        int subjectId,
        int level,
        long planetId,
        string? playerId,
        CancellationToken cancellationToken = default) =>
        _queries.CheckAsync(subjectType, subjectId, level, planetId, playerId, cancellationToken);
}
