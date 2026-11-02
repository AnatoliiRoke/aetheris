using System.Net;
using Aetheris.Tests.Infrastructure;

namespace Aetheris.Tests.Observability;

/// <summary>
/// The pack of the texts can be kept by the browser and asked again with its tag, so a client that
/// comes back to the page gets a short 304 and not the whole pack again (task 00036, section 6.3).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class TextsCacheTests
{
    private const string PackPath = "/api/v1/texts/uk";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextsCacheTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public TextsCacheTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The pack carries the policy that lets the browser keep it and the tag it is asked again
    /// with, for a visitor and for a signed-in player whose cookie the server renews; the other
    /// answers of the API stay out of every cache.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_pack_may_be_kept_by_the_browser_and_the_other_answers_may_not()
    {
        using HttpClient visitor = _factory.CreateBrowserClient();
        using HttpResponseMessage anonymous = await visitor.GetAsync(PackPath, Token);

        AssertPolicy(anonymous);

        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);
        using HttpClient player = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(player, account);
        using HttpResponseMessage signedIn = await player.GetAsync(PackPath, Token);

        AssertPolicy(signedIn);
        Assert.Equal(anonymous.Headers.ETag, signedIn.Headers.ETag);

        foreach (string path in new[] { "/api/v1/ping", "/api/v1/auth/me" })
        {
            using HttpResponseMessage other = await player.GetAsync(path, Token);

            Assert.Equal("no-store", other.Headers.CacheControl?.ToString());
        }
    }

    /// <summary>
    /// A request with the tag of the pack gets 304 and no body, for a visitor and for a signed-in
    /// player, and the answer repeats the policy and the tag.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_request_with_the_tag_of_the_pack_gets_304_and_no_body()
    {
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);
        using HttpClient player = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(player, account);

        foreach (HttpClient client in new[] { _factory.CreateBrowserClient(), player })
        {
            using HttpResponseMessage full = await client.GetAsync(PackPath, Token);

            using HttpRequestMessage again = new(HttpMethod.Get, PackPath);
            again.Headers.TryAddWithoutValidation("If-None-Match", full.Headers.ETag!.ToString());
            using HttpResponseMessage cached = await client.SendAsync(again, Token);

            Assert.Equal(HttpStatusCode.NotModified, cached.StatusCode);
            Assert.Empty(await cached.Content.ReadAsByteArrayAsync(Token));
            Assert.Equal(full.Headers.ETag, cached.Headers.ETag);
            AssertPolicy(cached);
        }
    }

    private static void AssertPolicy(HttpResponseMessage response)
    {
        Assert.NotNull(response.Headers.CacheControl);
        Assert.True(response.Headers.CacheControl.NoCache, "The pack is asked again every time.");
        Assert.True(response.Headers.CacheControl.Private, "The pack is kept by the browser only.");
        Assert.False(response.Headers.CacheControl.NoStore, "The pack may be kept by the browser.");
        Assert.NotNull(response.Headers.ETag);
        Assert.False(response.Headers.Contains("Pragma"), "The pack must not carry Pragma: no-cache of the cookie.");
        Assert.False(response.Content.Headers.Contains("Expires"), "The pack must not carry the past Expires of the cookie.");
    }
}
