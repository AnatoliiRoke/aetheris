using Aetheris.Combat.Engine;
using Aetheris.Combat.Services;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Production.Api;

namespace Aetheris.Tests.Combat;

/// <summary>The mapper turns catalog rows into a state without the database (task 00063).</summary>
public sealed class ShipStateBuilderTests
{
    private const long CopyStride = 1_000_000_000_000L;

    [Fact]
    public void A_plate_fills_the_armor_and_a_shield_stores_a_percent_as_a_share()
    {
        ShipState ship = Only(Stack(2, false, priority: 10), Plate(2, integrity: 75, weight: 12));
        ShipState shielded = Map(
            new FittedShip(1, 1, 240, [HullStack(), Stack(3, false, priority: 10)]),
            Catalog(hull: Hull(), modules: [Shield(3)]));

        Assert.Equal(ModuleKinds.Armor, ship.Modules[0].Kind);
        Assert.Equal(75, ship.Armor);
        Assert.Equal(1, ship.ArmorShare);
        Assert.Equal(0.6, shielded.EnergyBlock);
        Assert.Equal(0.6, shielded.KineticBlock);
        Assert.Equal(125, shielded.ShieldNow);
        Assert.False(shielded.IsCarrier);
    }

    [Fact]
    public void Quantity_expands_copies_and_a_destroyed_stack_stays_off()
    {
        ShipState copies = Map(
            new FittedShip(1, 1, 0, [HullStack(), Stack(5, false, quantity: 3, priority: 10)]),
            Catalog(hull: Hull(), modules: [Plate(5, integrity: 10)]));
        ShipState destroyed = Map(
            new FittedShip(1, 1, 0, [HullStack(), Stack(5, false, conditionCode: ShipModuleConditions.Destroyed, priority: 10)]),
            Catalog(hull: Hull(), modules: [Plate(5, integrity: 10)]));

        Assert.Equal(3, copies.Modules.Count);
        Assert.Equal(5, copies.Modules[0].ModuleId);
        Assert.Equal(5 + CopyStride, copies.Modules[1].ModuleId);
        Assert.Equal(5 + (CopyStride * 2), copies.Modules[2].ModuleId);
        Assert.Equal(30, copies.Armor);
        Assert.Equal(0, destroyed.Modules[0].Integrity);
        Assert.False(destroyed.Modules[0].On);
        Assert.Equal(0, destroyed.Armor);
    }

    [Fact]
    public void A_weapon_copies_the_lowest_round_and_a_launcher_becomes_a_missile()
    {
        ShipModuleDto weapon = Module(
            8,
            damage: 0,
            launcherSize: 2,
            magazine: 4,
            priority: 20,
            ammunitionIds: [9, 4]);
        ShipState ship = Map(
            new FittedShip(1, 1, 0, [HullStack(), Stack(8, false, priority: 20)]),
            Catalog(
                hull: Hull(),
                modules: [weapon],
                ammunition:
                [
                    new ShipAmmunitionDto(9, 5, PartDamageKinds.Kinetic, 2, 10),
                    new ShipAmmunitionDto(4, 41, PartDamageKinds.Kinetic, 2, 80),
                ]));

        ModuleFacts facts = ship.Modules[0].Facts;

        Assert.Equal(ModuleKinds.Weapon, ship.Modules[0].Kind);
        Assert.Equal(41, facts.Damage);
        Assert.Equal(80, facts.FlightSpeed);
        Assert.Equal(DamageKinds.Missile, facts.DamageKind);
        Assert.Equal(4, facts.AmmoPartTypeId);
    }

    [Fact]
    public void A_missile_count_is_not_a_chance_and_speed_stays_on_the_facts()
    {
        ShipModuleDto stopper = Module(
            6,
            priority: 50,
            facts: [new ShipModuleFactDto(CharacteristicKinds.Missiles, "n", null, 161, 1)]);
        ShipModuleDto computer = Module(
            7,
            priority: 50,
            facts: [new ShipModuleFactDto(CharacteristicKinds.Speed, "n", null, 110, 1)]);
        ShipState ship = Map(
            new FittedShip(1, 1, 0, [HullStack(), Stack(6, false), Stack(7, false)]),
            Catalog(hull: Hull(signature: 0.6m, leash: 800), modules: [stopper, computer]));

        Assert.Equal(ModuleKinds.AntiMissile, ship.Modules[0].Kind);
        Assert.Equal(0, ship.Modules[0].Facts.AntiMissileChance);
        Assert.Equal(161, ship.Modules[0].Facts.Missiles);
        Assert.Equal(ModuleKinds.Other, ship.Modules[1].Kind);
        Assert.Equal(110, ship.Modules[1].Facts.Speed);
        Assert.Equal(0.6, ship.Signature);
        Assert.False(ship.IsCarrier);
    }

