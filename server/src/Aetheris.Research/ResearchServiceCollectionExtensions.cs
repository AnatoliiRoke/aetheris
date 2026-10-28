using Aetheris.Core.Commands;
using Aetheris.Core.Data;
using Aetheris.Core.Scheduling;
using Aetheris.Planets.Services;
using Aetheris.Research.Api;
using Aetheris.Research.Data;
using Aetheris.Research.Events;
using Aetheris.Research.Services;
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

        services.AddCommand<EnqueueResearchCommand>();
        services.AddCommand<CancelResearchCommand>();

        // The technologies of a player give the bodies module their levels and their bonuses,
        // so the research module replaces the empty stand-in of the bodies module.
        services.RemoveAll<ITechnologyLevels>();
        services.AddScoped<ITechnologyLevels, TechnologyLevelsService>();

        return services;
    }

    /// <summary>
    /// Maps the reads of the module.
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints) =>
        ResearchEndpoints.Map(endpoints);
}
