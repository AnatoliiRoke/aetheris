using Aetheris.Core.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Core.Commands;

/// <summary>
/// A command type registered with the host, together with its request and response types.
/// </summary>
/// <param name="CommandType">The command class.</param>
/// <param name="RequestType">The request body type.</param>
/// <param name="ResponseType">The response body type.</param>
public sealed record CommandRegistration(Type CommandType, Type RequestType, Type ResponseType);

/// <summary>
/// The registration lines of the protection mechanisms.
/// </summary>
public static class CommandServiceCollectionExtensions
{
    /// <summary>
    /// Registers a command. The host publishes it as an endpoint with every protection of
    /// the command pipeline.
    /// </summary>
    /// <typeparam name="TCommand">The command class, derived from <see cref="Command{TRequest, TResponse}"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    public static IServiceCollection AddCommand<TCommand>(this IServiceCollection services)
        where TCommand : class, ICommandDefinition
    {
        ArgumentNullException.ThrowIfNull(services);

        Type? baseType = typeof(TCommand).BaseType;

        while (baseType is not null
            && !(baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(Command<,>)))
        {
            baseType = baseType.BaseType;
        }

        if (baseType is null)
        {
            throw new InvalidOperationException(
                $"{typeof(TCommand).Name} must derive from {typeof(Command<,>).Name}.");
        }

        Type[] arguments = baseType.GetGenericArguments();

        services.AddScoped<TCommand>();
        services.AddSingleton(new CommandRegistration(typeof(TCommand), arguments[0], arguments[^1]));

        return services;
    }

    /// <summary>
    /// Registers the ownership resolver of an object type.
    /// </summary>
    /// <typeparam name="TResolver">The resolver.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    public static IServiceCollection AddOwnedObjectType<TResolver>(this IServiceCollection services)
        where TResolver : class, IOwnedObjectResolver
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IOwnedObjectResolver, TResolver>();

        return services;
    }
}
