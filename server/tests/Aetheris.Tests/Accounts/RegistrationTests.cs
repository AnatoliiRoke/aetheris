using System.Net;
using System.Net.Http.Json;
using Aetheris.Core.Errors;
using Aetheris.Host.Auth;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Host.Security;
using Aetheris.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aetheris.Tests.Accounts;

/// <summary>
/// Tests of the registration (task 00006, acceptance criteria 1, 3 and 7).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class RegistrationTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistrationTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public RegistrationTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A registration creates the account and sends the confirmation link; the link works
    /// once and lives the configured 24 hours.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_registration_sends_a_one_time_confirmation_link()
    {
        TestAccount account = await TestAccounts.RegisterAsync(_factory);
        AccountEmail email = _factory.Emails.Last(account.Email, AccountEmailKinds.Confirmation);

        using HttpClient client = _factory.CreateBrowserClient();
        object body = new { userId = email.UserId, token = email.Token };

        HttpResponseMessage first = await client.PostAsJsonAsync(AuthEndpoints.Prefix + "/confirm-email", body, Token);
        HttpResponseMessage second = await client.PostAsJsonAsync(AuthEndpoints.Prefix + "/confirm-email", body, Token);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(GameErrorCodes.InvalidToken, await TestAccounts.ReadErrorAsync(second));

        using IServiceScope scope = _factory.Services.CreateScope();
        SecuritySettings settings = scope.ServiceProvider.GetRequiredService<SecuritySettings>();
        ConfirmationTokenProviderOptions options = scope.ServiceProvider.GetRequiredService<IOptions<ConfirmationTokenProviderOptions>>().Value;

        Assert.Equal(TimeSpan.FromHours(24), settings.ConfirmationLifetime);
        Assert.Equal(settings.ConfirmationLifetime, options.TokenLifespan);
    }

    /// <summary>
    /// The server checks the repeated password on its own.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_different_repeated_password_is_refused()
    {
        using HttpClient client = _factory.CreateBrowserClient();
        HttpResponseMessage response = await AccountTestSupport.RegisterAsync(
            client,
            TestAccounts.NewEmail(),
            TestAccounts.NewNickname(),
            TestAccounts.StrongPassword,
            TestAccounts.StrongPassword + "x");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(RegistrationService.PasswordMismatch, await AccountTestSupport.FieldErrorsAsync(response, nameof(RegisterRequest.PasswordConfirmation)));
    }

    /// <summary>
    /// The password rules at their edges: 7 and 8 characters, no digit, no upper-case letter
    /// and a common password.
    /// </summary>
    /// <param name="password">The password.</param>
    /// <param name="expectedCode">The refused rule, or an empty string when the password is accepted.</param>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Theory]
    [InlineData("Qwx7rtz", ConfigPasswordValidator.TooShort)]
    [InlineData("Qwx7rtzk", "")]
    [InlineData("Qwxrtzkm", ConfigPasswordValidator.NoDigit)]
    [InlineData("qwx7rtzk", ConfigPasswordValidator.NoUppercase)]
    [InlineData("Password1", ConfigPasswordValidator.Common)]
    public async Task The_password_rules_hold_at_their_edges(string password, string expectedCode)
    {
        using HttpClient client = _factory.CreateBrowserClient();
        HttpResponseMessage response = await AccountTestSupport.RegisterAsync(client, TestAccounts.NewEmail(), TestAccounts.NewNickname(), password);

        if (expectedCode.Length == 0)
        {
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            return;
        }

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedCode, await AccountTestSupport.FieldErrorsAsync(response, nameof(RegisterRequest.Password)));
    }

    /// <summary>
    /// The length of a nickname at its edges: 2, 3, 20 and 21 characters.
    /// </summary>
    /// <param name="length">The length.</param>
    /// <param name="expectedCode">The refused rule, or an empty string when the nickname is accepted.</param>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Theory]
    [InlineData(2, NicknameRules.TooShort)]
    [InlineData(3, "")]
    [InlineData(20, "")]
    [InlineData(21, NicknameRules.TooLong)]
    public async Task The_nickname_length_holds_at_its_edges(int length, string expectedCode)
    {
        await AssertNicknameAsync(AccountTestSupport.NicknameOfLength(length), expectedCode);
    }

    /// <summary>
    /// The characters, the restricted words and the reserved names of a nickname.
    /// </summary>
    /// <param name="nickname">The nickname.</param>
    /// <param name="expectedCode">The refused rule, or an empty string when the nickname is accepted.</param>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Theory]
    [InlineData("Bad!Name", NicknameRules.InvalidCharacters)]
    [InlineData("Admin_Bob", NicknameRules.Restricted)]
    [InlineData("AdminBob", NicknameRules.Restricted)]
    [InlineData("xx-FUCK-xx", NicknameRules.Restricted)]
    [InlineData("Пілот-Сука", NicknameRules.Restricted)]
    public async Task The_nickname_characters_and_words_are_checked(string nickname, string expectedCode)
    {
        await AssertNicknameAsync(nickname, expectedCode);
    }

    /// <summary>
    /// Latin and Cyrillic letters, digits, the space, the underscore and the dash are allowed.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_nickname_of_the_allowed_characters_is_accepted()
    {
        string suffix = Guid.NewGuid().ToString("N")[..4];

        await AssertNicknameAsync($"Їжак_{suffix} Ok-1", string.Empty);
    }

    /// <summary>
    /// A nickname is unique without regard to case.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_nickname_is_unique_without_regard_to_case()
    {
        string nickname = "CaseName" + Guid.NewGuid().ToString("N")[..6];

        await AssertNicknameAsync(nickname, string.Empty);
        await AssertNicknameAsync(nickname.ToLowerInvariant(), NicknameRules.Taken);
        await AssertNicknameAsync(nickname.ToUpperInvariant(), NicknameRules.Taken);
    }

    /// <summary>
    /// A domain of temporary mailboxes from the database list may not register.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_temporary_mailbox_domain_is_refused()
    {
        using HttpClient client = _factory.CreateBrowserClient();
        HttpResponseMessage response = await AccountTestSupport.RegisterAsync(
            client,
            $"someone-{Guid.NewGuid():N}@Mailinator.com",
            TestAccounts.NewNickname(),
            TestAccounts.StrongPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(RegistrationService.EmailDomainBlocked, await AccountTestSupport.FieldErrorsAsync(response, nameof(RegisterRequest.Email)));
    }

    /// <summary>
    /// One address may register only the configured number of accounts per window; the
    /// registration and every sign-in are kept with the address and the moment.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_registrations_of_one_address_are_limited_and_journaled()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

        int existing = await dbContext.AccountAccessLog.CountAsync(entry => entry.Kind == AccountAccessKinds.Registration, Token);
        string limit = (existing + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        string? previous = await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, SecurityKeys.RegistrationIpLimit, limit);

        try
        {
            TestAccount account = await TestAccounts.RegisterAsync(_factory);

            using HttpClient client = _factory.CreateBrowserClient();
            HttpResponseMessage refused = await AccountTestSupport.RegisterAsync(client, TestAccounts.NewEmail(), TestAccounts.NewNickname(), TestAccounts.StrongPassword);

            Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
            Assert.Equal(AuthEndpoints.RegistrationLimitReached, await TestAccounts.ReadErrorAsync(refused));

            Assert.Equal(HttpStatusCode.OK, (await AccountTestSupport.LoginAsync(client, account.Email, account.Password)).StatusCode);

            List<AccountAccessLogEntity> entries = await dbContext.AccountAccessLog.AsNoTracking()
                .Where(entry => entry.UserId == account.UserId)
                .OrderBy(entry => entry.Id)
                .ToListAsync(Token);

            Assert.Equal([AccountAccessKinds.Registration, AccountAccessKinds.SignIn], entries.Select(entry => entry.Kind));
            Assert.All(entries, entry => Assert.True(entry.At > DateTimeOffset.UtcNow.AddMinutes(-1)));
        }
        finally
        {
            await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, SecurityKeys.RegistrationIpLimit, previous!);
        }
    }

    private async Task AssertNicknameAsync(string nickname, string expectedCode)
    {
        using HttpClient client = _factory.CreateBrowserClient();
        HttpResponseMessage response = await AccountTestSupport.RegisterAsync(client, TestAccounts.NewEmail(), nickname, TestAccounts.StrongPassword);

        if (expectedCode.Length == 0)
        {
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            return;
        }

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedCode, await AccountTestSupport.FieldErrorsAsync(response, nameof(RegisterRequest.Nickname)));
    }
}
