using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Aetheris.Core.Admin;
using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Host.Security;
using Microsoft.Net.Http.Headers;

namespace Aetheris.Host.Admin;

/// <summary>
/// The configuration keys of the pictures of the reference rows (task 00023, decision 2).
/// </summary>
public static class ReferenceImageKeys
{
    /// <summary>The allowed types of a picture, separated by commas.</summary>
    public const string Types = "admin.reference.image_types";

    /// <summary>The largest size of a picture, in bytes.</summary>
    public const string MaxBytes = "admin.reference.image_max_bytes";

    /// <summary>The folder of the pictures; a relative path starts at the folder of the server.</summary>
    public const string Directory = "admin.reference.image_directory";

    /// <summary>
    /// Gets the definitions of the parameters.
    /// </summary>
    public static IReadOnlyList<ConfigParameterDefinition> Definitions { get; } =
    [
        new(Types, "image/png,image/webp", null, null, "Allowed types of a picture of a reference row, separated by commas: image/png, image/webp."),
        new(MaxBytes, "1048576", "1024", "16777216", "Largest size of a picture of a reference row, in bytes."),
        new(Directory, "uploads/reference-images", null, null, "Folder of the pictures of the reference rows; a relative path starts at the folder of the server."),
    ];
}

/// <summary>
/// The formats of the pictures the server recognises by their content: the type, the extension of
/// the file and the first bytes of the file.
/// </summary>
public static class ReferenceImageFormats
{
    /// <summary>The type of a PNG picture.</summary>
    public const string Png = "image/png";

    /// <summary>The type of a WebP picture.</summary>
    public const string Webp = "image/webp";

    private const int WebpMarkOffset = 8;

    /// <summary>The first eight bytes of every PNG file.</summary>
    private const string PngSignatureHex = "89504E470D0A1A0A";

    private static readonly byte[] PngSignature = Convert.FromHexString(PngSignatureHex);

    private static readonly byte[] RiffMark = "RIFF"u8.ToArray();

    private static readonly byte[] WebpMark = "WEBP"u8.ToArray();

    private static readonly Dictionary<string, string> Extensions = new(StringComparer.Ordinal)
    {
        [Png] = "png",
        [Webp] = "webp",
    };

    /// <summary>
    /// Recognises the type of a picture by its first bytes; the name and the declared type of the
    /// file are never trusted.
    /// </summary>
    /// <param name="content">The file.</param>
    /// <returns>The type, or <see langword="null"/> when the file is neither a PNG nor a WebP picture.</returns>
    public static string? Detect(ReadOnlySpan<byte> content)
    {
        if (content.StartsWith(PngSignature))
        {
            return Png;
        }

        return content.Length >= WebpMarkOffset + WebpMark.Length
            && content.StartsWith(RiffMark)
            && content.Slice(WebpMarkOffset, WebpMark.Length).SequenceEqual(WebpMark)
            ? Webp
            : null;
    }

    /// <summary>
    /// Gets the extension of the file of a type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The extension without the dot.</returns>
    public static string ExtensionOf(string type) => Extensions[type];

    /// <summary>
    /// Gets the type of a file by its extension.
    /// </summary>
    /// <param name="extension">The extension without the dot.</param>
    /// <returns>The type, or <see langword="null"/>.</returns>
    public static string? TypeOf(string extension) =>
        Extensions.FirstOrDefault(entry => string.Equals(entry.Value, extension, StringComparison.Ordinal)).Key;
}

/// <summary>
/// The pictures of the reference rows, stored as files on the server (task 00023, decision 2).
/// </summary>
/// <remarks>
/// The server names every file by a random key and its extension, so a key never names a path:
/// a key that does not match the pattern is never looked up on the disk. CDN, resizing and
/// compression come later.
/// </remarks>
public sealed partial class ReferenceImageStore : IAdminReferenceImages
{
    private const int KeyBytes = 16;

    private readonly IGameConfig _config;
    private readonly IHostEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReferenceImageStore"/> class.
    /// </summary>
    /// <param name="config">The game configuration.</param>
    /// <param name="environment">The environment, for the folder of the server.</param>
    public ReferenceImageStore(IGameConfig config, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(environment);

        _config = config;
        _environment = environment;
    }

    /// <summary>
    /// Gets the allowed types of a picture.
    /// </summary>
    public IReadOnlyList<string> AllowedTypes =>
        [.. (_config.GetRaw(ReferenceImageKeys.Types) ?? Default(ReferenceImageKeys.Types))
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    /// <summary>
    /// Gets the largest size of a picture, in bytes.
    /// </summary>
    public long MaxBytes => _config.GetInt64(ReferenceImageKeys.MaxBytes, long.Parse(Default(ReferenceImageKeys.MaxBytes), CultureInfo.InvariantCulture));

    private string Folder => Path.GetFullPath(Path.Combine(
        _environment.ContentRootPath,
        _config.GetRaw(ReferenceImageKeys.Directory) ?? Default(ReferenceImageKeys.Directory)));

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string imageKey, CancellationToken cancellationToken) =>
        Task.FromResult(PathOf(imageKey) is not null);

