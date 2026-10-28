using System.Globalization;
using System.Text.Json;
using Aetheris.Core.Data;
using Aetheris.Core.Errors;
using Aetheris.Core.Text;
using Aetheris.World.Api;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.World.Services;

/// <summary>
/// Creates the reference rows of the world from the admin panel (task 00022, decision 6).
/// </summary>
/// <remarks>
/// The server gives the neutral code, the administrator never types it. A new row comes with its
/// name and description in every language of the texts; the row and its texts are committed
/// together, so a row never exists without its name.
/// </remarks>
public sealed class WorldReferenceAdminService
{
    private readonly WorldDbContext _dbContext;
    private readonly WorldUnitOfWork _unitOfWork;
    private readonly ICodeSequence _codes;
    private readonly ILocalizationTexts _texts;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldReferenceAdminService"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="unitOfWork">The transaction of the request.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="texts">The texts of the game data.</param>
    public WorldReferenceAdminService(WorldDbContext dbContext, WorldUnitOfWork unitOfWork, ICodeSequence codes, ILocalizationTexts texts)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(texts);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _codes = codes;
        _texts = texts;
    }

    /// <summary>
    /// Creates a resource type with its texts.
    /// </summary>
    /// <param name="request">The group and the texts of the resource.</param>
    /// <param name="adminId">The administrator, the author of the texts.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What was created, for the journal.</returns>
    public async Task<AdminChange> CreateResourceAsync(AdminResourceCreate request, string adminId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(adminId);

        Dictionary<string, string[]> failures = CheckTexts(request.Name, request.Description);

        if (!await _dbContext.ResourceGroups.AnyAsync(group => group.Id == request.ResourceGroupId, cancellationToken).ConfigureAwait(false))
        {
            failures["resource_group_id"] = [AdminEditErrors.Unknown];
        }

        if (failures.Count > 0)
        {
            throw new WorldAdminEditException(GameErrorCodes.ValidationFailed, failures);
        }

        AdminChange change = await _unitOfWork.RunAsync(
            async () =>
            {
                string code = await _codes.NextAsync(WorldCodeGroups.Resource, cancellationToken).ConfigureAwait(false);
                ResourceTypeEntity resource = new()
                {
                    Code = code,
                    ResourceGroupId = request.ResourceGroupId,
                    NameKey = WorldTextKeys.Name(WorldTextKeys.Resource, code),
                    DescriptionKey = WorldTextKeys.Description(WorldTextKeys.Resource, code),
                };

                _dbContext.ResourceTypes.Add(resource);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await _texts.AddMissingAsync(
                    [
                        .. SeedTexts.Entries(resource.NameKey, Trimmed(request.Name)),
                        .. SeedTexts.Entries(resource.DescriptionKey, Trimmed(request.Description)),
                    ],
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                string newValue = JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["code"] = code,
                    ["resource_group_id"] = resource.ResourceGroupId,
                    ["name"] = Trimmed(request.Name),
                    ["description"] = Trimmed(request.Description),
                });

                return new AdminChange("resource:" + resource.Id.ToString(CultureInfo.InvariantCulture), "{}", newValue, resource.Id, 1);
            },
            cancellationToken).ConfigureAwait(false);

        // The row and its texts are committed: the copy of the texts in memory is dropped.
        _texts.Invalidate();

        return change;
    }

    private Dictionary<string, string[]> CheckTexts(IReadOnlyDictionary<string, string>? name, IReadOnlyDictionary<string, string>? description)
    {
        Dictionary<string, string[]> failures = [];

        foreach (string language in _texts.Languages)
        {
            Check(failures, "name." + language, name?.GetValueOrDefault(language));
            Check(failures, "description." + language, description?.GetValueOrDefault(language));
        }

        return failures;
    }

    private void Check(Dictionary<string, string[]> failures, string field, string? text)
    {
        string trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            failures[field] = [AdminEditErrors.Required];
        }
        else if (trimmed.Length > _texts.MaxLength)
        {
            failures[field] = [AdminEditErrors.TooLong];
        }
    }

    private Dictionary<string, string> Trimmed(IReadOnlyDictionary<string, string>? texts) =>
        _texts.Languages.ToDictionary(language => language, language => texts![language].Trim(), StringComparer.Ordinal);
}
