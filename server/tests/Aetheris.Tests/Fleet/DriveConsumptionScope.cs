using Aetheris.Production.Api;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Fleet;

/// <summary>
/// Sets the consumption of every drive of the catalog for the time of a test and puts the old
/// values back after it (task 00072).
/// </summary>
/// <remarks>
/// The consumption of a drive is a characteristic of the drive in the data, so a test that checks
/// the flight and not its cost sets the consumption of the drives to zero through the same data:
/// the check of the route before the fleet leaves then needs nothing, and the times of the flights
/// stay the times of the formula, with no cargo of the propellant in the holds.
/// </remarks>
internal sealed class DriveConsumptionScope : IAsyncDisposable
{
    private readonly IServiceProvider _services;
    private readonly Dictionary<(int PartTypeId, int CharacteristicId), decimal> _saved;
    private readonly decimal _amount;

    private DriveConsumptionScope(
        IServiceProvider services,
        Dictionary<(int PartTypeId, int CharacteristicId), decimal> saved,
        decimal amount)
    {
        _services = services;
        _saved = saved;
        _amount = amount;
    }

    /// <summary>
    /// Sets the consumption of every drive to an amount.
    /// </summary>
    /// <param name="services">The services of the test server.</param>
    /// <param name="amount">The consumption of every drive, in units a second.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The scope that puts the old values back when it is disposed.</returns>
    public static async Task<DriveConsumptionScope> SetAsync(
        IServiceProvider services,
        decimal amount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        using IServiceScope scope = services.CreateScope();
        ProductionDbContext database = scope.ServiceProvider.GetRequiredService<ProductionDbContext>();

        List<PartCharacteristicValueEntity> rows = await RowsAsync(database, cancellationToken);
        Dictionary<(int PartTypeId, int CharacteristicId), decimal> saved = rows.ToDictionary(
            row => (row.PartTypeId, row.CharacteristicId),
            row => row.NumberValue);
        DriveConsumptionScope created = new(services, saved, amount);

        await created.ApplyAsync(cancellationToken);

        return created;
    }

    /// <summary>
    /// Sets the consumption of every drive to the amount of the scope again, after a step of a test
    /// wrote the starting data of the catalog anew.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the drives are set.</returns>
    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _services.CreateScope();
        ProductionDbContext database = scope.ServiceProvider.GetRequiredService<ProductionDbContext>();

        foreach (PartCharacteristicValueEntity row in await RowsAsync(database, cancellationToken))
        {
            row.NumberValue = _amount;
        }

        await database.SaveChangesAsync(cancellationToken);
        scope.ServiceProvider.GetRequiredService<ProductionCatalogCache>().Invalidate();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        using IServiceScope scope = _services.CreateScope();
        ProductionDbContext database = scope.ServiceProvider.GetRequiredService<ProductionDbContext>();

        foreach (PartCharacteristicValueEntity row in await RowsAsync(database, CancellationToken.None))
        {
            if (_saved.TryGetValue((row.PartTypeId, row.CharacteristicId), out decimal amount))
            {
                row.NumberValue = amount;
            }
        }

        await database.SaveChangesAsync(CancellationToken.None);
        scope.ServiceProvider.GetRequiredService<ProductionCatalogCache>().Invalidate();
    }

    private static Task<List<PartCharacteristicValueEntity>> RowsAsync(
        ProductionDbContext database,
        CancellationToken cancellationToken) =>
        database.PartCharacteristicValues
            .Where(row => database.Characteristics.Any(characteristic => characteristic.Id == row.CharacteristicId
                && characteristic.Kind == CharacteristicKinds.FuelUse))
            .ToListAsync(cancellationToken);
}
