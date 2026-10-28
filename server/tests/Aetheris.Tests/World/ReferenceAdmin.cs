using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aetheris.Host.Admin;

namespace Aetheris.Tests.World;

/// <summary>
/// Helpers that call the shared endpoints of the reference kinds of the admin panel the way the
/// client does (task 00023).
/// </summary>
public static class ReferenceAdmin
{
    /// <summary>The reason of the changes of the tests.</summary>
    public const string Reason = "Reference test";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// Builds a text in both languages of the tests.
    /// </summary>
    /// <param name="ukrainian">The Ukrainian text.</param>
    /// <param name="english">The English text.</param>
    /// <returns>The text by language.</returns>
    public static Dictionary<string, string> Texts(string ukrainian, string english) =>
        new(StringComparer.Ordinal) { ["uk"] = ukrainian, ["en"] = english };

    /// <summary>
    /// Creates a row.
    /// </summary>
    /// <param name="client">The signed-in administrator.</param>
    /// <param name="route">The route of the kind.</param>
    /// <param name="values">The values.</param>
    /// <param name="reason">The reason.</param>
    /// <returns>The response.</returns>
    public static Task<HttpResponseMessage> CreateAsync(HttpClient client, string route, object values, string? reason = Reason)
    {
        ArgumentNullException.ThrowIfNull(client);

        return client.PostAsJsonAsync(AdminEndpoints.Prefix + route, new { reason, values }, Token);
    }

    /// <summary>
    /// Changes a row.
    /// </summary>
    /// <param name="client">The signed-in administrator.</param>
    /// <param name="route">The route of the kind.</param>
    /// <param name="id">The row.</param>
    /// <param name="version">The version the administrator read.</param>
    /// <param name="values">The values.</param>
    /// <param name="reason">The reason.</param>
    /// <returns>The response.</returns>
    public static Task<HttpResponseMessage> UpdateAsync(HttpClient client, string route, int id, int version, object values, string? reason = Reason)
    {
        ArgumentNullException.ThrowIfNull(client);

        return client.PutAsJsonAsync(AdminEndpoints.Prefix + route + "/" + id, new { version, reason, values }, Token);
    }

    /// <summary>
    /// Archives a row or returns it from the archive.
    /// </summary>
    /// <param name="client">The signed-in administrator.</param>
    /// <param name="route">The route of the kind.</param>
    /// <param name="id">The row.</param>
    /// <param name="version">The version the administrator read.</param>
    /// <param name="archive">Whether the row is archived.</param>
    /// <returns>The response.</returns>
    public static Task<HttpResponseMessage> ArchiveAsync(HttpClient client, string route, int id, int version, bool archive = true)
    {
        ArgumentNullException.ThrowIfNull(client);

        return client.PutAsJsonAsync(
            AdminEndpoints.Prefix + route + "/" + id + (archive ? "/archive" : "/restore"),
            new { version, reason = Reason },
            Token);
    }

    /// <summary>
    /// Reads a record.
    /// </summary>
    /// <param name="client">The signed-in administrator.</param>
    /// <param name="route">The route of the kind.</param>
    /// <param name="id">The row.</param>
    /// <returns>The record.</returns>
    public static async Task<JsonElement> ReadAsync(HttpClient client, string route, int id)
    {
        ArgumentNullException.ThrowIfNull(client);

        using JsonDocument document = JsonDocument.Parse(await client.GetStringAsync(AdminEndpoints.Prefix + route + "/" + id, Token));

        return document.RootElement.Clone();
    }

    /// <summary>
    /// Reads the codes of a list of a kind.
    /// </summary>
    /// <param name="client">The signed-in administrator.</param>
    /// <param name="route">The route of the kind.</param>
    /// <param name="query">The query string, without the question mark.</param>
    /// <returns>The codes of the rows.</returns>
    public static async Task<List<string>> ListCodesAsync(HttpClient client, string route, string query = "")
    {
        ArgumentNullException.ThrowIfNull(client);

        using JsonDocument document = JsonDocument.Parse(await client.GetStringAsync(AdminEndpoints.Prefix + route + "?" + query, Token));

        return [.. document.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("code").GetString()!)];
    }

    /// <summary>
    /// Reads the identifier and the version of a stored change.
    /// </summary>
    /// <param name="response">The response of the change.</param>
    /// <returns>The identifier and the version.</returns>
    public static async Task<(int Id, int Version)> ChangedAsync(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        string body = await response.Content.ReadAsStringAsync(Token);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using JsonDocument document = JsonDocument.Parse(body);

        return (document.RootElement.GetProperty("id").GetInt32(), document.RootElement.GetProperty("version").GetInt32());
    }

    /// <summary>
    /// Checks that a change was refused with a code of a field.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="status">The expected status.</param>
    /// <param name="field">The path of the field.</param>
    /// <param name="code">The code of the field.</param>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    public static async Task AssertFieldAsync(HttpResponseMessage response, HttpStatusCode status, string field, string code)
    {
        ArgumentNullException.ThrowIfNull(response);

        string body = await response.Content.ReadAsStringAsync(Token);
        Assert.True(response.StatusCode == status, $"{(int)response.StatusCode}: {body}");

        using JsonDocument document = JsonDocument.Parse(body);
        Assert.True(
            document.RootElement.GetProperty("details").TryGetProperty(field, out JsonElement codes),
            $"The field {field} has no error: {body}");
        Assert.Contains(code, codes.EnumerateArray().Select(entry => entry.GetString()));
    }
}
