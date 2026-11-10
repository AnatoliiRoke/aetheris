using System.Net;
using System.Text.Json;
using Aetheris.Core.Data;
using Aetheris.Core.Text;
using Aetheris.Host.Admin;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Host.Localization;
using Aetheris.Market;
using Aetheris.Market.Data;
using Aetheris.Processes.Data;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Processes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Localization;

/// <summary>
/// Tests that the starting data is the source of the codes of the reference rows: the seeders keep
/// the codes of the file, the counters stand above them, the command <c>reconcile-seed-codes</c>
/// brings a database written by an older seeder to the codes of the file, and the pack of the
/// texts follows the keys (task 00052).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class SeedCodeTests
{
    private const string PackPath = "/api/v1/texts/uk";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeedCodeTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public SeedCodeTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A fresh database holds the codes of the file in Production, Processes and Market, in the
    /// order of the file, and the keys of the texts are built from those codes; every key has a text
    /// in every language.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_fresh_database_has_the_codes_of_the_seed_and_the_keys_built_from_them()
    {
        await PrepareAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        List<ISeedCodeTable> tables = Tables(services);

        Assert.Equal(["part_group", "part_type", "recipe", "characteristic", "process_type", "process_recipe", "process_param", "market_level"], tables.Select(table => table.Table));

        HashSet<string> keys = [];

        foreach (ISeedCodeTable table in tables)
        {
            IReadOnlyList<SeedCodeRowKeys> rows = await table.ReadAsync(Token);

            Assert.Equal(table.SeedCodes, rows.Select(row => row.Code));

            foreach (SeedCodeRowKeys row in rows)
            {
                Assert.Equal(ExpectedKeys(table, row), row.Keys);
                keys.UnionWith(row.Keys.OfType<string>());
            }
        }

        // A characteristic of the seed that has no unit holds no unit key, and every key a row holds
        // has its text in every language.
        Assert.NotEmpty(keys);

        AetherisDbContext dbContext = services.GetRequiredService<AetherisDbContext>();
        IReadOnlyList<string> languages = services.GetRequiredService<ILocalizationTexts>().Languages;
        List<string> keyList = [.. keys];
        Dictionary<string, int> languagesByKey = await dbContext.LocalizationTexts.AsNoTracking()
            .Where(text => keyList.Contains(text.Key))
            .GroupBy(text => text.Key)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Key, group => group.Count, Token);

        List<string> withoutText = [.. keys.Where(key => languagesByKey.GetValueOrDefault(key) != languages.Count)];

        Assert.True(withoutText.Count == 0, "Keys without a text in every language: " + string.Join(", ", withoutText.Take(10)));
    }

    /// <summary>
    /// After the seed the counter of every group stands at or above the largest number the codes of
    /// the file end with, and the code the counter gives next is not a code of the file.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_counters_stand_above_the_codes_of_the_seed_and_an_admin_code_never_meets_one()
    {
        await PrepareAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        AetherisDbContext dbContext = services.GetRequiredService<AetherisDbContext>();
        ICodeSequence codes = services.GetRequiredService<ICodeSequence>();
        Dictionary<string, int> counters = await dbContext.CodeSequences.AsNoTracking()
            .ToDictionaryAsync(counter => counter.CodeGroup, counter => counter.LastNumber, Token);

        foreach (ISeedCodeTable table in Tables(services))
        {
            Assert.True(
                counters.GetValueOrDefault(table.CodeGroup) >= SeedCodeRules.LargestNumber(table.SeedCodes),
                $"The counter '{table.CodeGroup}' stands below the codes of the starting data.");

            string next = await codes.NextAsync(table.CodeGroup, Token);

            Assert.DoesNotContain(next, table.SeedCodes);
            Assert.True(SeedCodeRules.TrailingNumber(next) > SeedCodeRules.LargestNumber(table.SeedCodes));
        }
    }

    /// <summary>
    /// The starting data of every module holds a code for every row, in the plain form of letters,
    /// digits and underscores, and no code twice; an empty, malformed or repeated code stops the seed
    /// with a clear message.
    /// </summary>
    [Fact]
    public void An_empty_malformed_or_repeated_code_stops_the_seed()
    {
        ProductionReferenceSeed.ValidateCodes(ProductionReferenceSeed.Load());
        ProcessReferenceSeed.ValidateCodes(ProcessReferenceSeed.Load());
        MarketReferenceSeed.ValidateCodes(MarketReferenceSeed.Load());

        ProductionSeedData production = ProductionReferenceSeed.Load();
        SeedPart first = production.Parts[0];

        InvalidOperationException repeated = Assert.Throws<InvalidOperationException>(
            () => ProductionReferenceSeed.ValidateCodes(production with { Parts = [first, first with { SortOrder = 2 }] }));
        Assert.Contains(first.Code, repeated.Message, StringComparison.Ordinal);
        Assert.Contains("twice", repeated.Message, StringComparison.Ordinal);

        Assert.Contains("no code", Assert.Throws<InvalidOperationException>(
            () => ProductionReferenceSeed.ValidateCodes(production with { Parts = [first with { Code = string.Empty }] })).Message, StringComparison.Ordinal);
        Assert.Contains("only letters", Assert.Throws<InvalidOperationException>(
            () => ProductionReferenceSeed.ValidateCodes(production with { Parts = [first with { Code = "part m-1" }] })).Message, StringComparison.Ordinal);

        ProcessSeedData processes = ProcessReferenceSeed.Load();

        Assert.Contains("twice", Assert.Throws<InvalidOperationException>(
            () => ProcessReferenceSeed.ValidateCodes(processes with { Types = [processes.Types[0], processes.Types[0]] })).Message, StringComparison.Ordinal);

        MarketSeedData market = MarketReferenceSeed.Load();

        Assert.Contains("no code", Assert.Throws<InvalidOperationException>(
            () => MarketReferenceSeed.ValidateCodes(market with { MarketLevels = [market.MarketLevels[0] with { Code = " " }] })).Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The matcher leaves a table that already holds the codes of the file as it is, gives the k-th
    /// row the k-th code of the file for a table of the counter form, and refuses a table it cannot
    /// match without a guess.
    /// </summary>
    [Fact]
    public void The_matcher_renames_only_a_table_it_can_match_without_a_guess()
    {
        string[] seed = ["pg_1", "pg_2", "pg_3"];

        Assert.Empty(SeedCodeMatcher.Plan("part_group", "part_group", seed, [new(1, "pg_1"), new(2, "pg_2"), new(3, "pg_3"), new(4, "part_group_9")]));
        Assert.Empty(SeedCodeMatcher.Plan("part_group", "part_group", seed, []));

        IReadOnlyList<SeedCodeMove> moves = SeedCodeMatcher.Plan("part_group", "part_group", seed, [new(7, "part_group_1"), new(8, "part_group_2"), new(9, "part_group_3")]);
        Assert.Equal([new SeedCodeMove(7, "part_group_1", "pg_1"), new SeedCodeMove(8, "part_group_2", "pg_2"), new SeedCodeMove(9, "part_group_3", "pg_3")], moves);

        // A row that already has the code of the file at its place is not touched.
        Assert.Equal(
            [new SeedCodeMove(9, "part_group_3", "pg_3")],
            SeedCodeMatcher.Plan("part_group", "part_group", seed, [new(7, "pg_1"), new(8, "pg_2"), new(9, "part_group_3")]));

        Assert.Contains("rows", Assert.Throws<SeedCodeMismatchException>(
            () => SeedCodeMatcher.Plan("part_group", "part_group", seed, [new(1, "part_group_1"), new(2, "part_group_2")])).Message, StringComparison.Ordinal);
        Assert.Contains("neither", Assert.Throws<SeedCodeMismatchException>(
            () => SeedCodeMatcher.Plan("part_group", "part_group", seed, [new(1, "part_group_1"), new(2, "mine"), new(3, "part_group_3")])).Message, StringComparison.Ordinal);
        Assert.Contains("place", Assert.Throws<SeedCodeMismatchException>(
            () => SeedCodeMatcher.Plan("part_group", "part_group", seed, [new(1, "pg_2"), new(2, "part_group_2"), new(3, "part_group_3")])).Message, StringComparison.Ordinal);
        Assert.Contains("order", Assert.Throws<SeedCodeMismatchException>(
            () => SeedCodeMatcher.Plan("part_group", "part_group", seed, [new(1, "part_group_3"), new(2, "part_group_2"), new(3, "part_group_1")])).Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A database written by an older seeder (the codes and the keys of the counter form) is brought
    /// to the codes of the file with its keys and texts; the mode of the check writes nothing; a
    /// second run changes nothing; the version of the renamed texts rises, so the pack changes.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_command_brings_a_database_of_an_older_seeder_to_the_codes_of_the_seed()
    {
        await PrepareAsync();
        Dictionary<string, string[]> seedCodes = [];

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            foreach (ISeedCodeTable table in Tables(scope.ServiceProvider))
            {
                seedCodes[table.Table] = [.. table.SeedCodes];
            }
        }

        string packTagBefore = await PackTagAsync();
        await MakeOlderStateAsync();

        // The older state really differs from the file.
        Dictionary<string, string[]> older = await CodesAsync();
        Assert.NotEqual(seedCodes["part_type"], older["part_type"]);
        Assert.Equal("part_1", older["part_type"][0]);
        string olderTag = await PackTagAsync();
        Assert.NotEqual(packTagBefore, olderTag);

        // The check shows the differences and writes nothing.
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            SeedCodeReport check = await scope.ServiceProvider.GetRequiredService<SeedCodeReconciliation>().CheckAsync(Token);

            Assert.True(check.HasChanges);
            Assert.False(check.HasProblems);
            Assert.Equal(0, check.TextsRenamed);
        }

        Assert.Equal(older, await CodesAsync());

        // The command in the mode of the check fails and tells so; it writes nothing.
        StringWriter checkOutput = new();

        Assert.Equal(1, await SeedCodeCommandLine.RunAsync(_factory.Services, [SeedCodeCommandLine.ReconcileCommand, SeedCodeCommandLine.CheckOption], checkOutput, Token));
        Assert.Contains("part_type", checkOutput.ToString(), StringComparison.Ordinal);
        Assert.Equal(older, await CodesAsync());

        // The fix brings every table to the codes of the file.
        long versionSumBefore = await TextVersionSumAsync();
        StringWriter fixOutput = new();

        Assert.Equal(0, await SeedCodeCommandLine.RunAsync(_factory.Services, [SeedCodeCommandLine.ReconcileCommand], fixOutput, Token));
        Assert.Contains("Виправлено", fixOutput.ToString(), StringComparison.Ordinal);
        Assert.Equal(seedCodes, await CodesAsync());
        Assert.True(await TextVersionSumAsync() > versionSumBefore, "The version of the renamed texts must rise.");

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            foreach (ISeedCodeTable table in Tables(scope.ServiceProvider))
            {
                foreach (SeedCodeRowKeys row in await table.ReadAsync(Token))
                {
                    Assert.Equal(ExpectedKeys(table, row), row.Keys);
                }
            }

            await AssertEveryKeyHasTextsAsync(scope.ServiceProvider);

            // The counters stand above the codes of the file again.
            Dictionary<string, int> counters = await scope.ServiceProvider.GetRequiredService<AetherisDbContext>().CodeSequences.AsNoTracking()
                .ToDictionaryAsync(counter => counter.CodeGroup, counter => counter.LastNumber, Token);

            foreach (ISeedCodeTable table in Tables(scope.ServiceProvider))
            {
                Assert.True(counters.GetValueOrDefault(table.CodeGroup) >= SeedCodeRules.LargestNumber(table.SeedCodes));
            }
        }

        // The pack of the texts is a pack of the new keys, and its tag differs from the older one.
        Assert.NotEqual(olderTag, await PackTagAsync());

        // A second run changes nothing and says so.
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            SeedCodeReport again = await scope.ServiceProvider.GetRequiredService<SeedCodeReconciliation>().ReconcileAsync(Token);

            Assert.False(again.HasChanges);
            Assert.False(again.HasProblems);
            Assert.Equal(0, again.TextsRenamed);
        }

        StringWriter secondOutput = new();

        Assert.Equal(0, await SeedCodeCommandLine.RunAsync(_factory.Services, [SeedCodeCommandLine.ReconcileCommand, SeedCodeCommandLine.CheckOption], secondOutput, Token));
        Assert.Contains("Розбіжностей немає", secondOutput.ToString(), StringComparison.Ordinal);
        Assert.Equal(seedCodes, await CodesAsync());
    }

    /// <summary>
    /// A table with more rows than the file cannot be matched without a guess, so the command stops
    /// without changing any table and says why.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_table_with_more_rows_than_the_seed_stops_the_command_without_changes()
    {
        await PrepareAsync();
        await MakeOlderStateAsync();

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            ProductionDbContext dbContext = scope.ServiceProvider.GetRequiredService<ProductionDbContext>();
            PartGroupEntity first = await dbContext.PartGroups.AsNoTracking().OrderBy(group => group.Id).FirstAsync(Token);

            dbContext.PartGroups.Add(new PartGroupEntity
            {
                Code = "part_group_900",
                NameKey = "production.part_group.part_group_900.name",
                DescriptionKey = "production.part_group.part_group_900.description",
                SortOrder = first.SortOrder,
            });
            await dbContext.SaveChangesAsync(Token);
        }

        Dictionary<string, string[]> before = await CodesAsync();
        StringWriter output = new();

        Assert.Equal(1, await SeedCodeCommandLine.RunAsync(_factory.Services, [SeedCodeCommandLine.ReconcileCommand], output, Token));
        Assert.Contains("part_group", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Зупинено без змін", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(before, await CodesAsync());

        // A seeder run on such a database does not add the rows of the file a second time.
        using IServiceScope seedScope = _factory.Services.CreateScope();
        int partCount = await seedScope.ServiceProvider.GetRequiredService<Aetheris.Production.Data.ProductionDbContext>().PartTypes.CountAsync(Token);

        await Assert.ThrowsAsync<InvalidOperationException>(() => seedScope.ServiceProvider.GetRequiredService<Aetheris.Production.IProductionModule>().EnsureAsync(Token));
        Assert.Equal(partCount, await seedScope.ServiceProvider.GetRequiredService<Aetheris.Production.Data.ProductionDbContext>().PartTypes.CountAsync(Token));
    }

    /// <summary>
    /// A key renamed in the table without a change of any version still changes the tag of the pack,
    /// the server keeps the built pack until its lifetime ends and then builds it again, and a request
    /// with the old tag gets the whole new pack, not 304.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_pack_follows_the_keys_and_a_request_with_the_old_tag_gets_the_new_pack()
    {
        await PrepareAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        AetherisDbContext dbContext = services.GetRequiredService<AetherisDbContext>();

        string? previousLifetime = await AetherisTestFactory.SetParameterAsync(services, TextKeys.PackCacheSeconds, "3600");

        try
        {
            using HttpClient client = _factory.CreateBrowserClient();
            using HttpResponseMessage first = await client.GetAsync(PackPath, Token);
            string oldTag = first.Headers.ETag!.Tag;
            using JsonDocument firstPack = JsonDocument.Parse(await first.Content.ReadAsStringAsync(Token));
            long version = firstPack.RootElement.GetProperty("version").GetInt64();

            LocalizationTextEntity text = await dbContext.LocalizationTexts.AsNoTracking()
                .Where(entry => entry.Language == "uk" && entry.Key.StartsWith("production.recipe."))
                .OrderBy(entry => entry.Key)
                .FirstAsync(Token);
            string newKey = text.Key + ".renamed";

            // The key is renamed in the table and no version changes, as a change outside the
            // application does.
            await dbContext.Database.ExecuteSqlAsync($"UPDATE localization_text SET key = {newKey} WHERE id = {text.Id}", Token);

            // The built pack is kept for its lifetime, so the old pack is still served.
            using HttpResponseMessage cached = await client.GetAsync(PackPath, Token);

            Assert.Equal(oldTag, cached.Headers.ETag!.Tag);

            // Once the lifetime is over the pack is built again from the table.
            await AetherisTestFactory.SetParameterAsync(services, TextKeys.PackCacheSeconds, "1");
            await Task.Delay(TimeSpan.FromMilliseconds(1500), Token);

            using HttpResponseMessage rebuilt = await client.GetAsync(PackPath, Token);
            string newTag = rebuilt.Headers.ETag!.Tag;
            using JsonDocument rebuiltPack = JsonDocument.Parse(await rebuilt.Content.ReadAsStringAsync(Token));

            Assert.NotEqual(oldTag, newTag);
            Assert.Equal(version, rebuiltPack.RootElement.GetProperty("version").GetInt64());
            Assert.False(rebuiltPack.RootElement.GetProperty("texts").TryGetProperty(text.Key, out _));
            Assert.True(rebuiltPack.RootElement.GetProperty("texts").TryGetProperty(newKey, out _));

            // The browser with the old tag gets the whole pack; the browser with the new one gets 304.
            using HttpRequestMessage oldTagRequest = new(HttpMethod.Get, PackPath);
            oldTagRequest.Headers.TryAddWithoutValidation("If-None-Match", oldTag);
            using HttpResponseMessage withOldTag = await client.SendAsync(oldTagRequest, Token);

            Assert.Equal(HttpStatusCode.OK, withOldTag.StatusCode);
            Assert.Equal(newTag, withOldTag.Headers.ETag!.Tag);
            Assert.True(withOldTag.Headers.CacheControl!.NoCache);
            Assert.True(withOldTag.Headers.CacheControl.Private);

            using HttpRequestMessage newTagRequest = new(HttpMethod.Get, PackPath);
            newTagRequest.Headers.TryAddWithoutValidation("If-None-Match", newTag);
            using HttpResponseMessage withNewTag = await client.SendAsync(newTagRequest, Token);

            Assert.Equal(HttpStatusCode.NotModified, withNewTag.StatusCode);
        }
        finally
        {
            await AetherisTestFactory.SetParameterAsync(services, TextKeys.PackCacheSeconds, previousLifetime ?? "0");
        }
    }

    /// <summary>
    /// The hash of the content of a pack depends on the keys, the texts and the versions, and not on
    /// the order the rows are read in.
    /// </summary>
    [Fact]
    public void The_hash_of_a_pack_depends_on_the_content_and_not_on_the_order()
    {
        LocalizationTextEntity first = new() { Key = "a.one", Language = "uk", Text = "Один", Version = 1 };
        LocalizationTextEntity second = new() { Key = "a.two", Language = "uk", Text = "Два", Version = 1 };
        LocalizationTextEntity renamed = new() { Key = "a.three", Language = "uk", Text = "Два", Version = 1 };
        LocalizationTextEntity edited = new() { Key = "a.two", Language = "uk", Text = "Два!", Version = 1 };

        string hash = LocalizationTextStore.ContentHash([first, second]);

        Assert.Equal(hash, LocalizationTextStore.ContentHash([second, first]));
        Assert.NotEqual(hash, LocalizationTextStore.ContentHash([first, renamed]));
        Assert.NotEqual(hash, LocalizationTextStore.ContentHash([first, edited]));
        Assert.NotEqual(hash, LocalizationTextStore.ContentHash([first, new LocalizationTextEntity { Key = "a.two", Language = "uk", Text = "Два", Version = 2 }]));
        Assert.Matches("^[0-9a-f]+$", hash);
    }

    private async Task PrepareAsync()
    {
        await ProcessesTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IMarketModule>().EnsureAsync(Token);
    }

    /// <summary>
    /// The keys a row holds when its code is the code given: a row that holds no key in a column
    /// (a characteristic without a unit) holds none after the change either.
    /// </summary>
    private static IReadOnlyList<string?> ExpectedKeys(ISeedCodeTable table, SeedCodeRowKeys row, string? code = null) =>
        [.. table.KeysOf(code ?? row.Code).Select((key, column) => row.Keys[column] is null ? null : key)];

    private static List<ISeedCodeTable> Tables(IServiceProvider services) =>
        [.. services.GetServices<ISeedCodeSource>().SelectMany(source => source.Tables)];

    private async Task<Dictionary<string, string[]>> CodesAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        Dictionary<string, string[]> codes = [];

        foreach (ISeedCodeTable table in Tables(scope.ServiceProvider))
        {
            codes[table.Table] = [.. (await table.ReadAsync(Token)).Select(row => row.Code)];
        }

        return codes;
    }

    /// <summary>
    /// Writes the state an older seeder left: the n-th row of every table has the code
    /// <c>group_n</c> and the keys built from it, and the texts follow the keys. The versions of the
    /// texts stay as they are, so the older pack differs from the new one by its keys only.
    /// </summary>
    private async Task MakeOlderStateAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();
        List<string> oldKeys = [];
        List<string> newKeys = [];

        foreach (ISeedCodeTable table in Tables(scope.ServiceProvider))
        {
            IReadOnlyList<SeedCodeRowKeys> rows = await table.ReadAsync(Token);
            List<SeedCodeRowChange> changes = [];

            for (int index = 0; index < rows.Count; index++)
            {
                string code = table.CodeGroup + "_" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                IReadOnlyList<string?> keys = ExpectedKeys(table, rows[index], code);

                for (int column = 0; column < keys.Count; column++)
                {
                    if (rows[index].Keys[column] is { } oldKey && keys[column] is { } newKey && oldKey != newKey)
                    {
                        oldKeys.Add(oldKey);
                        newKeys.Add(newKey);
                    }
                }

                changes.Add(new SeedCodeRowChange(rows[index].Id, code, keys));
            }

            await table.WriteAsync(changes, Token);
        }

        // Every key is renamed in one statement through a temporary name, so no pair meets another.
        string[] from = [.. oldKeys];
        string[] to = [.. newKeys];

        await dbContext.Database.ExecuteSqlAsync(
            $"UPDATE localization_text AS t SET key = 'tmp:' || t.key FROM unnest({from}) AS r(old_key) WHERE t.key = r.old_key",
            Token);
        await dbContext.Database.ExecuteSqlAsync(
            $"UPDATE localization_text AS t SET key = r.new_key FROM unnest({from}, {to}) AS r(old_key, new_key) WHERE t.key = 'tmp:' || r.old_key",
            Token);
    }

    private async Task<long> TextVersionSumAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AetherisDbContext>().LocalizationTexts.AsNoTracking()
            .SumAsync(text => (long)text.Version, Token);
    }

    private async Task<string> PackTagAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        LocalizationPack pack = await scope.ServiceProvider.GetRequiredService<ILocalizationTexts>().GetPackAsync("uk", Token);

        return pack.Version.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + pack.ContentHash;
    }

    private static async Task AssertEveryKeyHasTextsAsync(IServiceProvider services)
    {
        AetherisDbContext dbContext = services.GetRequiredService<AetherisDbContext>();
        IReadOnlyList<string> languages = services.GetRequiredService<ILocalizationTexts>().Languages;
        HashSet<string> keys = [];

        foreach (ISeedCodeTable table in Tables(services))
        {
            foreach (SeedCodeRowKeys row in await table.ReadAsync(Token))
            {
                keys.UnionWith(row.Keys.OfType<string>());
            }
        }

        List<string> keyList = [.. keys];
        Dictionary<string, int> languagesByKey = await dbContext.LocalizationTexts.AsNoTracking()
            .Where(text => keyList.Contains(text.Key))
            .GroupBy(text => text.Key)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Key, group => group.Count, Token);

        List<string> withoutText = [.. keys.Where(key => languagesByKey.GetValueOrDefault(key) != languages.Count)];

        Assert.True(withoutText.Count == 0, "Keys without a text in every language: " + string.Join(", ", withoutText.Take(10)));
    }
}
