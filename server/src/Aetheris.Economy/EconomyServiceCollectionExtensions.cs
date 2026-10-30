using Aetheris.Core.Data;
using Aetheris.Economy.Api;
using Aetheris.Economy.Data;
using Aetheris.Economy.Services;
using Aetheris.World.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Economy;

/// <summary>
/// The registration of the module of the economy and its endpoints (task 00015).
/// </summary>
public static class EconomyServiceCollectionExtensions
{
    /// <summary>
    /// Adds the module of the economy to the services of the server.
    /// </summary>
    /// <param name="services">The services of the server.</param>
    /// <returns>The same services.</returns>
    public static IServiceCollection AddEconomyModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<EconomyDbContext>((serviceProvider, options) =>
            EconomyDatabase.Configure(options, serviceProvider.GetRequiredService<IDatabaseSession>().Connection));

        services.AddScoped<EconomyUnitOfWork>();
        services.AddScoped<ColonyRegistry>();
        services.AddScoped<IColonyRegistry>(serviceProvider => serviceProvider.GetRequiredService<ColonyRegistry>());
        services.AddScoped<EconomyService>();
        services.AddScoped<CostSplitter>();

        // The world asks before the scope of a resource changes whether a balance names it.
        services.AddScoped<IResourceUsage, EconomyResourceUsage>();
        services.AddScoped<IEconomyModule, EconomyModule>();

        return services;
    }

    /// <summary>
    /// Maps the endpoints of the module.
    /// </summary>
    /// <param name="endpoints">The endpoints of the server.</param>
    /// <returns>The same endpoints.</returns>
    public static IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints) =>
        EconomyEndpoints.Map(endpoints);

    /// <summary>
    /// Maps the endpoints of the admin panel of the module.
    /// </summary>
    /// <param name="secured">The secured group of the admin panel.</param>
    /// <returns>The same group.</returns>
    public static RouteGroupBuilder MapAdminEndpoints(RouteGroupBuilder secured) =>
        EconomyAdminEndpoints.Map(secured);
}
