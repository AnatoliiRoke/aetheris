using Aetheris.Core.Configuration;
using Aetheris.Host.Configuration;
using Aetheris.Host.Data.Entities;
using Aetheris.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Configuration;

/// <summary>
/// Tests that the game configuration is read from the database and that a change in the
/// database changes the result without rebuilding the code.
/// </summary>
/// <remarks>
/// This is acceptance criterion 4 of task 00001.
/// </remarks>
public sealed class ConfigReloadTests : IClassFixture<AetherisTestFactory>
{
    private const string ParameterKey = "test.hello_multiplier";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigReloadTests"/> class.
    /// </summary>
    /// <param name="factory">The test host factory.</param>
    public ConfigReloadTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    /// <summary>
    /// A value written to the database changes what the configuration returns, without any
    /// code change or rebuild.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Changing_a_parameter_in_the_database_changes_the_result()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

        await SetParameterAsync(dbContext, "7").ConfigureAwait(false);

        ConfigReloadService reloadService = scope.ServiceProvider.GetRequiredService<ConfigReloadService>();
        await reloadService.ReloadAsync().ConfigureAwait(false);

        IGameConfig config = scope.ServiceProvider.GetRequiredService<IGameConfig>();

        Assert.Equal(7, config.GetInt64(ParameterKey));

        await SetParameterAsync(dbContext, "9").ConfigureAwait(false);

        await reloadService.ReloadAsync().ConfigureAwait(false);

        Assert.Equal(9, config.GetInt64(ParameterKey));
    }

    /// <summary>
    /// The in-memory configuration returns the default value for an unknown key.
    /// </summary>
    [Fact]
    public void Unknown_key_returns_the_default_value()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IGameConfig config = scope.ServiceProvider.GetRequiredService<IGameConfig>();

        Assert.Equal(42, config.GetInt64("test.key_that_does_not_exist", 42));
        Assert.True(config.GetBoolean("test.key_that_does_not_exist", true));
    }

    /// <summary>
    /// Writing a parameter to the database and reading it back through the store returns the
    /// same value.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_store_returns_the_value_written_to_the_database()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

        await SetParameterAsync(dbContext, "3").ConfigureAwait(false);

        IConfigParameterStore store = scope.ServiceProvider.GetRequiredService<IConfigParameterStore>();
        IReadOnlyList<KeyValuePair<string, ConfigParameter>> values =
            await store.ReadAllAsync().ConfigureAwait(false);

        ConfigParameter? parameter = values
            .Where(entry => entry.Key == ParameterKey)
            .Select(entry => entry.Value)
            .FirstOrDefault();

        Assert.NotNull(parameter);
        Assert.Equal("3", parameter.Value);
    }

    private static async Task SetParameterAsync(AetherisDbContext dbContext, string value)
    {
        ConfigParameterEntity? existing = await dbContext.ConfigParameters
            .FirstOrDefaultAsync(parameter => parameter.Key == ParameterKey)
            .ConfigureAwait(false);

        if (existing is null)
        {
            dbContext.ConfigParameters.Add(new ConfigParameterEntity
            {
                Key = ParameterKey,
                Value = value,
                Version = 1,
                UpdatedAt = DateTimeOffset.UtcNow,
                UpdatedBy = "test",
            });
        }
        else
        {
            existing.Value = value;
            existing.Version++;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync().ConfigureAwait(false);
    }
}
