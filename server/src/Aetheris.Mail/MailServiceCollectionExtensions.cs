using Aetheris.Core.Commands;
using Aetheris.Core.Data;
using Aetheris.Core.Scheduling;
using Aetheris.Mail.Api;
using Aetheris.Mail.Data;
using Aetheris.Mail.Events;
using Aetheris.Mail.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Mail;

/// <summary>
/// The registration of the mail module and its endpoints (task 00060).
/// </summary>
public static class MailServiceCollectionExtensions
{
    /// <summary>
    /// Adds the mail module to the services of the server: its database context on the
    /// connection of the request and its services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    /// <remarks>
    /// The host provides <see cref="IDatabaseSession"/>, the texts of the game data and the
    /// nicknames of the players.
    /// </remarks>
    public static IServiceCollection AddMailModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<MailDbContext>((serviceProvider, options) =>
            MailDatabase.Configure(options, serviceProvider.GetRequiredService<IDatabaseSession>().Connection));

        services.AddScoped<MailService>();
        services.AddScoped<MailSendService>();
        services.AddScoped<MailBootstrapService>();
        services.AddScoped<MailUnitOfWork>();
        services.AddScoped<IScheduledEventHandler, MailTrashCleanupHandler>();

        services.AddCommand<SendMailCommand>();
        services.AddCommand<SaveMailDraftCommand>();
        services.AddCommand<RemoveMailDraftCommand>();
        services.AddCommand<ReadAllMailCommand>();
        services.AddCommand<ToggleMailSavedCommand>();
        services.AddCommand<ToggleMailDeletedCommand>();
        services.AddCommand<EmptyMailTrashCommand>();
        services.AddCommand<ComplainAboutMailCommand>();

        // The module keeps tables of its own, so it works on the context of the request, which
        // lives in the scope.
        services.AddScoped<IMailModule, MailModule>();

        return services;
    }

    /// <summary>
    /// Maps the endpoints of the module.
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints) =>
        MailEndpoints.Map(endpoints);
}
