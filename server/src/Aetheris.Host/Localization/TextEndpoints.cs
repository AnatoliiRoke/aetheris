using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aetheris.Core.Admin;
using Aetheris.Core.Errors;
using Aetheris.Core.Text;
using Aetheris.Host.Admin;
using Aetheris.Host.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Aetheris.Host.Localization;

/// <summary>
/// A change of a text from the admin screen «Тексти».
/// </summary>
/// <param name="Key">The localisation key.</param>
/// <param name="Language">The language.</param>
/// <param name="Text">The new text.</param>
/// <param name="Version">The version the administrator read, 0 for a missing text.</param>
/// <param name="Reason">The reason of the change.</param>
public sealed record TextChangeRequest(
    [property: JsonPropertyName("key")] string? Key,
    [property: JsonPropertyName("language")] string? Language,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("reason")] string? Reason);

/// <summary>
/// The endpoints of the texts of the game data (task 00022): the pack of a language for the
/// client and the admin screen «Тексти».
/// </summary>
public static partial class TextEndpoints
{
    /// <summary>
    /// The route of the pack of a language.
    /// </summary>
    public const string PackRoute = "/api/v1/texts/{language}";

    /// <summary>
    /// The error code of a text that was changed by someone else since it was read.
    /// </summary>
    public const string VersionConflict = "text_version_conflict";

    /// <summary>
    /// The field error of a language outside the settings.
    /// </summary>
    public const string LanguageUnknown = "language_unknown";

    /// <summary>
    /// Maps the pack of a language. It is public: the landing page names the races before any
    /// sign-in. Only the global limit per address applies.
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapTextEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(PackRoute, ReadPackAsync).AllowAnonymous();

        return endpoints;
    }

    /// <summary>
    /// Maps the admin screen «Тексти» on the secured admin group.
    /// </summary>
    /// <param name="secured">The secured admin group.</param>
    /// <returns>The same group.</returns>
    public static RouteGroupBuilder MapTextAdminEndpoints(this RouteGroupBuilder secured)
    {
        ArgumentNullException.ThrowIfNull(secured);

        secured.MapGet("/texts", ReadTextsAsync)
            .WithName("admin.texts_read");

        secured.MapPut("/texts", ChangeTextAsync)
            .WithName("admin.text_updated");

        return secured;
    }

    private static async Task<IResult> ReadPackAsync(
        string language,
        HttpContext httpContext,
        ILocalizationTexts texts,
        CancellationToken cancellationToken)
    {
        if (!LanguagePattern().IsMatch(language))
        {
            return ApiErrors.Validation(new Dictionary<string, string[]> { ["language"] = [LanguageUnknown] });
        }

        LocalizationPack pack = await texts.GetPackAsync(language, cancellationToken).ConfigureAwait(false);
        string etag = string.Create(CultureInfo.InvariantCulture, $"\"{pack.Language}-{pack.Version}\"");

        // The client asks again whenever it comes back to the page; an unchanged pack costs a
        // short answer only.
        httpContext.Response.Headers[HeaderNames.ETag] = etag;
        httpContext.Response.Headers[HeaderNames.CacheControl] = "no-cache";

        if (httpContext.Request.Headers[HeaderNames.IfNoneMatch].Contains(etag))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return AdminJson.Ok(new { language = pack.Language, version = pack.Version, texts = pack.Texts });
    }

    private static async Task<IResult> ReadTextsAsync(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "search")] string? search,
        [FromQuery(Name = "language")] string? language,
        [FromQuery(Name = "missing")] bool? missing,
        AdminTextService texts,
        AdminConfigService configs,
        TextSettings settings,
        IAdminQueryLimits limits,
        CancellationToken cancellationToken)
    {
        PageRequest? request = AdminPaging.Create(page, pageSize, search, null, null, limits);

        if (request is null)
        {
            return ApiErrors.Validation(new Dictionary<string, string[]> { ["search"] = [AdminPaging.SearchTooLong] });
        }

        PageResult<AdminTextRow> rows = await texts.ListAsync(
            request,
            new AdminTextFilter(string.IsNullOrWhiteSpace(language) ? null : language.Trim(), missing ?? false),
            ids => configs.AdminNamesAsync(ids, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        return AdminJson.Ok(new
        {
            items = rows.Items,
            total = rows.Total,
            page = rows.Page,
            page_size = rows.PageSize,
            languages = settings.Languages,
            max_length = settings.MaxLength,
        });
    }

    private static async Task<IResult> ChangeTextAsync(
        TextChangeRequest request,
        ClaimsPrincipal principal,
        AdminTextService texts,
        ILocalizationTexts store,
        TextSettings settings,
        IAdminQueryLimits limits,
        AdminAuditScope audit,
        CancellationToken cancellationToken)
    {
        string key = request.Key?.Trim() ?? string.Empty;
        string language = request.Language?.Trim() ?? string.Empty;
        string text = request.Text?.Trim() ?? string.Empty;

        audit.Record(Target(key, language), null, null);

        Dictionary<string, string[]> failures = [];

        if (key.Length == 0)
        {
            failures["key"] = [AdminReason.Required];
        }

        if (!settings.Languages.Contains(language, StringComparer.Ordinal))
        {
            failures["language"] = [LanguageUnknown];
        }

        if (text.Length == 0)
        {
            failures["text"] = [AdminReason.Required];
        }
        else if (text.Length > settings.MaxLength)
        {
            failures["text"] = [AdminReason.TooLong];
        }

        if (AdminReason.Check(request.Reason, limits) is { } reasonError)
        {
            failures["reason"] = [reasonError];
        }

        if (failures.Count > 0)
        {
            return ApiErrors.Validation(failures);
        }

        (TextChangeOutcome outcome, string? oldText, int version) = await texts.ChangeAsync(
            key,
            language,
            text,
            request.Version,
            principal.GetAdminId()!,
            cancellationToken).ConfigureAwait(false);

        switch (outcome)
        {
            case TextChangeOutcome.NotFound:
                return ApiErrors.Result(StatusCodes.Status404NotFound, GameErrorCodes.NotFound);

            case TextChangeOutcome.Conflict:
                return ApiErrors.Result(StatusCodes.Status409Conflict, VersionConflict);

            default:
                // The copy in memory is dropped at once: the change is already written.
                store.Invalidate();
                audit.Record(Target(key, language), oldText, text);
                audit.RecordReason(request.Reason!.Trim());

                return AdminJson.Ok(new { key, language, text, version });
        }
    }

    private static string Target(string key, string language) => "text:" + key + "/" + language;

    [GeneratedRegex("^[a-z]{2,8}$")]
    private static partial Regex LanguagePattern();
}