    [Fact]
    public void Condition_scales_integrity_and_the_hold_maps_are_not_shared()
    {
        ShipState half = Map(
            new FittedShip(1, 1, 10, [HullStack(conditionPercent: 50), Stack(2, false, conditionPercent: 50, priority: 10)]),
            Catalog(hull: Hull(integrity: 1000, weight: 10), modules: [Plate(2, integrity: 100, weight: 30, signatureCut: 60)]));
        ShipState other = Map(
            new FittedShip(2, 1, 0, [HullStack()]),
            Catalog(hull: Hull()));

        Assert.Equal(500, half.Hull);
        Assert.Equal(1000, half.HullMax);
        Assert.Equal(50, half.Armor);
        Assert.Equal(100, half.ArmorMax);
        Assert.Equal(4, half.LoadFactor);
        Assert.Equal(0.6, half.SignatureCut);
        Assert.False(ReferenceEquals(half.Ammo, other.Ammo));
        Assert.False(ReferenceEquals(half.HoldDrones, other.HoldDrones));
    }

    [Fact]
    public void A_missing_part_or_a_missing_shutdown_order_is_refused()
    {
        Assert.Throws<InvalidOperationException>(() => Map(
            new FittedShip(1, 1, 0, [HullStack(), Stack(99, false, priority: 10)]),
            Catalog(hull: Hull())));
        Assert.Throws<InvalidOperationException>(() => Map(
            new FittedShip(1, 1, 0, [HullStack(), Stack(2, false)]),
            Catalog(hull: Hull(), modules: [Plate(2, priority: null)])));
        Assert.Throws<InvalidOperationException>(() => Map(
            new FittedShip(1, 1, 0, [Stack(2, false, priority: 10)]),
            Catalog(hull: Hull(), modules: [Plate(2)])));
    }

    private static ShipState Only(FittedModule stack, ShipModuleDto part) =>
        Map(new FittedShip(1, 1, 0, [HullStack(), stack]), Catalog(hull: Hull(), modules: [part]));

    private static ShipState Map(FittedShip fitted, ShipCatalogDto catalog) =>
        ShipStateBuilder.Map([fitted], catalog)[0];

    private static ShipCatalogDto Catalog(
        ShipHullDto hull,
        IReadOnlyList<ShipModuleDto>? modules = null,
        IReadOnlyList<ShipAmmunitionDto>? ammunition = null) =>
        new([hull], modules ?? [], [], ammunition ?? []);

    private static ShipHullDto Hull(decimal integrity = 1000, decimal weight = 0, decimal signature = 1, decimal leash = 0) =>
        new(1, "h", "n", "d", null, 12, "g", "gn", "combat", 1, 0, 0, integrity, weight, false, Signature: signature, LeashRadius: leash);

    private static FittedModule HullStack(int conditionPercent = 100) =>
        new(1, 1, 1, conditionPercent, ShipModuleConditions.Intact, 1, true, null);

    private static FittedModule Stack(
        long moduleId,
        bool isHull,
        int conditionPercent = 100,
        string conditionCode = ShipModuleConditions.Intact,
        long quantity = 1,
        short? priority = null) =>
        new(moduleId, (int)moduleId, 1, conditionPercent, conditionCode, quantity, isHull, priority);

    private static ShipModuleDto Plate(
        int id,
        decimal integrity = 75,
        decimal weight = 1,
        decimal signatureCut = 0,
        short? priority = 50) =>
        Module(id, integrity: integrity, weight: weight, signatureCut: signatureCut, priority: priority);

    private static ShipModuleDto Shield(int id) =>
        Module(id, shieldCapacity: 125, energyBlock: 60, kineticBlock: 60, integrity: 100, priority: 10);

    private static ShipModuleDto Module(
        int id,
        string? component = null,
        decimal energyUse = 0,
        decimal power = 0,
        decimal damage = 0,
        decimal shieldCapacity = 0,
        decimal weight = 1,
        decimal integrity = 0,
        decimal rate = 0,
        decimal launcherSize = 0,
        decimal signatureCut = 0,
        short? priority = 50,
        decimal magazine = 0,
        decimal energyBlock = 0,
        decimal kineticBlock = 0,
        IReadOnlyList<int>? ammunitionIds = null,
        IReadOnlyList<ShipModuleFactDto>? facts = null) =>
        new(
            id,
            "m",
            "n",
            "d",
            null,
            1,
            "g",
            "gn",
            "role",
            component,
            [],
            [],
            energyUse,
            power,
            0,
            0,
            damage,
            null,
            shieldCapacity,
            weight,
            [1],
            false,
            Integrity: integrity,
            SignatureCut: signatureCut,
            DefaultPowerPriority: priority,
            Rate: rate,
            LauncherSize: launcherSize,
            Magazine: magazine,
            EnergyBlock: energyBlock,
            KineticBlock: kineticBlock,
            AmmunitionIds: ammunitionIds,
            Facts: facts);
}
