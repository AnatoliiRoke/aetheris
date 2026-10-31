using System.Text.Json;
using Aetheris.Core.Configuration;

namespace Aetheris.World.Configuration;

/// <summary>
/// The starting coefficients of the first seed (task 00028, section 3.2).
/// </summary>
/// <remarks>
/// The numbers live in the embedded file, not in the program. The host publishes them as
/// configuration parameters, and the generator that writes the seed files reads the same file.
/// </remarks>
public static class SeedCoefficientKeys
{
    /// <summary>The name of the embedded coefficient file.</summary>
    public const string ResourceName = "Aetheris.World.seed-coefficients.json";

    /// <summary>Gets the coefficient parameters.</summary>
    public static IReadOnlyList<ConfigParameterDefinition> Definitions { get; } = Load();

    private static IReadOnlyList<ConfigParameterDefinition> Load()
    {
        using Stream stream = typeof(SeedCoefficientKeys).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded starting data {ResourceName} is missing.");

        CoefficientFile? file = JsonSerializer.Deserialize<CoefficientFile>(
            stream,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidOperationException($"The embedded starting data {ResourceName} is empty.");

        return
        [
            .. file.Parameters.Select(parameter => new ConfigParameterDefinition(
                parameter.Key,
                parameter.Value,
                parameter.Minimum,
                parameter.Maximum,
                parameter.Description)),
        ];
    }

    private sealed record CoefficientFile(IReadOnlyList<CoefficientParameter> Parameters);

    private sealed record CoefficientParameter(
        string Key,
        string Value,
        string? Minimum,
        string? Maximum,
        string Description);
}
