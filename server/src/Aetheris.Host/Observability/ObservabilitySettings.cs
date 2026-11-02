using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Core.Configuration;

namespace Aetheris.Host.Observability;

/// <summary>
/// The values of the observability parameters, parsed once per version of the configuration.
/// </summary>
/// <param name="TraceEnabled">Whether the tracing of the requests is on.</param>
/// <param name="RequestIdMaxLength">The largest length of a request identifier of a client.</param>
/// <param name="AllowedHeaders">The headers whose values may appear in the log.</param>
/// <param name="SlowRequestMilliseconds">The duration from which a request is slow.</param>
/// <param name="BodiesEnabled">Whether the JSON bodies are written.</param>
/// <param name="BodiesMaxBytes">The largest number of bytes of a body in the log.</param>
/// <param name="MaskedFields">The parts of the names of the masked JSON fields.</param>
/// <param name="ExcludedPaths">The path prefixes without bodies.</param>
/// <param name="SqlSlowMilliseconds">The duration from which an SQL command is slow.</param>
/// <param name="SqlVerbose">Whether every SQL command is written.</param>
/// <param name="SqlMaxTextLength">The largest length of the text of an SQL command in the log.</param>
/// <param name="MaskedFieldPattern">The pattern that masks the fields in a body that is cut or is not valid JSON.</param>
public sealed record ObservabilitySnapshot(
    bool TraceEnabled,
    int RequestIdMaxLength,
    HashSet<string> AllowedHeaders,
    int SlowRequestMilliseconds,
    bool BodiesEnabled,
    int BodiesMaxBytes,
    string[] MaskedFields,
    string[] ExcludedPaths,
    int SqlSlowMilliseconds,
    bool SqlVerbose,
    int SqlMaxTextLength,
    Regex? MaskedFieldPattern);

/// <summary>
/// Typed read access to the observability parameters, shared by the middleware and the
/// interceptors.
/// </summary>
/// <remarks>
/// The snapshot is rebuilt only when the configuration is reloaded, so the work per request
/// and per SQL command is one comparison of the version. A value outside the bounds of its
/// definition is moved to the nearest bound, and a missing value falls back to the starting
/// value.
/// </remarks>
public sealed class ObservabilitySettings
{
    /// <summary>
    /// The longest time a pattern that masks a body may take, so a hostile body cannot hold a thread.
    /// </summary>
    private const int PatternTimeoutMilliseconds = 250;

    private static readonly TimeSpan PatternTimeout = TimeSpan.FromMilliseconds(PatternTimeoutMilliseconds);

    private static readonly Dictionary<string, ConfigParameterDefinition> DefinitionsByKey =
        ObservabilityKeys.Definitions.ToDictionary(definition => definition.Key, StringComparer.Ordinal);

    private readonly GameConfig _config;
    private ObservabilitySnapshot? _snapshot;
    private int _snapshotVersion = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservabilitySettings"/> class.
    /// </summary>
    /// <param name="config">The configuration.</param>
    public ObservabilitySettings(GameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _config = config;
    }

    /// <summary>
    /// Gets the values for the current version of the configuration.
    /// </summary>
    public ObservabilitySnapshot Current
    {
        get
        {
            ObservabilitySnapshot? snapshot = Volatile.Read(ref _snapshot);
            int version = _config.Version;

            if (snapshot is not null && version == Volatile.Read(ref _snapshotVersion))
            {
                return snapshot;
            }

            // Two threads may both rebuild: the result is the same, the last write wins.
            snapshot = Build();
            Volatile.Write(ref _snapshot, snapshot);
            Volatile.Write(ref _snapshotVersion, version);

            return snapshot;
        }
    }

    private ObservabilitySnapshot Build()
    {
        string[] maskedFields = ReadList(ObservabilityKeys.HttpBodiesMaskedFields);

        return new ObservabilitySnapshot(
            ReadBoolean(ObservabilityKeys.HttpTraceEnabled),
            ReadInt32(ObservabilityKeys.RequestIdMaxLength),
            new HashSet<string>(ReadList(ObservabilityKeys.AllowedHeaders), StringComparer.OrdinalIgnoreCase),
            ReadInt32(ObservabilityKeys.SlowRequestMilliseconds),
            ReadBoolean(ObservabilityKeys.HttpBodiesEnabled),
            ReadInt32(ObservabilityKeys.HttpBodiesMaxBytes),
            maskedFields,
            ReadList(ObservabilityKeys.HttpBodiesExcludedPaths),
            ReadInt32(ObservabilityKeys.SqlSlowMilliseconds),
            ReadBoolean(ObservabilityKeys.SqlVerbose),
            ReadInt32(ObservabilityKeys.SqlMaxTextLength),
            BuildMaskPattern(maskedFields));
    }

    private static Regex? BuildMaskPattern(string[] maskedFields)
    {
        if (maskedFields.Length == 0)
        {
            return null;
        }

        string alternatives = string.Join('|', maskedFields.Select(Regex.Escape));

        // A name that contains one of the parts, then its value: a string (possibly cut at the
        // end of the text), a whole object or array to the end of the text, or a bare literal.
        // An object or an array is masked up to the end, which hides more but never less.
        string pattern = "\"(?<name>[^\"\\\\]*(?:" + alternatives + ")[^\"\\\\]*)\"\\s*:\\s*(?:\"(?:[^\"\\\\]|\\\\.)*(?:\"|$)|[\\{\\[].*|[^,}\\]\\s]*)";

        return new Regex(
            pattern,
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant,
            PatternTimeout);
    }

    private bool ReadBoolean(string key)
    {
        bool fallback = bool.Parse(DefinitionsByKey[key].DefaultValue);

        return _config.GetBoolean(key, fallback);
    }

    private int ReadInt32(string key)
    {
        ConfigParameterDefinition definition = DefinitionsByKey[key];
        double fallback = double.Parse(definition.DefaultValue, CultureInfo.InvariantCulture);

        return (int)definition.Clamp(_config.GetDouble(key, fallback));
    }

    private string[] ReadList(string key)
    {
        string text = _config.GetRaw(key) ?? DefinitionsByKey[key].DefaultValue;

        return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
