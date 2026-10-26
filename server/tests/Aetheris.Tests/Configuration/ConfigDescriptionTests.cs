using System.Text.RegularExpressions;
using Aetheris.Host.Data;

namespace Aetheris.Tests.Configuration;

/// <summary>
/// Tests that every configuration parameter has its Ukrainian description in the dictionary
/// of the admin panel (task 00009, decision 13).
/// </summary>
public sealed partial class ConfigDescriptionTests
{
    /// <summary>
    /// The dictionary of the client, relative to the root of the repository.
    /// </summary>
    private static readonly string[] DictionaryPath = ["client", "src", "admin", "i18n", "configUk.ts"];

    /// <summary>
    /// Every parameter of the seed, the world parameters included, has a Ukrainian description
    /// under <c>config.&lt;key&gt;.description</c>, and the dictionary has no description of a
    /// parameter that does not exist.
    /// </summary>
    [Fact]
    public void Every_parameter_has_a_Ukrainian_description()
    {
        string text = File.ReadAllText(Path.Combine([RepositoryRoot(), .. DictionaryPath]));
        Dictionary<string, string> descriptions = DescriptionPattern().Matches(text)
            .ToDictionary(match => match.Groups["key"].Value, match => match.Groups["text"].Value, StringComparer.Ordinal);

        List<string> keys = [.. ConfigSeed.All.Select(definition => definition.Key)];
        List<string> missing = [.. keys.Where(key => !descriptions.TryGetValue(key, out string? description) || string.IsNullOrWhiteSpace(description))];
        List<string> unknown = [.. descriptions.Keys.Except(keys, StringComparer.Ordinal)];

        Assert.Contains(keys, key => key.StartsWith("world.", StringComparison.Ordinal));
        Assert.True(missing.Count == 0, "Parameters without a Ukrainian description: " + string.Join(", ", missing));
        Assert.True(unknown.Count == 0, "Descriptions of unknown parameters: " + string.Join(", ", unknown));
        Assert.All(descriptions.Values, description => Assert.Matches(CyrillicPattern(), description));
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "client")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The root of the repository was not found.");
    }

    [GeneratedRegex(@"'config\.(?<key>[^']+)\.description':\s*'(?<text>(?:[^'\\]|\\.)*)'")]
    private static partial Regex DescriptionPattern();

    [GeneratedRegex(@"\p{IsCyrillic}")]
    private static partial Regex CyrillicPattern();
}
