using Aetheris.Core.Admin;
using Aetheris.Core.Commands;
using Aetheris.Core.Data;
using Aetheris.Core.Scheduling;
using Aetheris.Production.Api;
using Aetheris.Production.Data;
using Aetheris.Production.Events;
using Aetheris.Production.Services;
using Aetheris.Research.Services;
using Aetheris.World.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Production;

/// <summary>
/// The registration of the production module.
/// </summary>
public static class ProductionServiceCollectionExtensions
{
    /// <summary>
    /// Registers the production module: its database context on the connection of the
    /// request, its services, its event handler and its reference kinds in the admin panel.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    /// <remarks>
    /// The host provides <see cref="IDatabaseSession"/>, <see cref="Core.Scheduling.IEventScheduler"/>,
    /// the game configuration and the game clock.
    /// </remarks>
    public static IServiceCollection AddProductionModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<ProductionDbContext>((serviceProvider, options) =>
            ProductionDatabase.Configure(options, serviceProvider.GetRequiredService<IDatabaseSession>().Connection));

        services.AddSingleton<ProductionCatalogCache>();
        services.AddScoped<ProductionUnitOfWork>();
        services.AddScoped<ProductionBootstrapService>();
        services.AddScoped<ProductionQueueService>();
        services.AddScoped<PartStockService>();
        services.AddScoped<IResourceUsage, ProductionResourceUsage>();
        services.AddScoped<IProductionModule, ProductionModule>();
        services.AddScoped<IScheduledEventHandler, ProductionUnitReadyHandler>();
        services.AddScoped<ProductionReferenceAdminService>();
        services.AddScoped<IAdminReferenceService>(serviceProvider => serviceProvider.GetRequiredService<ProductionReferenceAdminService>());

        // A technology may open a recipe with its effect «opens» (task 00023, decision 7).
        services.AddScoped<ITechnologyUnlockTargets, ProductionUnlockTargets>();

        services.AddCommand<EnqueueProductionCommand>();
        services.AddCommand<CancelProductionCommand>();

        return services;
    }

    /// <summary>
    /// Maps the reads of the module.
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints) =>
        ProductionEndpoints.Map(endpoints);

    /// <summary>
    /// Maps the reference kinds of the module on the secured admin group of the host
    /// (task 00023): the groups of the parts, the parts and the recipes.
    /// </summary>
    /// <param name="secured">The secured admin group.</param>
    /// <returns>The same group.</returns>
    public static RouteGroupBuilder MapAdminEndpoints(RouteGroupBuilder secured) =>
        secured
            .MapAdminReference<ProductionReferenceAdminService>(ProductionReferenceAdminService.PartGroupDescriptor)
            .MapAdminReference<ProductionReferenceAdminService>(ProductionReferenceAdminService.PartDescriptor)
            .MapAdminReference<ProductionReferenceAdminService>(ProductionReferenceAdminService.RecipeDescriptor);
}
