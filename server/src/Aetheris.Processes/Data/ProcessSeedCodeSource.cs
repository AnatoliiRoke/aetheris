using Aetheris.Core.Data;
using Aetheris.Processes.Data.Entities;
using Aetheris.Processes.Services;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Processes.Data;

/// <summary>
/// Tells the command <c>reconcile-seed-codes</c> which tables of the processes module the
/// starting data fills and how the keys of their texts are built from the code (task 00052).
/// </summary>
public sealed class ProcessSeedCodeSource : ISeedCodeSource
{
    private static readonly string[] TextColumns = ["name_key", "description_key"];

    private readonly ProcessesDbContext _dbContext;
    private readonly ProcessesUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessSeedCodeSource"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    public ProcessSeedCodeSource(ProcessesDbContext dbContext, ProcessesUnitOfWork unitOfWork)
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
            ProcessSeedData data = ProcessReferenceSeed.Load();

            return
            [
                new SeedCodeTable<ProcessTypeEntity>(
                    "process_type",
                    ProcessTextKeys.ProcessTypeCodeGroup,
                    [.. data.Types.Select(type => type.Code)],
                    TextColumns,
                    code => [ProcessTextKeys.TypeName(code), ProcessTextKeys.TypeDescription(code)],
                    cancellationToken => _dbContext.ProcessTypes.OrderBy(row => row.Id).ToListAsync(cancellationToken),
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
                new SeedCodeTable<ProcessRecipeEntity>(
                    "process_recipe",
                    ProcessTextKeys.ProcessRecipeCodeGroup,
                    [.. data.Recipes.Select(recipe => recipe.Code)],
                    TextColumns,
                    code => [ProcessTextKeys.RecipeName(code), ProcessTextKeys.RecipeDescription(code)],
                    cancellationToken => _dbContext.ProcessRecipes.OrderBy(row => row.Id).ToListAsync(cancellationToken),
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
                new SeedCodeTable<ProcessParamEntity>(
                    "process_param",
                    ProcessTextKeys.ProcessParamCodeGroup,
                    [.. data.Params.Select(parameter => parameter.Code)],
                    TextColumns,
                    code => [ProcessTextKeys.ParamName(code), ProcessTextKeys.ParamDescription(code)],
                    cancellationToken => _dbContext.ProcessParams.OrderBy(row => row.Id).ToListAsync(cancellationToken),
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
            ];
        }
    }
}
