using Aetheris.Core.Admin;
using Aetheris.Core.Commands;
using Aetheris.Core.Data;
using Aetheris.Core.Scheduling;
using Aetheris.Economy.Services;
using Aetheris.Planets.Services;
using Aetheris.Research.Api;
using Aetheris.Research.Data;
using Aetheris.Research.Events;
using Aetheris.Research.Services;
using Aetheris.World.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aetheris.Research;

/// <summary>
/// The registration of the research module.
/// </summary>
public static class ResearchServiceCollectionExtensions
{
    /// <summary>
    /// Registers the research module: its database context on the connection of the request,
    /// its services, its event handler and the technologies of the players, which the bodies
    /// module reads.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    /// <remarks>
    /// The host provides <see cref="IDatabaseSession"/>, <see cref="Core.Scheduling.IEventScheduler"/>,
    /// the game configuration and the game clock.
    /// </remarks>
    public static IServiceCollection AddResearchModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<ResearchDbContext>((serviceProvider, options) =>
            ResearchDatabase.Configure(options, serviceProvider.GetRequiredService<IDatabaseSession>().Connection));

        services.AddSingleton<TechnologyCatalogCache>();
        services.AddScoped<ResearchUnitOfWork>();
        services.AddScoped<ResearchBootstrapService>();
        services.AddScoped<ResearchQueueService>();
        services.AddScoped<ResearchTreeBuilder>();
        services.AddScoped<IResearchModule, ResearchModule>();
        services.AddScoped<IScheduledEventHandler, ResearchCompleteHandler>();
        services.AddScoped<ResearchReferenceAdminService>();
        services.AddScoped<IAdminReferenceService>(serviceProvider => serviceProvider.GetRequiredService<ResearchReferenceAdminService>());

        services.AddCommand<EnqueueResearchCommand>();
        services.AddCommand<CancelResearchCommand>();

        // The technologies of a player give the bodies module their levels and their bonuses,
        // so the research module replaces the empty stand-in of the bodies module.
        services.RemoveAll<ITechnologyLevels>();
        services.AddScoped<ITechnologyLevels, TechnologyLevelsService>();

        // The economy module asks for the bonuses the technologies give the population and the
        // credits; the contract is declared by the economy module and implemented here
        // (task 00015).
        services.AddScoped<IEconomyTechnologyBonuses, EconomyTechnologyBonuses>();
        services.AddScoped<IResourceUsage, ResearchResourceUsage>();

        // The multipliers of the production are a rule of the research, so the production
        // module reads them through this contract (task 00013, decision 14).
        services.AddScoped<IProductionMultipliers, ProductionMultipliersService>();

        return services;
    }

    /// <summary>
    /// Maps the reads of the module.
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints) =>
        ResearchEndpoints.Map(endpoints);

    /// <summary>
    /// Maps the reference kinds of the module on the secured admin group of the host
    /// (task 00023): the technologies and the branches of the tree.
    /// </summary>
    /// <param name="secured">The secured admin group.</param>
    /// <returns>The same group.</returns>
    public static RouteGroupBuilder MapAdminEndpoints(RouteGroupBuilder secured) =>
        secured
            .MapAdminReference<ResearchReferenceAdminService>(ResearchReferenceAdminService.TechnologyDescriptor)
            .MapAdminReference<ResearchReferenceAdminService>(ResearchReferenceAdminService.BranchDescriptor);
}
