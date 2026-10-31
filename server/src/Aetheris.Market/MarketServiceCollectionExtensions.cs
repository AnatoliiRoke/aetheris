using Aetheris.Core.Admin;
using Aetheris.Core.Data;
using Aetheris.Market.Data;
using Aetheris.Market.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Market;

/// <summary>
/// The registration of the market module and its endpoints (task 00024).
/// </summary>
public static class MarketServiceCollectionExtensions
{
    /// <summary>
    /// Adds the market module to the services of the server: its database context on the
    /// connection of the request, its services and its reference kinds in the admin panel.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    /// <remarks>
    /// The host provides <see cref="IDatabaseSession"/>, the counters of the neutral codes and
    /// the texts of the game data.
    /// </remarks>
    public static IServiceCollection AddMarketModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<MarketDbContext>((serviceProvider, options) =>
            MarketDatabase.Configure(options, serviceProvider.GetRequiredService<IDatabaseSession>().Connection));

        services.AddScoped<MarketUnitOfWork>();
        services.AddScoped<MarketBootstrapService>();

        // The module keeps tables of its own, so it works on the context of the request, which
        // lives in the scope.
        services.AddScoped<IMarketModule, MarketModule>();

        // The kinds of the module are registered as the contract of the admin panel, so the
        // menu of the reference kinds lists them without a change of the host.
        services.AddScoped<MarketReferenceAdminService>();
        services.AddScoped<IAdminReferenceService>(serviceProvider => serviceProvider.GetRequiredService<MarketReferenceAdminService>());

        return services;
    }

    /// <summary>
    /// Maps the endpoints of the module.
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints) =>
        MarketEndpoints.Map(endpoints);

    /// <summary>
    /// Maps the reference kinds of the module on the secured admin group of the host
    /// (task 00023): the levels of the market and the reference prices of the items.
    /// </summary>
    /// <param name="secured">The secured admin group.</param>
    /// <returns>The same group.</returns>
    public static RouteGroupBuilder MapAdminEndpoints(RouteGroupBuilder secured) =>
        secured
            .MapAdminReference<MarketReferenceAdminService>(MarketReferenceAdminService.MarketLevelDescriptor)
            .MapAdminReference<MarketReferenceAdminService>(MarketReferenceAdminService.ReferencePriceDescriptor);
}
