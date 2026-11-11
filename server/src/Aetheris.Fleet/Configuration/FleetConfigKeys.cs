using Aetheris.Core.Configuration;

namespace Aetheris.Fleet.Configuration;

/// <summary>
/// The configuration keys of the fleet module with their starting values (task 00038).
/// </summary>
/// <remarks>
/// The hulls, their slots and the modules live in the tables of the production; these are the
/// limits around them. The host adds these definitions to its seed, so the admin panel checks
/// the bounds like for every other parameter.
/// </remarks>
public static class FleetConfigKeys
{
    /// <summary>
    /// The least set of ammunition the hold of a hull must take (task 00038, section 3.2).
    /// </summary>
    /// <remarks>
    /// The number comes from the modelling of the ships (task 00041); until the modelling gives
    /// it, this value is the starting one, so a change of the number is a change of the data
    /// and not of the code.
    /// </remarks>
    public const string MinimumAmmunition = "fleet.hold.minimum_ammunition";

    /// <summary>
    /// The largest number of the characters of the name of a project.
    /// </summary>
    public const string DesignNameMaxLength = "fleet.design.name_max_length";

    /// <summary>The places of a shipyard queue when the building has no effect yet (task 00039).</summary>
    public const string QueueBaseSlots = "fleet.shipyard.queue_base_slots";

    /// <summary>The most places a shipyard queue may have (task 00039).</summary>
    public const string QueueMaxSlots = "fleet.shipyard.queue_max_slots";

    /// <summary>The assembly time of the smallest project, in game seconds. The guide is ten minutes.</summary>
    public const string AssemblyMinSeconds = "fleet.shipyard.assembly_min_seconds";

    /// <summary>The assembly time of the largest project, in game seconds. The guide is six hours.</summary>
    public const string AssemblyMaxSeconds = "fleet.shipyard.assembly_max_seconds";

    /// <summary>The size, in slots, that gets the shortest assembly. A smaller project stays at the shortest time.</summary>
    public const string SizeMin = "fleet.shipyard.size_min";

    /// <summary>The size, in slots, that gets the longest assembly. A larger project stays at the longest time.</summary>
    public const string SizeMax = "fleet.shipyard.size_max";

    /// <summary>How many tonnes of mass count as one slot of size.</summary>
    public const string MassPerSlot = "fleet.shipyard.mass_per_slot";

    /// <summary>The credits of the assembly for one slot of the hull.</summary>
    public const string CreditsPerSlot = "fleet.shipyard.credits_per_slot";

    /// <summary>The credits of the assembly for one tonne of the project.</summary>
    public const string CreditsPerTonne = "fleet.shipyard.credits_per_tonne";

    /// <summary>The share of the credits spent on the modules that the assembly takes as credits.</summary>
    public const string CreditShare = "fleet.shipyard.credit_share";

    /// <summary>The share of the resources spent on the modules that the assembly takes as materials.</summary>
    public const string MaterialShare = "fleet.shipyard.material_share";

    /// <summary>
    /// The share of the credit price of the fitted modules that a finished ship takes as credits per hour.
    /// </summary>
    public const string UpkeepShare = "fleet.shipyard.upkeep_share";

    /// <summary>
    /// Kept in the catalog. A dismantling does not read it: the project returns every component
    /// and the crew, and the resources stay.
    /// </summary>
    public const string HullReturnShare = "fleet.shipyard.hull_return_share";

    /// <summary>The largest number of the characters of the name of a fleet (task 00040). The column holds 128.</summary>
    public const string FleetNameMaxLength = "fleet.formation.name_max_length";

    /// <summary>
    /// The most orders a flight plan of a fleet may hold (task 00050).
    /// </summary>
    public const string FlightPlanMaxOrders = "fleet.flight_plan.max_orders";

    /// <summary>
    /// The length of a cycle of extraction, in game seconds (task 00053): the yield of a fleet goes to
    /// its holds once per cycle.
    /// </summary>
    public const string ExtractionCycleSeconds = "fleet.extraction.cycle_seconds";

    /// <summary>
    /// How far a fleet may be from the point of a free asteroid field and still be at the field, in
    /// the units of the map (task 00054): the extraction takes its place from the position of the fleet.
    /// </summary>
    public const string ExtractionPlaceTolerance = "fleet.extraction.place_tolerance";

