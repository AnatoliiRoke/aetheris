using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The kinds of an item of an order of a load or an unload. They are logical codes, not numbers of
/// the balance (task 00057).
/// </summary>
public static class CargoItemKinds
{
    /// <summary>The item is an amount of a resource.</summary>
    public const string Resource = "resource";

    /// <summary>The item is a number of modules of one kind, mark and bonus.</summary>
    public const string Part = "part";
}

/// <summary>
/// One item of an order of a load or an unload (task 00057): what to carry and how much of it.
/// </summary>
/// <param name="Kind">The kind of the item, one of <see cref="CargoItemKinds"/>.</param>
/// <param name="Id">The resource or the kind of the part.</param>
/// <param name="MarkLevel">The mark level of a part; zero for a resource.</param>
/// <param name="HasRaceBonus">Whether a part carries the bonus of its race; always <see langword="false"/> for a resource.</param>
/// <param name="Amount">How much of the resource, or how many parts; above zero.</param>
public sealed record CargoItem(string Kind, int Id, int MarkLevel, bool HasRaceBonus, long Amount);

/// <summary>
/// The parameters of an order of a load or an unload (task 00057).
/// </summary>
/// <remarks>
/// The list of the items is kept in the document of the parameters of the order, under the name
/// <c>items</c>, next to what other orders keep there. An item that is not well formed makes the
/// whole list wrong, so a plan never carries an order that the fleet cannot read.
/// </remarks>
public static class CargoOrderParameters
{
    /// <summary>
    /// The name of the parameter that holds the list of the items.
    /// </summary>
    public const string ItemsName = "items";

    /// <summary>
    /// Reads the list of the items out of the document of the parameters of an order.
    /// </summary>
    /// <param name="parameters">The document of the parameters of the order.</param>
    /// <returns>
    /// The items, merged when the document names the same item twice, or <see langword="null"/> when the
    /// list is missing, is empty or has an item that is not well formed.
    /// </returns>
    public static IReadOnlyList<CargoItem>? ReadItems(string? parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
        {
            return null;
        }

        JsonNode? document;

        try
        {
            document = JsonNode.Parse(parameters);
        }
        catch (JsonException)
        {
            return null;
        }

        if (document is not JsonObject root || !root.TryGetPropertyValue(ItemsName, out JsonNode? node) || node is not JsonArray array || array.Count == 0)
        {
            return null;
        }

        List<CargoItem> items = [];

        foreach (JsonNode? entry in array)
        {
            CargoItem? item = ReadItem(entry as JsonObject);

            if (item is null)
            {
                return null;
            }

            int index = items.FindIndex(existing => Same(existing, item));

            if (index >= 0)
            {
                items[index] = items[index] with { Amount = items[index].Amount + item.Amount };
            }
            else
            {
                items.Add(item);
            }
        }

        return items;
    }

    /// <summary>
    /// Whether two items name the same thing to carry.
    /// </summary>
    /// <param name="first">The first item.</param>
    /// <param name="second">The second item.</param>
    /// <returns><see langword="true"/> when the kind, the identifier, the mark and the bonus are the same.</returns>
    public static bool Same(CargoItem first, CargoItem second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return first.Kind == second.Kind
            && first.Id == second.Id
            && first.MarkLevel == second.MarkLevel
            && first.HasRaceBonus == second.HasRaceBonus;
    }

    /// <summary>
    /// Reads one item.
    /// </summary>
    /// <param name="entry">The object of the item.</param>
    /// <returns>The item, or <see langword="null"/> when it is not well formed.</returns>
    private static CargoItem? ReadItem(JsonObject? entry)
    {
        if (entry is null
            || !entry.TryGetPropertyValue("kind", out JsonNode? kindNode)
            || kindNode is null
            || !entry.TryGetPropertyValue("id", out JsonNode? idNode)
            || idNode is null
            || !entry.TryGetPropertyValue("amount", out JsonNode? amountNode)
            || amountNode is null)
        {
            return null;
        }

        try
        {
            string kind = kindNode.GetValue<string>();
            int id = idNode.GetValue<int>();
            long amount = amountNode.GetValue<long>();

            if ((kind != CargoItemKinds.Resource && kind != CargoItemKinds.Part) || id <= 0 || amount <= 0)
            {
                return null;
            }

            if (kind == CargoItemKinds.Resource)
            {
                return new CargoItem(kind, id, 0, false, amount);
            }

            int mark = entry.TryGetPropertyValue("mark_level", out JsonNode? markNode) && markNode is not null ? markNode.GetValue<int>() : 1;
            bool bonus = entry.TryGetPropertyValue("has_race_bonus", out JsonNode? bonusNode) && bonusNode is not null && bonusNode.GetValue<bool>();

            return mark <= 0 ? null : new CargoItem(kind, id, mark, bonus, amount);
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException or JsonException)
        {
            return null;
        }
    }
}
