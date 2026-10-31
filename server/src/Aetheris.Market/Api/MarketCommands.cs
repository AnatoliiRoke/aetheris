using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Core.Commands;
using Aetheris.Market.Services;
using Aetheris.World;
using FluentValidation;

namespace Aetheris.Market.Api;

/// <summary>
/// Puts goods of a body up for sale (task 00024, section 4.1).
/// </summary>
public sealed class ListLotCommand : Command<ListLotCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/market/orders";

    /// <summary>The action code of the command.</summary>
    public const string Code = "market.list_lot";

    private readonly MarketTradeService _trade;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListLotCommand"/> class.
    /// </summary>
    /// <param name="trade">The commands of the market.</param>
    public ListLotCommand(MarketTradeService trade)
    {
        ArgumentNullException.ThrowIfNull(trade);

        _trade = trade;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.PlanetId).NotNull().GreaterThan(0);
        rules.RuleFor(request => request.MarketLevelId).NotNull().GreaterThan(0);
        rules.RuleFor(request => request.Quantity).NotNull().GreaterThan(0);
        rules.RuleFor(request => request.PricePerUnit).NotNull().GreaterThanOrEqualTo(0);
        rules.RuleFor(request => request.ResourceTypeId).GreaterThan(0).When(request => request.ResourceTypeId is not null);
        rules.RuleFor(request => request.PartTypeId).GreaterThan(0).When(request => request.PartTypeId is not null);
        rules.RuleFor(request => request).Must(request => (request.ResourceTypeId is null) != (request.PartTypeId is null))
            .WithMessage("Name a resource or a part, not both.");
    }

    /// <inheritdoc />
    public override OwnedObjectReference? GetTarget(Request request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new(PlanetOwnerResolver.Type, request.PlanetId!.Value.ToString(CultureInfo.InvariantCulture));
    }

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyList<LotDto> lots = await _trade
            .ListLotAsync(
                context.PlayerId,
                new ListLotRequest(
                    request.PlanetId!.Value,
                    request.MarketLevelId!.Value,
                    request.ResourceTypeId,
                    request.PartTypeId,
                    request.Quantity!.Value,
                    request.PricePerUnit!.Value,
                    request.PartConditionPercent),
                cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(lots, MarketJson.Options);
    }

    /// <summary>
    /// The request: the body, the level, the item, the quantity and the price of a unit.
    /// </summary>
    /// <param name="PlanetId">The body whose stock the goods leave.</param>
    /// <param name="MarketLevelId">The level of the market.</param>
    /// <param name="Quantity">How many units.</param>
    /// <param name="PricePerUnit">The price of a unit in the trade currency.</param>
    /// <param name="ResourceTypeId">The resource, or <see langword="null"/> for a part.</param>
    /// <param name="PartTypeId">The part, or <see langword="null"/> for a resource.</param>
    /// <param name="PartConditionPercent">The condition of the parts to sell; new parts when empty.</param>
    public sealed record Request(
        [property: JsonPropertyName("planet_id")] long? PlanetId,
        [property: JsonPropertyName("market_level_id")] int? MarketLevelId,
        [property: JsonPropertyName("quantity")] long? Quantity,
        [property: JsonPropertyName("price_per_unit")] long? PricePerUnit,
        [property: JsonPropertyName("resource_type_id")] int? ResourceTypeId = null,
        [property: JsonPropertyName("part_type_id")] int? PartTypeId = null,
        [property: JsonPropertyName("part_condition_percent")] int? PartConditionPercent = null);
}

/// <summary>
/// Takes a lot of the player off the market (task 00024, section 4.1).
/// </summary>
public sealed class CancelLotCommand : Command<CancelLotCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/market/orders/cancel";

    /// <summary>The action code of the command.</summary>
    public const string Code = "market.cancel_lot";

    private readonly MarketTradeService _trade;

    /// <summary>
    /// Initializes a new instance of the <see cref="CancelLotCommand"/> class.
    /// </summary>
    /// <param name="trade">The commands of the market.</param>
    public CancelLotCommand(MarketTradeService trade)
    {
        ArgumentNullException.ThrowIfNull(trade);

        _trade = trade;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.LotId).NotNull().GreaterThan(0);
    }

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        LotDto lot = await _trade.CancelLotAsync(context.PlayerId, request.LotId!.Value, cancellationToken).ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(lot, MarketJson.Options);
    }

    /// <summary>
    /// The request: the lot. The seller is checked from the lot itself.
    /// </summary>
    /// <param name="LotId">The lot.</param>
    public sealed record Request([property: JsonPropertyName("lot_id")] long? LotId);
}

/// <summary>
/// Buys a whole lot and sends the goods to a body of the buyer (task 00024, section 4.1).
/// </summary>
public sealed class BuyLotCommand : Command<BuyLotCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/market/orders/buy";

    /// <summary>The action code of the command.</summary>
    public const string Code = "market.buy_lot";

    private readonly MarketTradeService _trade;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuyLotCommand"/> class.
    /// </summary>
    /// <param name="trade">The commands of the market.</param>
    public BuyLotCommand(MarketTradeService trade)
    {
        ArgumentNullException.ThrowIfNull(trade);

        _trade = trade;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.LotId).NotNull().GreaterThan(0);
        rules.RuleFor(request => request.DestinationPlanetId).NotNull().GreaterThan(0);
    }

    /// <inheritdoc />
    public override OwnedObjectReference? GetTarget(Request request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new(PlanetOwnerResolver.Type, request.DestinationPlanetId!.Value.ToString(CultureInfo.InvariantCulture));
    }

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        DealDto deal = await _trade
            .BuyAsync(context.PlayerId, request.LotId!.Value, request.DestinationPlanetId!.Value, cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(deal, MarketJson.Options);
    }

    /// <summary>
    /// The request: the lot and the body of the buyer the goods arrive at.
    /// </summary>
    /// <param name="LotId">The lot.</param>
    /// <param name="DestinationPlanetId">The body of the buyer.</param>
    public sealed record Request(
        [property: JsonPropertyName("lot_id")] long? LotId,
        [property: JsonPropertyName("destination_planet_id")] long? DestinationPlanetId);
}

/// <summary>
/// Swaps credits for the trade currency or the trade currency for credits (task 00024,
/// section 4.1).
/// </summary>
public sealed class ExchangeCommand : Command<ExchangeCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/exchange";

    /// <summary>The action code of the command.</summary>
    public const string Code = "market.exchange";

    private readonly MarketTradeService _trade;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExchangeCommand"/> class.
    /// </summary>
    /// <param name="trade">The commands of the market.</param>
    public ExchangeCommand(MarketTradeService trade)
    {
        ArgumentNullException.ThrowIfNull(trade);

        _trade = trade;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.GiveResourceTypeId).NotNull().GreaterThan(0);
        rules.RuleFor(request => request.Amount).NotNull().GreaterThan(0);
    }

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ExchangeDto exchange = await _trade
            .ExchangeAsync(context.PlayerId, request.GiveResourceTypeId!.Value, request.Amount!.Value, cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(exchange, MarketJson.Options);
    }

    /// <summary>
    /// The request: the currency the player gives and how much of it.
    /// </summary>
    /// <param name="GiveResourceTypeId">The currency the player gives.</param>
    /// <param name="Amount">How much of it.</param>
    public sealed record Request(
        [property: JsonPropertyName("give_resource_type_id")] int? GiveResourceTypeId,
        [property: JsonPropertyName("amount")] long? Amount);
}