    /// <summary>
    /// Finds the file of a picture.
    /// </summary>
    /// <param name="imageKey">The key.</param>
    /// <returns>The path and the type, or <see langword="null"/> when there is no such picture.</returns>
    public (string Path, string Type)? PathOf(string imageKey)
    {
        if (string.IsNullOrEmpty(imageKey) || !KeyPattern().IsMatch(imageKey))
        {
            return null;
        }

        string path = Path.Combine(Folder, imageKey);
        string? type = ReferenceImageFormats.TypeOf(Path.GetExtension(imageKey).TrimStart('.'));

        return type is not null && File.Exists(path) ? (path, type) : null;
    }

    /// <summary>
    /// Stores a picture under a new key.
    /// </summary>
    /// <param name="content">The file.</param>
    /// <param name="type">The recognised type.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The key.</returns>
    public async Task<string> SaveAsync(byte[] content, string type, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        string key = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(KeyBytes)) + "." + ReferenceImageFormats.ExtensionOf(type);
        string folder = Folder;

        Directory.CreateDirectory(folder);
        await File.WriteAllBytesAsync(Path.Combine(folder, key), content, cancellationToken).ConfigureAwait(false);

        return key;
    }

    private static string Default(string key) =>
        ReferenceImageKeys.Definitions.Single(definition => definition.Key == key).DefaultValue;

    [GeneratedRegex("^[0-9a-f]{32}\\.(png|webp)$")]
    private static partial Regex KeyPattern();
}

/// <summary>
/// The upload and the read of the pictures of the reference rows (task 00023, section 4.2).
/// </summary>
public static class ReferenceImageEndpoints
{
    /// <summary>The route of the upload under the admin prefix.</summary>
    public const string UploadRoute = "/reference/images";

    /// <summary>The route of the read of a picture; it is public, the players see the pictures.</summary>
    public const string ReadRoute = "/api/v1/images/{imageKey}";

    /// <summary>The error code of a file that is not an allowed picture.</summary>
    public const string TypeRefused = "image_type_refused";

    /// <summary>The error code of a file larger than the configured size.</summary>
    public const string TooLarge = "image_too_large";

    /// <summary>The field of the errors of the file.</summary>
    public const string FileField = "file";

    /// <summary>A picture never changes under its key, so the browser keeps it for a year.</summary>
    private const string CacheControl = "public, max-age=31536000, immutable";

    /// <summary>The size of one read of the body of an upload.</summary>
    private const int ChunkBytes = 81920;

    /// <summary>
    /// Maps the upload on the secured admin group.
    /// </summary>
    /// <param name="secured">The secured admin group.</param>
    /// <returns>The same group.</returns>
    public static RouteGroupBuilder MapReferenceImageAdminEndpoints(this RouteGroupBuilder secured)
    {
        ArgumentNullException.ThrowIfNull(secured);

        secured.MapPost(UploadRoute, UploadAsync)
            .WithName("admin.reference_image_uploaded");

        return secured;
    }

    /// <summary>
    /// Maps the public read of the pictures.
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapReferenceImageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(ReadRoute, (string imageKey, HttpContext httpContext, ReferenceImageStore images) =>
        {
            (string Path, string Type)? file = images.PathOf(imageKey);

            if (file is null)
            {
                return ApiErrors.Result(StatusCodes.Status404NotFound, GameErrorCodes.NotFound);
            }

            httpContext.Response.Headers[HeaderNames.CacheControl] = CacheControl;

            return Results.File(file.Value.Path, file.Value.Type);
        }).AllowAnonymous();

        return endpoints;
    }

    /// <summary>
    /// Stores the body of the request as a picture: the type is recognised by the content and
    /// checked against the configuration, the size too. The key goes into a row with an ordinary
    /// change of the row, with its reason and its journal record.
    /// </summary>
    private static async Task<IResult> UploadAsync(
        HttpContext httpContext,
        ReferenceImageStore images,
        IAdminAuditRecorder audit,
        CancellationToken cancellationToken)
    {
        long maxBytes = images.MaxBytes;

        audit.Record("reference_image", null, null);

        if (httpContext.Request.ContentLength > maxBytes)
        {
            return Refused(TooLarge);
        }

        using MemoryStream buffer = new();
        byte[] chunk = new byte[ChunkBytes];
        int read;

        // A body without a declared length is read up to one byte over the limit.
        while ((read = await httpContext.Request.Body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            buffer.Write(chunk, 0, read);

            if (buffer.Length > maxBytes)
            {
                return Refused(TooLarge);
            }
        }

        if (buffer.Length == 0)
        {
            return ApiErrors.Validation(new Dictionary<string, string[]> { [FileField] = [AdminReferenceErrors.Required] });
        }

        byte[] content = buffer.ToArray();
        string? type = ReferenceImageFormats.Detect(content);

        if (type is null || !images.AllowedTypes.Contains(type, StringComparer.Ordinal))
        {
            return Refused(TypeRefused);
        }

        string key = await images.SaveAsync(content, type, cancellationToken).ConfigureAwait(false);

        audit.Record("reference_image:" + key, null, type + "; " + content.Length.ToString(CultureInfo.InvariantCulture));

        return AdminJson.Ok(new { image_key = key, type, size = content.Length });
    }

    private static IResult Refused(string code) =>
        ApiErrors.Result(
            StatusCodes.Status422UnprocessableEntity,
            GameErrorCodes.ValidationFailed,
            new Dictionary<string, object?> { [FileField] = new[] { code } });
}
