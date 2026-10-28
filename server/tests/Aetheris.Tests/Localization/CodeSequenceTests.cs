using Aetheris.Core.Data;
using Aetheris.Host.Data;
using Aetheris.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Localization;

/// <summary>
/// Tests of the neutral codes given by the server (task 00022, decision 1).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class CodeSequenceTests
{
    private const string Group = "test_group";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeSequenceTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public CodeSequenceTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The codes of a group follow each other; a raise below the counter changes nothing and a
    /// raise above it moves the next code past it.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_codes_follow_each_other_and_never_go_back()
    {
        await ResetAsync();

        using IServiceScope scope = _factory.Services.CreateScope();
        ICodeSequence codes = scope.ServiceProvider.GetRequiredService<ICodeSequence>();

        Assert.Equal("test_group_1", await codes.NextAsync(Group, Token));
        Assert.Equal("test_group_2", await codes.NextAsync(Group, Token));

        await codes.RaiseAsync(Group, 1, Token);
        Assert.Equal("test_group_3", await codes.NextAsync(Group, Token));

        await codes.RaiseAsync(Group, 10, Token);
        Assert.Equal("test_group_11", await codes.NextAsync(Group, Token));
        Assert.Equal("other_group_1", await codes.NextAsync("other_group", Token));
    }

    /// <summary>
    /// Twenty parallel requests, each in its own transaction, get twenty different codes with no
    /// gap (acceptance criterion 3).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Twenty_parallel_requests_get_twenty_different_codes()
    {
        const int requestCount = 20;

        await ResetAsync();

        string[] codes = await Task.WhenAll(Enumerable.Range(0, requestCount).Select(_ => Task.Run(
            async () =>
            {
                await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
                IDatabaseSession session = scope.ServiceProvider.GetRequiredService<IDatabaseSession>();

                await using IDatabaseTransaction transaction = await session.BeginTransactionAsync(Token);
                string code = await scope.ServiceProvider.GetRequiredService<ICodeSequence>().NextAsync(Group, Token);
                await transaction.CommitAsync(Token);

                return code;
            },
            Token)));

        Assert.Equal(requestCount, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            Enumerable.Range(1, requestCount).Select(number => EfCodeSequence.Format(Group, number)).Order(StringComparer.Ordinal),
            codes.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// A group is lower-case letters and underscores; anything else is refused before it reaches
    /// the database.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_group_with_other_characters_is_refused()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ICodeSequence codes = scope.ServiceProvider.GetRequiredService<ICodeSequence>();

        await Assert.ThrowsAsync<ArgumentException>(() => codes.NextAsync("Group-1", Token));
        await Assert.ThrowsAsync<ArgumentException>(() => codes.NextAsync("group'; --", Token));
    }

    private async Task ResetAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        await AetherisTestFactory.ResetDatabaseAsync(scope.ServiceProvider);
    }
}
