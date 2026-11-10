using Aetheris.Core.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Production.Data;

/// <summary>
/// Tells the command <c>reconcile-seed-codes</c> which tables of the production module the
/// starting data fills and how the keys of their texts are built from the code (task 00052).
/// </summary>
public sealed class ProductionSeedCodeSource : ISeedCodeSource
{
    private static readonly string[] TextColumns = ["name_key", "description_key"];
    private static readonly string[] CharacteristicColumns = ["name_key", "description_key", "unit_key"];

    private readonly ProductionDbContext _dbContext;
    private readonly ProductionUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductionSeedCodeSource"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    public ProductionSeedCodeSource(ProductionDbContext dbContext, ProductionUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public IReadOnlyList<ISeedCodeTable> Tables
    {
        get
        {
            ProductionSeedData data = ProductionReferenceSeed.Load();

            return
            [
                new SeedCodeTable<PartGroupEntity>(
                    "part_group",
                    ProductionTextKeys.PartGroupCodeGroup,
                    [.. data.PartGroups.Select(group => group.Code)],
                    TextColumns,
                    code => [ProductionTextKeys.GroupName(code), ProductionTextKeys.GroupDescription(code)],
                    cancellationToken => _dbContext.PartGroups.OrderBy(row => row.Id).ToListAsync(cancellationToken),
                    _unitOfWork.RunAsync,
                    row => row.Id,
                    row => row.Code,
                    row => [row.NameKey, row.DescriptionKey],
                    (row, code, keys) =>
                    {
                        row.Code = code;
                        row.NameKey = keys[0] ?? row.NameKey;
                        row.DescriptionKey = keys[1] ?? row.DescriptionKey;
                    }),
                new SeedCodeTable<PartTypeEntity>(
                    "part_type",
                    ProductionTextKeys.PartTypeCodeGroup,
                    [.. data.Parts.Select(part => part.Code)],
                    TextColumns,
                    code => [ProductionTextKeys.PartName(code), ProductionTextKeys.PartDescription(code)],
                    cancellationToken => _dbContext.PartTypes.OrderBy(row => row.Id).ToListAsync(cancellationToken),
                    _unitOfWork.RunAsync,
                    row => row.Id,
                    row => row.Code,
                    row => [row.NameKey, row.DescriptionKey],
                    (row, code, keys) =>
                    {
                        row.Code = code;
                        row.NameKey = keys[0] ?? row.NameKey;
                        row.DescriptionKey = keys[1] ?? row.DescriptionKey;
                    }),
                new SeedCodeTable<RecipeEntity>(
                    "recipe",
                    ProductionTextKeys.RecipeCodeGroup,
                    [.. data.Recipes.Select(recipe => recipe.Code)],
                    TextColumns,
                    code => [ProductionTextKeys.RecipeName(code), ProductionTextKeys.RecipeDescription(code)],
                    cancellationToken => _dbContext.Recipes.OrderBy(row => row.Id).ToListAsync(cancellationToken),
                    _unitOfWork.RunAsync,
                    row => row.Id,
                    row => row.Code,
                    row => [row.NameKey, row.DescriptionKey],
                    (row, code, keys) =>
                    {
                        row.Code = code;
                        row.NameKey = keys[0] ?? row.NameKey;
                        row.DescriptionKey = keys[1] ?? row.DescriptionKey;
                    }),
                new SeedCodeTable<CharacteristicEntity>(
                    "characteristic",
                    ProductionTextKeys.CharacteristicCodeGroup,
                    [.. (data.Characteristics ?? []).Select(characteristic => characteristic.Code)],
                    CharacteristicColumns,
                    code => [ProductionTextKeys.CharacteristicName(code), ProductionTextKeys.CharacteristicDescription(code), ProductionTextKeys.CharacteristicUnit(code)],
                    cancellationToken => _dbContext.Characteristics.OrderBy(row => row.Id).ToListAsync(cancellationToken),
                    _unitOfWork.RunAsync,
                    row => row.Id,
                    row => row.Code,
                    row => [row.NameKey, row.DescriptionKey, row.UnitKey],
                    (row, code, keys) =>
                    {
                        row.Code = code;
                        row.NameKey = keys[0] ?? row.NameKey;
                        row.DescriptionKey = keys[1] ?? row.DescriptionKey;
                        row.UnitKey = keys[^1] ?? row.UnitKey;
                    }),
            ];
        }
    }
}
