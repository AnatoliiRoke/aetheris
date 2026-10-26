using System.Text.Json;
using Aetheris.Host.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Data;

/// <summary>
/// Fills the lists of the account rules in a fresh database (task 00006): the actions allowed
/// before the e-mail confirmation, the blocked domains of temporary mailboxes and the
/// restricted words of the visible names.
/// </summary>
/// <remarks>
/// The starting lists live in the embedded file <c>Data/account-lists-seed.json</c>, not in
/// the code. A list is written only while its table is empty, so the seed never overwrites
/// or restores what an administrator changed later.
/// </remarks>
public static class AccountListsSeed
{
    /// <summary>
    /// The name of the embedded starting lists.
    /// </summary>
    public const string StreamName = "Aetheris.Host.account-lists-seed.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Reads the starting lists.
    /// </summary>
    /// <returns>The starting lists.</returns>
    public static AccountListsSeedData Load()
    {
        using Stream stream = typeof(AccountListsSeed).Assembly.GetManifestResourceStream(StreamName)
            ?? throw new InvalidOperationException($"The embedded starting data {StreamName} is missing.");

        return JsonSerializer.Deserialize<AccountListsSeedData>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"The embedded starting data {StreamName} is empty.");
    }

    /// <summary>
    /// Writes every list whose table is empty.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the lists are written.</returns>
    public static async Task SeedAsync(AetherisDbContext dbContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        AccountListsSeedData seed = Load();

        if (!await dbContext.UnconfirmedAllowedActions.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            dbContext.UnconfirmedAllowedActions.AddRange(seed.UnconfirmedAllowedActions
                .Select(code => new UnconfirmedAllowedActionEntity { ActionCode = code }));
        }

        if (!await dbContext.BlockedEmailDomains.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            dbContext.BlockedEmailDomains.AddRange(seed.BlockedEmailDomains
                .Select(domain => new BlockedEmailDomainEntity { Domain = domain.ToLowerInvariant() }));
        }

        if (!await dbContext.RestrictedWords.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            dbContext.RestrictedWords.AddRange(seed.RestrictedWords
                .Select(entry => new RestrictedWordEntity { Word = entry.Word.ToLowerInvariant(), Kind = entry.Kind }));
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>The starting lists of the account rules.</summary>
/// <param name="UnconfirmedAllowedActions">The action codes allowed before the e-mail confirmation.</param>
/// <param name="BlockedEmailDomains">The domains of temporary mailboxes.</param>
/// <param name="RestrictedWords">The restricted words.</param>
public sealed record AccountListsSeedData(
    IReadOnlyList<string> UnconfirmedAllowedActions,
    IReadOnlyList<string> BlockedEmailDomains,
    IReadOnlyList<SeedRestrictedWord> RestrictedWords);

/// <summary>A restricted word of the starting lists.</summary>
/// <param name="Word">The word.</param>
/// <param name="Kind">The kind, one of <see cref="RestrictedWordKinds"/>.</param>
public sealed record SeedRestrictedWord(string Word, string Kind);
