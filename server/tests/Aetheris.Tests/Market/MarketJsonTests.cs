using System.Text.Json;
using Aetheris.Market.Api;

namespace Aetheris.Tests.Market;

/// <summary>
/// Tests of the JSON of the market (task 00026): the client reads an empty field as `null`, so the
/// server must send it and never leave it out.
/// </summary>
public sealed class MarketJsonTests
{
    /// <summary>
    /// A lot without a reason to refuse it still carries the field of the reason, as null.
    /// </summary>
    [Fact]
    public void An_empty_field_is_sent_as_null_and_the_names_are_snake_case()
    {
        LotListItemDto lot = new(
            1,
            new PlayerCardDto("p", "Nick", null, null),
            2,
            3,
            4,
            null,
            5,
            6,
            30,
            DateTimeOffset.UnixEpoch,
            null,
            null,
            null,
            null);

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(lot, MarketJson.Options));
        JsonElement root = document.RootElement;

        Assert.Equal(JsonValueKind.Null, root.GetProperty("unavailable_reason").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("delivery_seconds").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("part_type_id").ValueKind);
        Assert.Equal("Nick", root.GetProperty("seller").GetProperty("nickname").GetString());
    }
}