    /// <summary>
    /// The push of the reference point of the speed, in kilonewtons (task 00053).
    /// </summary>
    public const string SpeedReferencePush = "fleet.speed.reference_push";

    /// <summary>
    /// The mass of the reference point of the speed, in tonnes (task 00053).
    /// </summary>
    public const string SpeedReferenceMass = "fleet.speed.reference_mass";

    /// <summary>
    /// The speed of the reference point, in light-years per hour (task 00053): a ship with the
    /// reference push and the reference mass flies at this speed.
    /// </summary>
    public const string SpeedReferenceSpeed = "fleet.speed.reference_speed";

    /// <summary>
    /// Gets the definitions of the parameters with their starting values and bounds.
    /// </summary>
    public static IReadOnlyList<ConfigParameterDefinition> Definitions { get; } =
    [
        new(MinimumAmmunition, "100", "0", "1000000000", "Найменший боєкомплект, який має вмістити базовий трюм корпусу."),
        new(DesignNameMaxLength, "128", "1", "450", "Найбільша кількість символів у назві проєкту корабля."),
        new(QueueBaseSlots, "2", "1", "100", "Місця черги верфі, коли в будівлі ще немає ефекту місць."),
        new(QueueMaxSlots, "4", "1", "100", "Найбільша кількість місць у черзі однієї верфі."),
        new(AssemblyMinSeconds, "600", "1", "86400", "Найкоротший час складання корабля, у секундах ігрового часу. Орієнтир — десять хвилин."),
        new(AssemblyMaxSeconds, "21600", "1", "86400", "Найдовший час складання корабля, у секундах ігрового часу. Орієнтир — шість годин."),
        new(SizeMin, "8", "0", "1000000", "Розмір проєкту в слотах, якому дістається найкоротший час складання."),
        new(SizeMax, "80", "0", "1000000", "Розмір проєкту в слотах, якому дістається найдовший час складання."),
        new(MassPerSlot, "25", "0.000001", "1000000", "Скільки тонн маси рахуються як один слот розміру при складанні."),
        new(CreditsPerSlot, "20", "0", "1000000000", "Кредити складання за один слот корпусу."),
        new(CreditsPerTonne, "2", "0", "1000000000", "Кредити складання за одну тонну проєкту."),
        new(CreditShare, "0.5", "0", "1", "Частка кредитів, витрачених на модулі проєкту, яку складання бере кредитами."),
        new(MaterialShare, "0.1", "0", "1", "Частка ресурсів, витрачених на модулі проєкту, яку складання бере матеріалами планети."),
        new(UpkeepShare, "0.1", "0", "1", "Частка кредитної ціни модулів проєкту, яку готовий корабель бере кредитами за годину. Корпус не входить."),
        new(FleetNameMaxLength, "64", "1", "128", "Найбільша кількість символів у назві флоту."),
        new(FlightPlanMaxOrders, "32", "1", "256", "Найбільша кількість наказів у політному листі флоту."),
        new(ExtractionCycleSeconds, "900", "1", "86400", "Тривалість одного циклу видобування, у секундах ігрового часу: раз на цикл видобуток потрапляє в трюми флоту. Орієнтир — 15 хвилин."),
        new(ExtractionPlaceTolerance, "1", "0", "1000000", "Допуск збігу координат флоту з точкою вільного астероїдного поля, в одиницях карти: видобування бере місце з позиції флоту, і поле вважається тим, де стоїть флот, коли відстань до його точки не більша за допуск."),
        new(SpeedReferencePush, "100", "0.000001", "1000000000", "Опорна тяга для швидкості перельоту, у кН. Разом з опорною масою й опорною швидкістю задає сталу формули швидкості v = K·√(F/m): корабель із цією тягою й цією масою летить з опорною швидкістю."),
        new(SpeedReferenceMass, "1000", "0.000001", "1000000000", "Опорна маса для швидкості перельоту, у тоннах (корпус разом із вантажем)."),
        new(SpeedReferenceSpeed, "45", "0.000001", "1000000000", "Опорна швидкість для швидкості перельоту, у світлових роках за годину: стільки літає корабель з опорною тягою й опорною масою."),
        new(HullReturnShare, "0.5", "0", "1", "Не застосовується. Розбір повертає всі компоненти проєкту й екіпаж і не повертає ресурси на склад."),
    ];
}
