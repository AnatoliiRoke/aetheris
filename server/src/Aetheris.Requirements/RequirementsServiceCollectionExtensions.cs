using Aetheris.Core.Data;
using Aetheris.Requirements.Data;
using Aetheris.Requirements.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Requirements;

/// <summary>
/// The registration of the common requirements module.
/// </summary>
public static class RequirementsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the common requirements module: its database context on the connection of the
    /// request, its services and its public contract.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    /// <remarks>
    /// The host provides <see cref="IDatabaseSession"/>, so the context of the module joins the
    /// transaction of the request like the context of every other module.
    /// </remarks>
    public static IServiceCollection AddRequirementsModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<RequirementsDbContext>((serviceProvider, options) =>
            RequirementsDatabase.Configure(options, serviceProvider.GetRequiredService<IDatabaseSession>().Connection));

        services.AddScoped<RequirementsUnitOfWork>();
        services.AddScoped<RequirementsService>();
        services.AddScoped<IRequirementsModule, RequirementsModule>();

        return services;
    }
}
