using Aetheris.Core.Commands;
using Aetheris.Core.Data;
using Aetheris.Core.Scheduling;
using Aetheris.Fleet.Api;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Events;
using Aetheris.Fleet.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Fleet;

/// <summary>
/// The registration of the fleet module and its endpoints (task 00038).
/// </summary>
public static class FleetServiceCollectionExtensions
{
    /// <summary>
    /// Adds the fleet module to the services of the server: its database context on the
    /// connection of the request and its services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    /// <remarks>
    /// The host provides <see cref="IDatabaseSession"/>, the counters of the neutral codes and
    /// the texts of the game data.
    /// </remarks>
    public static IServiceCollection AddFleetModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<FleetDbContext>((serviceProvider, options) =>
            FleetDatabase.Configure(options, serviceProvider.GetRequiredService<IDatabaseSession>().Connection));

        services.AddScoped<FleetUnitOfWork>();
        services.AddScoped<ShipDesignService>();
        services.AddScoped<ShipyardQueueService>();
        services.AddScoped<FleetFormationService>();
        services.AddScoped<FleetBootstrapService>();
        services.AddScoped<IScheduledEventHandler, ShipyardCompleteHandler>();

        services.AddCommand<CreateShipDesignCommand>();
        services.AddCommand<RenameShipDesignCommand>();
        services.AddCommand<SaveShipDesignCommand>();
        services.AddCommand<ArchiveShipDesignCommand>();
        services.AddCommand<EnqueueShipyardCommand>();
        services.AddCommand<CancelShipyardCommand>();
        services.AddCommand<DisassembleShipCommand>();
        services.AddCommand<CreateFleetCommand>();
        services.AddCommand<AddFleetShipsCommand>();
        services.AddCommand<RemoveFleetShipsCommand>();
        services.AddCommand<TransferFleetShipsCommand>();
        services.AddCommand<RenameFleetCommand>();
        services.AddCommand<DissolveFleetCommand>();

        // The module keeps tables of its own, so it works on the context of the request, which
        // lives in the scope.
        services.AddScoped<IFleetModule, FleetModule>();

        return services;
    }

    /// <summary>
    /// Maps the endpoints of the module.
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints) =>
        FleetEndpoints.Map(endpoints);

    /// <summary>
    /// Maps the ship projects of a player on the secured admin group.
    /// </summary>
    /// <param name="secured">The secured admin group.</param>
    /// <returns>The same group.</returns>
    public static RouteGroupBuilder MapAdminEndpoints(RouteGroupBuilder secured) =>
        FleetPlayerAdminEndpoints.Map(secured);
}
