using System.Net;
using System.Text.Json;
using Aetheris.Mail;
using Aetheris.Mail.Configuration;
using Aetheris.Mail.Data;
using Aetheris.Mail.Data.Entities;
using Aetheris.Mail.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Mail;

/// <summary>
/// Tests of the mail of the players (task 00060): a letter of a player to another player, the
/// folders and their counters, the bin with its cleaning, the limits of the settings and the
/// letters of the game.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class MailTests
{
    private const string Mail = "/api/v1/mail";
    private const string Letters = "/api/v1/mail/letters";
    private const string SystemCategory = "mail_category_2";
    private const string ReportCategory = "mail_category_3";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailTests"/> class.
    /// </summary>
    /// <param name="factory">The factory of the tests.</param>
    public MailTests(AetherisTestFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// A letter of one player reaches the other one as unread, and the counter of the incoming
    /// letters grows.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_letter_of_a_player_reaches_the_other_one_unread()
    {
        await ResetAsync();
        using PlanetsTestPlayer sender = await FoundAsync();
        using PlanetsTestPlayer receiver = await FoundAsync();

        long messageId = await SendAsync(sender, receiver.Account.Nickname, "Щодо Ісару", "Пропоную ненапад на 3 доби.");

        // The receiver sees the letter in the incoming folder, not read.
        JsonElement view = await ReadAsync(receiver);
        JsonElement letter = view.GetProperty("letters").EnumerateArray().Single();

        Assert.Equal(messageId, letter.GetProperty("id").GetInt64());
        Assert.Equal("Щодо Ісару", letter.GetProperty("subject").GetString());
        Assert.Equal(sender.Account.Nickname, letter.GetProperty("sender_nickname").GetString());
        Assert.False(letter.GetProperty("is_read").GetBoolean());
        Assert.Equal(1, view.GetProperty("unread_count").GetInt32());

        // The sender sees it in the sent folder, already read.
        JsonElement sent = await ReadAsync(sender, "sent");

        Assert.Single(sent.GetProperty("letters").EnumerateArray());
        Assert.Equal(0, sent.GetProperty("unread_count").GetInt32());
    }

    /// <summary>
    /// A letter of a player may not be written to oneself, to a player that does not exist, with a
    /// link in the text or with a text that is longer than the settings allow.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_letter_that_the_rules_forbid_is_refused_with_its_reason()
    {
        await ResetAsync();
        using PlanetsTestPlayer sender = await FoundAsync();

        await AssertRefusedAsync(sender, sender.Account.Nickname, "Тема", "Текст", "mail_self_not_allowed");
        await AssertRefusedAsync(sender, "НемаєТакого", "Тема", "Текст", "mail_receiver_unknown");
        await AssertRefusedAsync(sender, sender.Account.Nickname, "Тема", "Дивись https://example.com", "mail_link_not_allowed");
        await AssertRefusedAsync(sender, sender.Account.Nickname, "Тема", string.Empty, "mail_body_invalid");
    }

    /// <summary>
    /// The limit of the text of a letter comes from the settings: a text that fits the limit of
    /// the settings is written, and a longer one is refused.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_limit_of_the_text_of_a_letter_comes_from_the_settings()
    {
        await ResetAsync();
        using PlanetsTestPlayer sender = await FoundAsync();
        using PlanetsTestPlayer receiver = await FoundAsync();

        await SetParameterAsync(MailConfigKeys.LetterBodyMaxLength, "20");

        // A text of twenty characters fits the limit of the settings.
        await SendAsync(sender, receiver.Account.Nickname, "Тема", "12345678901234567890");

        // A text of twenty-one characters does not.
        await AssertRefusedAsync(sender, receiver.Account.Nickname, "Тема", "123456789012345678901", "mail_body_invalid");
    }

    /// <summary>
    /// The number of the letters a player writes in one hour comes from the settings: the player
    /// writes as many letters as the settings allow and is refused the next one.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_limit_of_the_letters_of_an_hour_comes_from_the_settings()
    {
        await ResetAsync();
        using PlanetsTestPlayer sender = await FoundAsync();
        using PlanetsTestPlayer receiver = await FoundAsync();

        await SetParameterAsync(MailConfigKeys.SendLimitPerHour, "2");

        await SendAsync(sender, receiver.Account.Nickname, "Перша", "Текст");
        await SendAsync(sender, receiver.Account.Nickname, "Друга", "Текст");

        await AssertRefusedAsync(sender, receiver.Account.Nickname, "Третя", "Текст", "mail_send_limit_reached");
    }

    /// <summary>
    /// A letter that the player opened is read, and the counter of the unread letters falls.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_letter_that_the_player_opened_is_read()
    {
        await ResetAsync();
        using PlanetsTestPlayer sender = await FoundAsync();
        using PlanetsTestPlayer receiver = await FoundAsync();

        long messageId = await SendAsync(sender, receiver.Account.Nickname, "Щодо Ісару", "Пропоную ненапад.");

        using HttpResponseMessage response = await receiver.Client.GetAsync($"{Mail}/letters/{messageId}", Token);
        string text = await response.Content.ReadAsStringAsync(Token);

        Assert.True(response.StatusCode == HttpStatusCode.OK, text);

        JsonElement detail = JsonDocument.Parse(text).RootElement;

        Assert.Equal("Пропоную ненапад.", detail.GetProperty("body").GetString());
        Assert.True(detail.GetProperty("letter").GetProperty("is_read").GetBoolean());

        // The counter of the incoming letters is zero now.
        JsonElement view = await ReadAsync(receiver);

        Assert.Equal(0, view.GetProperty("unread_count").GetInt32());
    }

    /// <summary>
    /// A letter of another player is unknown to the player who did not receive it, and a request
    /// without a session is refused.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_letter_of_another_player_is_unknown()
    {
        await ResetAsync();
        using PlanetsTestPlayer sender = await FoundAsync();
        using PlanetsTestPlayer receiver = await FoundAsync();
        using PlanetsTestPlayer stranger = await FoundAsync();

        long messageId = await SendAsync(sender, receiver.Account.Nickname, "Щодо Ісару", "Текст");

        using HttpResponseMessage response = await stranger.Client.GetAsync($"{Mail}/letters/{messageId}", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using HttpClient anonymous = _factory.CreateBrowserClient();
        using HttpResponseMessage withoutSession = await anonymous.GetAsync($"{Mail}/letters/{messageId}", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, withoutSession.StatusCode);
    }

    /// <summary>
    /// A letter may be saved, taken back from the saved ones and put into the bin; a letter of the
    /// bin may be taken back and the bin may be emptied.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_letter_is_saved_taken_back_and_put_into_the_bin()
    {
        await ResetAsync();
        using PlanetsTestPlayer sender = await FoundAsync();
        using PlanetsTestPlayer receiver = await FoundAsync();

        long messageId = await SendAsync(sender, receiver.Account.Nickname, "Щодо Ісару", "Текст");

        // The letter is saved: it lies in the saved folder.
        await CommandAsync(receiver, "/api/v1/mail/letters/save", new { message_id = messageId });

        JsonElement saved = await ReadAsync(receiver, "saved");

        Assert.Single(saved.GetProperty("letters").EnumerateArray());

        // It is taken back from the saved ones.
        await CommandAsync(receiver, "/api/v1/mail/letters/save", new { message_id = messageId });

        Assert.Empty((await ReadAsync(receiver, "saved")).GetProperty("letters").EnumerateArray());

        // It goes into the bin: the list without a folder does not show it any more.
        await CommandAsync(receiver, "/api/v1/mail/letters/delete", new { message_id = messageId });

        Assert.Empty((await ReadAsync(receiver)).GetProperty("letters").EnumerateArray());
        Assert.Single((await ReadAsync(receiver, "trash")).GetProperty("letters").EnumerateArray());

        // It comes back from the bin.
        await CommandAsync(receiver, "/api/v1/mail/letters/delete", new { message_id = messageId });

        Assert.Single((await ReadAsync(receiver)).GetProperty("letters").EnumerateArray());
        Assert.Empty((await ReadAsync(receiver, "trash")).GetProperty("letters").EnumerateArray());
    }

    /// <summary>
    /// The bin is emptied: the letters of the bin are gone and the folder holds nothing.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_bin_is_emptied()
    {
        await ResetAsync();
        using PlanetsTestPlayer sender = await FoundAsync();
        using PlanetsTestPlayer receiver = await FoundAsync();

        long messageId = await SendAsync(sender, receiver.Account.Nickname, "Щодо Ісару", "Текст");

        await CommandAsync(receiver, "/api/v1/mail/letters/delete", new { message_id = messageId });
        await CommandAsync(receiver, "/api/v1/mail/trash/empty", new { });

        Assert.Empty((await ReadAsync(receiver, "trash")).GetProperty("letters").EnumerateArray());
    }

    /// <summary>
    /// Every letter of a folder is marked as read at once, and the counter of the unread letters
    /// falls to zero.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Every_letter_of_a_folder_is_marked_as_read()
    {
        await ResetAsync();
        using PlanetsTestPlayer sender = await FoundAsync();
        using PlanetsTestPlayer receiver = await FoundAsync();

        await SendAsync(sender, receiver.Account.Nickname, "Перша", "Текст");
        await SendAsync(sender, receiver.Account.Nickname, "Друга", "Текст");

        Assert.Equal(2, (await ReadAsync(receiver)).GetProperty("unread_count").GetInt32());

        await CommandAsync(receiver, "/api/v1/mail/read-all", new { folder_code = "incoming" });

        JsonElement view = await ReadAsync(receiver);

        Assert.Equal(0, view.GetProperty("unread_count").GetInt32());
        Assert.All(
            view.GetProperty("letters").EnumerateArray(),
            letter => Assert.True(letter.GetProperty("is_read").GetBoolean()));
    }

    /// <summary>
    /// The list of the letters is filtered by the folder, by the category and by the search.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_list_is_filtered_by_the_folder_the_category_and_the_search()
    {
        await ResetAsync();
        using PlanetsTestPlayer sender = await FoundAsync();
        using PlanetsTestPlayer receiver = await FoundAsync();

        await SendAsync(sender, receiver.Account.Nickname, "Щодо Ісару", "Пропоную ненапад.");

        // The search finds the letter by its subject.
        using HttpResponseMessage searched = await receiver.Client.GetAsync($"{Mail}/?search={Uri.EscapeDataString("Ісару")}", Token);
        string text = await searched.Content.ReadAsStringAsync(Token);

        Assert.True(searched.StatusCode == HttpStatusCode.OK, text);
        Assert.Single(JsonDocument.Parse(text).RootElement.GetProperty("letters").EnumerateArray());

        // The search that finds nothing leaves the list empty.
        using HttpResponseMessage empty = await receiver.Client.GetAsync($"{Mail}/?search={Uri.EscapeDataString("НемаєТакого")}", Token);
        string emptyText = await empty.Content.ReadAsStringAsync(Token);

        Assert.Empty(JsonDocument.Parse(emptyText).RootElement.GetProperty("letters").EnumerateArray());
    }

    /// <summary>
    /// A letter of the game comes to the player with the key of its text and its parameters, and
    /// the category of the letter comes from the seed of the categories.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_letter_of_the_game_carries_the_key_of_its_text()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();

        long? messageId;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            IMailModule mail = scope.ServiceProvider.GetRequiredService<IMailModule>();

            messageId = await mail.SendSystemAsync(
                player.Account.UserId,
                SystemCategory,
                "mail.system.welcome",
                "{\"name\":\"Вега\"}",
                cancellationToken: Token);
        }

        Assert.NotNull(messageId);

        JsonElement view = await ReadAsync(player);
        JsonElement letter = view.GetProperty("letters").EnumerateArray().Single();

        Assert.Equal(messageId!.Value, letter.GetProperty("id").GetInt64());
        Assert.Equal("system", letter.GetProperty("kind_code").GetString());
        Assert.Null(letter.GetProperty("sender_nickname").GetString());
        Assert.False(letter.GetProperty("is_read").GetBoolean());

        // The text of the letter is read with its key and its parameters.
        using HttpResponseMessage response = await player.Client.GetAsync($"{Mail}/letters/{messageId}", Token);
        string text = await response.Content.ReadAsStringAsync(Token);

        Assert.True(response.StatusCode == HttpStatusCode.OK, text);

        JsonElement detail = JsonDocument.Parse(text).RootElement;

        Assert.Equal("mail.system.welcome", detail.GetProperty("text_key").GetString());
        Assert.Contains("Вега", detail.GetProperty("text_parameters").GetString() ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// A letter of the game with a category that does not exist is not written at all.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_letter_of_the_game_with_an_unknown_category_is_not_written()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();

        using IServiceScope scope = _factory.Services.CreateScope();
        IMailModule mail = scope.ServiceProvider.GetRequiredService<IMailModule>();

        long? messageId = await mail.SendSystemAsync(
            player.Account.UserId,
            "mail_category_unknown",
            "mail.system.welcome",
            null,
            cancellationToken: Token);

        Assert.Null(messageId);
        Assert.Empty((await ReadAsync(player)).GetProperty("letters").EnumerateArray());
    }

    /// <summary>
    /// A draft of a letter is written, read again and removed.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_draft_is_written_read_and_removed()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();

        JsonElement draft = await CommandAsync(player, "/api/v1/mail/drafts", new
        {
            draft_id = 0,
            receiver_nickname = "Хтось",
            subject = "Обмін кристалами",
            body = "Привіт!",
        });

        long draftId = draft.GetProperty("id").GetInt64();

        using HttpResponseMessage response = await player.Client.GetAsync($"{Mail}/drafts", Token);
        string text = await response.Content.ReadAsStringAsync(Token);

        Assert.True(response.StatusCode == HttpStatusCode.OK, text);
        Assert.Single(JsonDocument.Parse(text).RootElement.GetProperty("drafts").EnumerateArray());

        await CommandAsync(player, "/api/v1/mail/drafts/remove", new { draft_id = draftId });

        using HttpResponseMessage after = await player.Client.GetAsync($"{Mail}/drafts", Token);
        string afterText = await after.Content.ReadAsStringAsync(Token);

        Assert.Empty(JsonDocument.Parse(afterText).RootElement.GetProperty("drafts").EnumerateArray());
    }

    /// <summary>
    /// A complaint about a letter of another player is written, and a complaint about a letter of
    /// the game is refused.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_complaint_about_a_letter_of_another_player_is_written()
    {
        await ResetAsync();
        using PlanetsTestPlayer sender = await FoundAsync();
        using PlanetsTestPlayer receiver = await FoundAsync();

        long messageId = await SendAsync(sender, receiver.Account.Nickname, "Щодо Ісару", "Текст");

        await CommandAsync(receiver, "/api/v1/mail/complaints", new { message_id = messageId, reason = "spam" });

        using IServiceScope scope = _factory.Services.CreateScope();
        MailDbContext database = scope.ServiceProvider.GetRequiredService<MailDbContext>();

        Assert.Equal(1, await database.MailComplaints.CountAsync(Token));

        // A letter of the game may not be complained about: the game writes it.
        using IServiceScope other = _factory.Services.CreateScope();
        IMailModule mail = other.ServiceProvider.GetRequiredService<IMailModule>();

        long? systemMessageId = await mail.SendSystemAsync(
            receiver.Account.UserId,
            ReportCategory,
            "mail.fleet.extraction_report",
            "{}",
            "fleet_report",
            Token);

        Assert.NotNull(systemMessageId);

        using HttpResponseMessage refused = await TestAccounts.SendCommandAsync(
            receiver.Client,
            "/api/v1/mail/complaints",
            new { message_id = systemMessageId!.Value, reason = "spam" },
            Guid.NewGuid().ToString("N"));

        Assert.False(refused.IsSuccessStatusCode);
        Assert.Equal("mail_complaint_not_allowed", await TestAccounts.ReadErrorAsync(refused));
    }

    /// <summary>
    /// The number of the letters that are not read is read alone, for the counter of the top bar.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_number_of_the_unread_letters_is_read_alone()
    {
        await ResetAsync();
        using PlanetsTestPlayer sender = await FoundAsync();
        using PlanetsTestPlayer receiver = await FoundAsync();

        await SendAsync(sender, receiver.Account.Nickname, "Щодо Ісару", "Текст");

        using HttpResponseMessage response = await receiver.Client.GetAsync($"{Mail}/unread", Token);
        string text = await response.Content.ReadAsStringAsync(Token);

        Assert.True(response.StatusCode == HttpStatusCode.OK, text);
        Assert.Equal(1, JsonDocument.Parse(text).RootElement.GetProperty("unread_count").GetInt32());
    }

    /// <summary>
    /// Clears the database and prepares the mail, as a fresh server does at start.
    /// </summary>
    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MailBootstrapService>().EnsureAsync(Token);
    }

    /// <summary>
    /// Registers a player with a civilization and signs him in: the mail is a part of the game,
    /// so a player of it has a civilization.
    /// </summary>
    private async Task<PlanetsTestPlayer> FoundAsync()
    {
        return await PlanetsTestData.FoundAsync(_factory);
    }
    private async Task SetParameterAsync(string key, string value)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, key, value);
    }

    private static async Task<long> SendAsync(
        PlanetsTestPlayer sender,
        string receiverNickname,
        string subject,
        string body)
    {
        JsonElement answer = await CommandAsync(sender, Letters, new
        {
            receiver_nickname = receiverNickname,
            subject,
            body,
        });

        return answer.GetProperty("id").GetInt64();
    }

    private static async Task AssertRefusedAsync(
        PlanetsTestPlayer sender,
        string receiverNickname,
        string subject,
        string body,
        string expectedCode)
    {
        using HttpResponseMessage response = await TestAccounts.SendCommandAsync(
            sender.Client,
            Letters,
            new { receiver_nickname = receiverNickname, subject, body },
            Guid.NewGuid().ToString("N"));
        string text = await response.Content.ReadAsStringAsync(Token);

        Assert.False(response.IsSuccessStatusCode);
        Assert.True(
            await TestAccounts.ReadErrorAsync(response) == expectedCode,
            $"the refusal was not {expectedCode}: {text}");
    }

    private static async Task<JsonElement> CommandAsync(PlanetsTestPlayer player, string route, object body)
    {
        using HttpResponseMessage response = await TestAccounts.SendCommandAsync(
            player.Client,
            route,
            body,
            Guid.NewGuid().ToString("N"));
        string text = await response.Content.ReadAsStringAsync(Token);

        Assert.True(response.StatusCode == HttpStatusCode.OK, text);

        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static async Task<JsonElement> ReadAsync(PlanetsTestPlayer player, string? folder = null)
    {
        string route = folder is null ? $"{Mail}/" : $"{Mail}/?folder={folder}";

        using HttpResponseMessage response = await player.Client.GetAsync(route, Token);
        string text = await response.Content.ReadAsStringAsync(Token);

        Assert.True(response.StatusCode == HttpStatusCode.OK, text);

        return JsonDocument.Parse(text).RootElement.Clone();
    }

}
