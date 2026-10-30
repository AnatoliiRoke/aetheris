using Aetheris.Core.Admin;
using Aetheris.Core.Commands;
using Aetheris.Core.Data;
using Aetheris.Core.Scheduling;
using Aetheris.Processes.Api;
using Aetheris.Processes.Data;
using Aetheris.Processes.Events;
using Aetheris.Processes.Handlers;
using Aetheris.Processes.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aetheris.Processes;

/// <summary>
/// The registration of the module of the processes and its endpoints (task 00014).
/// </summary>
public static class ProcessesServiceCollectionExtensions
{
    /// <summary>
    /// Adds the module of the processes to the services of the server.
    /// </summary>
    /// <param name="services">The services of the server.</param>
    /// <returns>The same services.</returns>
    public static IServiceCollection AddProcessesModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<ProcessesDbContext>((serviceProvider, options) =>
            ProcessesDatabase.Configure(options, serviceProvider.GetRequiredService<IDatabaseSession>().Connection));

        services.AddSingleton<ProcessCatalogCache>();

        // The ships on the orbit are a stub until the task of the flights (task 00014,
        // decision 5); that task replaces the registration with the real ships.
        services.TryAddSingleton<ShipsOnOrbitStub>();
        services.TryAddSingleton<IShipsOnOrbit>(serviceProvider => serviceProvider.GetRequiredService<ShipsOnOrbitStub>());
        services.AddScoped<ProcessesUnitOfWork>();
        services.AddScoped<ProcessesBootstrapService>();
        services.AddScoped<ProcessQueueService>();
        services.AddScoped<IProcessesModule, ProcessesModule>();
        services.AddScoped<IScheduledEventHandler, ProcessReadyHandler>();
        services.AddScoped<ProcessReferenceAdminService>();
        services.AddScoped<IAdminReferenceService>(serviceProvider => serviceProvider.GetRequiredService<ProcessReferenceAdminService>());

        // The handlers of the kinds of the processes: a new process is a class and a row, so
        // the engine finds the handler by its logical code (task 00014, decision 6).
        services.AddScoped<IProcessHandler, RecyclingHandler>();
        services.AddScoped<IProcessHandler, MatterConversionHandler>();
        services.AddScoped<IProcessHandler, ShipRepairHandler>();
        services.AddScoped<IProcessHandler, ModuleReplacementHandler>();
        services.AddScoped<IProcessHandler, ModuleRepairHandler>();

        // The terraforming is started by a ship and is no process of the queue of a building
        // (task 00014, section 9), so it is a service of its own and not a handler.
        services.AddScoped<TerraformingHandler>();

        services.AddCommand<EnqueueProcessCommand>();
        services.AddCommand<CancelProcessCommand>();
        services.AddCommand<TerraformCommand>();

        return services;
    }

    /// <summary>
    /// Maps the endpoints of the module.
    /// </summary>
    /// <param name="endpoints">The endpoints of the server.</param>
    /// <returns>The same endpoints.</returns>
    public static IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints) =>
        ProcessesEndpoints.Map(endpoints);

    /// <summary>
    /// Maps the endpoints of the reference kinds of the module in the admin panel.
    /// </summary>
    /// <param name="secured">The secured group of the admin panel.</param>
    /// <returns>The same group.</returns>
    public static RouteGroupBuilder MapAdminEndpoints(RouteGroupBuilder secured) =>
        secured
            .MapAdminReference<ProcessReferenceAdminService>(ProcessReferenceAdminService.ProcessTypeDescriptor)
            .MapAdminReference<ProcessReferenceAdminService>(ProcessReferenceAdminService.ProcessRecipeDescriptor)
            .MapAdminReference<ProcessReferenceAdminService>(ProcessReferenceAdminService.ProcessParamDescriptor);
}
