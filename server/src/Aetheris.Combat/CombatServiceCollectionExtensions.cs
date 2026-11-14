using Aetheris.Combat.Data;
using Aetheris.Combat.Services;
using Aetheris.Core.Admin;
using Aetheris.Core.Data;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Combat;

/// <summary>
/// The registration of the combat module (task 00062).
/// </summary>
public static class CombatServiceCollectionExtensions
{
    /// <summary>
    /// Adds the combat module: its context on the connection of the request and its services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddCombatModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<CombatDbContext>((serviceProvider, options) =>
            CombatDatabase.Configure(options, serviceProvider.GetRequiredService<IDatabaseSession>().Connection));

        services.AddScoped<CombatUnitOfWork>();
        services.AddScoped<CombatReferenceAdminService>();
        services.AddScoped<IAdminReferenceService>(serviceProvider => serviceProvider.GetRequiredService<CombatReferenceAdminService>());
        services.AddScoped<CombatBootstrapService>();

        // The module keeps tables of its own, so it works on the context of the request.
        services.AddScoped<ICombatModule, CombatModule>();

        return services;
    }

    /// <summary>
    /// Maps the player endpoints of the module. Task 00062 has none.
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints;
    }

    /// <summary>
    /// Maps the target rules and the behaviors on the secured admin group.
    /// </summary>
    /// <param name="secured">The secured admin group.</param>
    /// <returns>The same group.</returns>
    public static RouteGroupBuilder MapAdminEndpoints(RouteGroupBuilder secured)
    {
        ArgumentNullException.ThrowIfNull(secured);

        return secured
            .MapAdminReference<CombatReferenceAdminService>(CombatReferenceAdminService.TargetRuleDescriptor)
            .MapAdminReference<CombatReferenceAdminService>(CombatReferenceAdminService.BehaviorDescriptor);
    }
}
