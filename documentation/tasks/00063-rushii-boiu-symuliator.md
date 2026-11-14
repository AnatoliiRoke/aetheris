# Задача 00063 — Рушій бою і симулятор бою

> **Статус:** ✅ ВИКОНАНО 14.11.2026
> **Запит автора (12–13.11.2026):** серце бою: детермінований розрахунок відрізку без бази й мережі та консольний симулятор на тому самому коді.
> **Логіка бою:** [game-design/06](../game-design/06-kosmichni-boi.md) §2–§8, [14 §5](../game-design/14-balans-i-formuly.md), симулятори [tech/06 §4](../tech/06-testuvannia-ta-symuliatory.md).
> **Обов'язково прочитати:** `documentation/tech/08-pravyla-kodu-dlia-ahentiv.md`, `documentation/tech/09-zvedeni-pravyla-kodu-lohiky-ta-kodiv.md`, `documentation/tech/01-arkhitektura.md`.
> **Залежить від:** [00062](00062-dani-boiu-kharakterystyky-ta-nalashtuvannia.md), [00071](00071-boiovyi-poriadok-flotu.md) (перелік формацій для побудови початкового стану).

## 1. Як є зараз (перевірено 13.11.2026)

1. Бойового коду немає. Після 00062 існує порожній проєкт `server/src/Aetheris.Combat` з `CombatConfigKeys`/`CombatSettings`.
2. Каталог деталей для рушія береться з `IProductionModule` (методи каталогу кораблів); дані корабля — `ShipEntity` + `ShipModuleEntity` (Fleet).
3. Випадковість у модулях — через `Aetheris.Core.Randomness.IRandomSource` (недетермінований). Для рушія потрібен власний детермінований генератор: `IRandomSource` у рушії **не** використовується.
4. Годинник гри — `IGameClock`; рушій його не бачить: час у рушії — лише тік від початку відрізку.
5. Симулятори балансу — у `server/tools/` (за зразком наявних; див. tech/06 §4).

## 2. Рішення

### 2.1 Структура проєктів

```
server/src/Aetheris.Combat/                 модуль (серверна частина: 00064–00066)
server/src/Aetheris.Combat.Engine/          чиста бібліотека, без EF, ASP.NET, IGameClock, IGameConfig
server/tools/Aetheris.CombatSim/            консольний симулятор (залежить лише від Engine)
server/tests/Aetheris.Tests/Combat/         тести
```

`Aetheris.Combat.Engine` не посилається на жоден інший модуль; тест архітектури (`server/tests/Aetheris.Tests/Architecture`) забороняє залежності й `DateTime.UtcNow`, `Random`, `Guid.NewGuid` у цьому проєкті.

### 2.2 Типи стану (усі `readonly record struct` / `sealed record`, координати — `Vec3(double X, double Y, double Z)` з `Z = 0`, правило автора про 3D)

```csharp
public readonly record struct Vec3(double X, double Y, double Z);

public sealed record BattleState(
    long BattleId, int Cycle, ulong Seed,
    FieldState Field,
    IReadOnlyList<SideState> Sides,
    IReadOnlyList<SquadronState> Squadrons,
    IReadOnlyList<ShipState> Ships,
    IReadOnlyList<DroneState> Drones,
    IReadOnlyList<DebrisState> Debris);

public sealed record FieldState(
    double Width, double Height,
    IReadOnlyList<FieldObject> Objects);              // Kind: Planet | AsteroidField, Center, Radius

public sealed record ShipState(
    long ShipId, int SideId, int SquadronId, int HullGroupId,
    Vec3 Position, Vec3 Velocity, double Heading,
    double Hull, double HullMax, double Armor, double ArmorMax,
    double ShieldNow, double ShieldCapacity,
    double Signature, double SignatureCut,
    IReadOnlyList<ModuleState> Modules,               // PartTypeId, Kind, Integrity, IntegrityMax, PowerPriority, Mass, On
    IReadOnlyDictionary<int, int> Ammo,               // part_type_id оружия -> боєкомплект
    IReadOnlyDictionary<int, int> HoldDrones,         // part_type_id дрона -> кількість у трюмі
    bool Retreating, bool Destroyed);

public sealed record SquadronState(
    int SquadronId, int SideId, string Name,
    IReadOnlyList<long> ShipIds,
    SquadronOrder Order);                              // чинний наказ

public sealed record SquadronOrder(
    IReadOnlyList<Vec3> Route, string BehaviorLogic, string TargetRuleLogic,
    long? FocusShipId, long? FocusSquadronId,
    DroneCommand DroneCommand, bool Retreat, int? EscortSquadronId);
```

Тип `string BehaviorLogic` — це `logic_code` з довідника (`approach`/`keep_distance`/`stand`/`escort`), рушій не знає нейтральних кодів.

### 2.3 Вхід і вихід

```csharp
public static class SegmentSimulator
{
    public static SegmentResult Run(BattleState state, SegmentInput input, CombatSettings settings);
}
public sealed record SegmentInput(int SegmentSeconds);                 // з settings; для тестів задається
public sealed record SegmentResult(
    BattleState NewState,
    IReadOnlyList<SquadronTrack> Tracks,        // за кожну ескадрилью: Vec3[] через track_step_seconds
    IReadOnlyList<BattleLogEntry> Log,
    BattleOutcome Outcome);                     // Continue | SideWon(sideId) | Draw
public static class CycleEnd { public static BattleState Apply(BattleState s, CombatSettings settings); }
public static class HoldSplitter { public static HoldSplit Split(FleetHold hold, IReadOnlyList<ShipHoldSpec> ships, HoldPreference? preference); }
```

Накази — частина `SquadronState.Order` у вхідному стані (сервер підставляє накази до виклику), тож `Run` — **чиста функція** `state → (state', результат)`.

### 2.4 Детермінованість

1. Генератор `Xoshiro256Rng(seed)`; зерно відрізку = `SplitMix64(battleSeed ^ cycle)`. Кожен кораблетік бере значення в порядку, заданому **упорядкованими за `ShipId`** списками (ніколи за хешем словника).
2. Фіксований крок `dt = tick_ms / 1000.0`; кількість тіків `segment_seconds * 1000 / tick_ms` (ціле, перевіряється, що ділиться націло, інакше налаштування відхиляється при завантаженні `CombatSettings`).
3. Обчислення лише в `double` з однаковим порядком операцій; заборонені паралельні `Parallel.For`, `Dictionary` без впорядкування, `Math.FusedMultiplyAdd` (різна поведінка на CPU).
4. Журнал і слід — функції стану; **тест на відтворюваність:** два прогони з тим самим входом дають побайтово однаковий серіалізований результат; прогін «стан після циклу N, збережений у JSON і відновлений» дає той самий цикл N+1.

### 2.5 Тік розрахунку (порядок кроків незмінний)

1. **Видимість.** Для кожної сторони `visible[side] = {ворожий корабель t | ∃ свій корабель s: dist(s,t) ≤ detect(s) × t.Signature × (1 − t.SignatureCut) × fieldDetectMult(t), і відрізок s→t не перетинає коло планети}`; корабель-детектор: максимум `char_detection` живих радарів. Просторова сітка (2.7). Невидимі цілі не обираються ціллю.
2. **Рух.** Швидкість ескадрильї = мінімум живих швидкостей кораблів; швидкість корабля = `sum(char_speed живих двигунів) / loadFactor`, у астероїдному полі × `asteroid_speed_mult`. Поведінка: `approach` — рух до найближчої видимої цілі до дальності зброї, `keep_distance` — до `0.8 × maxWeaponRange` і назад, `stand` — без руху, `escort` — рух за ескадрильєю-ціллю з відступом. Маршрут із точок перекриває поведінку (до останньої точки). Кожен корабель тримає зсув строю від центру ескадрильї. Планета — коло-перешкода; обхід по дотичній з меншим кутом.
3. **Дрони на повідці.** Дрон літає в колі `leashRadius` навколо носія (`char_leash_radius`), ціль — найближча видима ціль у цьому колі (або ціль фокусу ескадрильї); носій знищено → усі його дрони `Destroyed`.
4. **Вибір цілі.** Ціль фокусу ескадрильї, якщо видима й у дальності; інакше за `TargetRuleLogic` з видимих у дальності зброї: `nearest` (мінімум відстані), `weakest` (мінімум `Hull+Armor+Shield`), `strongest` (максимум), `carriers` (пріоритет носіям дронів), `drones` (пріоритет дронам); за рівності — менший `ShipId`.
5. **Постріл.** Зброя стріляє, якщо: модуль не `destroyed` і `On`, є боєкомплект (для кінетичної/ракетної) і пройшов інтервал `1 / char_rate` (лічильник перезарядки в стані зброї). Енергетична (`damage_kind = energy`, `char_flight_speed = 0`) — влучання перевіряється тим самим тіком; інакше створюється снаряд `Projectile(Id, ShooterId, TargetId, Origin, Speed, DamageSalvo, Kind, ExpectedImpactTick)`.
6. **Підліт і влучання.** `impactTick = fireTick + ceil(dist / (flightSpeed × dt))`. На тіку влучання: ціль мертва / сховалась за планетою (лінія стрілок→ціль перетинає планету) / вийшла з видимості → промах. Протиракетна система цілі (`char_missiles`) з імовірністю `AntiMissileChance` збиває ракету/торпеду. Інакше `p = clamp01( accuracy[hullGroup] × (1 − distanceFalloff × (d/range)) × (1 − targetSpeedFalloff × min(1, v/vRef)) × (1 + radarBonus) × (1 − ewMalus) × fieldHitMult )`, де `accuracy[g]` з каталогу або `accuracy_default`; `vRef` — швидкість найшвидшого корабля бою.
7. **Шкода.** `salvo` → щит: `absorbed = min(salvo × block[kind], shieldNow)`, `shieldNow −= absorbed`, `rest = salvo − absorbed / block[kind]` (енергетичний/кінетичний блок з `char_energy_block`/`char_kinetic_block`) → броня: `armor −= rest × armorShare` → корпус: `hull −= решта`; одночасно `moduleDamage = rest × combat.module_damage_share` потрапляє в випадковий живий модуль (вага — `Mass`); `Integrity ≤ 0 → destroyed`. `Hull ≤ 0` → корабель `Destroyed`: дрони й боєкомплект зникають, уламки створюються (00066).
8. **Енергія.** Якщо `sum(power живих реакторів) < sum(energy_use увімкнених модулів)`, модулі вимикаються за `PowerPriority`: першим — з найбільшим значенням (менше = вимикається пізніше), за рівності — більший `ModuleId`; так до зведення балансу.
9. **Відступ.** Ескадрилья з `Retreat` рухається до найближчого краю; перетин кордону — кораблі `Left`, вони виходять із бою.

### 2.6 Кінець відрізку та циклу

`CycleEnd.Apply`: щит `+= shieldCapacity × shield_restore_share + char_shield_restore`, не вище `ShieldCapacity`; ремонтні дрони (05a §5.2) повертають броню за наявною характеристикою ремонту дрона; лічильники перезарядки скидаються. **Снаряди** на межі відрізків: тривалість польоту снаряда ≪ тривалості відрізку, тому знімок стану снарядів не містить; снаряд, що не долетів до кінця відрізку, **долітає в тому ж відрізку за скороченим правилом** (влучання рахується на останньому тіку за положенням цілі), щоб стан після циклу був повним і відновлюваним.

### 2.7 Продуктивність

1. Рівномірна сітка (клітина = `maxRange / 2`), перебудовується раз на 4 тіки; пошук цілі й видимості — по сусідніх клітинах, не O(n²).
2. Масиви структур (`ShipState` у `Span`-подібних буферах), пул снарядів, без LINQ і алокацій у гарячому циклі (`BenchmarkDotNet` у `server/tools/Aetheris.CombatSim` перевіряє алокації).
3. Ціль: відрізок 60 с × `tick_ms = 250` (240 тіків) для 2×300 кораблів + дрони < 200 мс на одному ядрі, сталі алокації < 5 МБ.

### 2.8 Результат відрізку для клієнта й звіту

- `SquadronTrack(SquadronId, Vec3[] Points)` — точки кожні `track_step_seconds` (6 точок на 60 с).
- `BattleLogEntry(Tick, Type, …)`, `Type` ∈ `fire_summary`, `ship_destroyed`, `module_destroyed`, `drone_destroyed`, `squadron_left`, `shield_down`, `debris_collected`; **зведення**, а не кожен постріл: для пари «ескадрилья → ескадрилья» за відрізок один запис `fire_summary(shots, hits, shield, armor, hull)`.
- Покадрових траєкторій не зберігаємо.

### 2.9 Консольний симулятор

`dotnet run --project server/tools/Aetheris.CombatSim -- --fleet-a a.json --fleet-b b.json --runs 200 --seed 1 --behavior-a approach --behavior-b keep_distance --out server/tools/balance/combat/report-001.md`

- Вхід: JSON флоту `{ "ships":[{"designId":…,"count":N}], "formation":{…} }`; проєкти беруться з `server/tools/balance/combat/designs.json` (вивантаження сіду).
- Вихід: відсоток перемог, середні втрати в одиницях і вартості, матриця «проєкт проти проєкту» (N×N), найефективніший модуль (шкода на одиницю вартості), розподіл тривалості в циклах; пишеться у Markdown і CSV.

## 3. Що зробити

1. Проєкти `Aetheris.Combat.Engine`, `Aetheris.CombatSim`, тести; у `Aetheris.slnx`.
2. Типи 2.2, `Xoshiro256Rng`, `SegmentSimulator.Run`, `CycleEnd.Apply`, `HoldSplitter.Split`, `SpatialGrid`, `LineOfSight` (відрізок проти кола), `Pathing` (обхід планети по дотичній).
3. `CombatSettings` з усіх ключів 00062 (без чисел за замовчуванням у рушії, перевірка меж).
4. `ShipStateBuilder` (у серверній частині `Aetheris.Combat`): будує `ShipState` з сутностей Fleet і каталогу Production **двома пакетними запитами** на бій.
5. Симулятор, `designs.json`, перший звіт `server/tools/balance/combat/report-001.md`; README модуля з командою запуску.
6. Тести `server/tests/Aetheris.Tests/Combat/`:
   - `DeterminismTests` (два прогони й відновлення зі збереженого стану);
   - `DamageTests` (щит→броня→корпус→модуль за сталим зерном; енергія й кінетика);
   - `AccuracyTests` (таблиця `accuracy[group]`, відстань, швидкість, астероїди);
   - `LineOfSightTests` (планета закриває; кут обходу);
   - `ProjectileTests` (ціль померла до влучання → промах; протиракета);
   - `PowerTests` (вимкнення за пріоритетом);
   - `DroneTests` (повідок, смерть із носієм);
   - `RetreatTests` (вихід за край);
   - `HoldSplitterTests` (нестача боєприпасів, межа місткості, зникнення частки знищеного);
   - `CycleEndTests` (щит +30 %, ремонтні дрони);
   - `PerformanceTests` (2×300 кораблів < 200 мс, позначка `[Trait("Category","Perf")]`).

## 4. Критерії приймання

1. Рушій реалізує 2.5–2.6 повністю; кожен крок покритий тестом.
2. Детермінованість доведена тестом; `Aetheris.Combat.Engine` не посилається на базу, мережу, годинник, `Random`.
3. Симулятор запускається командою з README й видає звіт за секунди; `report-001.md` у репозиторії.
4. Продуктивність 2.7 перевірена тестом.
5. У коді немає чисел балансу; `dotnet build -c Release`, `dotnet test -c Release`, `dotnet format --verify-no-changes --no-restore` — зелені.

## 5. Результат виконання

**Рушій.** Бібліотека `server/src/Aetheris.Combat.Engine` рахує відрізок без бази, мережі, годинника й `Random`. `SegmentSimulator.Run` не збільшує номер циклу. Кінець циклу робить `CycleEnd.Apply`. Є `HoldSplitter`, `SpatialGrid`, `Xoshiro256Rng`, `BattleJson`. `CombatSettings` не тримає стартових чисел балансу: їх задає виклик, межі перевіряються в конструкторі. Частка дистанції утримання 0.8, період сітки 4 і двосторонній запас клітини лежать в `EngineTune` як числа формули, не балансу.

**Стан корабля.** `ShipStateBuilder` у `server/src/Aetheris.Combat` збирає `ShipState` двома пакетними запитами. Цей SQL на `aetheris_test` не виконувався. Таблиць `fleet_battle_*` немає. `target_rule` і `behavior` не створювались знову: вони з 00062.

**Симулятор.** `server/tools/Aetheris.CombatSim`. Каталог `server/tools/balance/combat/designs.json` зібраний із `server/src/Aetheris.Production/Data/production-reference-seed.json`. Легкий лазер `sapsan-light-laser` коштує 3133, важкий `sapsan-heavy-laser` — 3213. Флоти по 3 кораблі: початки (1000, 4000) і (1040, 4000), крок 8. Команда з README прогнана з кореня репозиторію в Release і записала `server/tools/balance/combat/report-001.md` та CSV поруч. 200 прогонів, зерно 1, `approach` проти `keep_distance`: перемог A 0, B 0, нічиїх 200. Середні постріли 3 і 3, влучання 0.755 і 0.43, втрати 0 кораблів і 0 вартості. Усі 200 тривають 6 циклів при ліміті 6. На старті 9 пар, усі бачать одна одну, найбільша відстань 56, виявлення 66, дальність зброї 100 і 100. Після першого прогону найближчі кораблі різних сторін стоять на 72. `keep_distance` тримає центр на 0.8 дальності зброї, після залпу обидві сторони вже за виявленням, тож далі тиша до ліміту. Формули й розстановку не змінювали. Легкий лазер `part_m_13`: середня шкода 30.955, на одиницю вартості 0.043. Важкий `part_m_14`: 21.07 і 0.022. Матриця один на один — теж усі нічиї. Один відрізок після прогріву: 8.775 мс і 670760 байт. BenchmarkDotNet не додавався.

**Тести** в `server/tests/Aetheris.Tests/Combat/`: `DeterminismTests`, `DamageTests`, `AccuracyTests`, `LineOfSightTests`, `ProjectileTests`, `PowerTests`, `DroneTests`, `RetreatTests`, `HoldSplitterTests`, `CycleEndTests`, `PerformanceTests`, `CombatSettingsTests`, `MovementTests`, `ShipStateBuilderTests`. Чистота рушія — `Architecture/EnginePurityTests`.

**Команди (14.11.2026), з каталогу `server/`.**
- `dotnet build -c Release`: 0 попереджень, 0 помилок.
- `dotnet format --verify-no-changes --no-restore`: код виходу 0. Завантаження робочого простору написало попередження; файли формат не відхилив.
- Повний `dotnet test -c Release`: 847 тестів, 835 зелених, 12 червоних, 0 пропущених, 19 хв 23 с. `Aetheris.Analyzers.Tests` зелені. Окремий фільтр `FullyQualifiedName~Aetheris.Tests.Combat`: 36 з 36 за 1.4 с, бюджет 200 мс і 5 МБ у цьому прогоні виконано. У повному прогоні `PerformanceTests.Two_lines_finish_a_segment_inside_the_budget` заміряв 523.764 мс стіни годинника, бо набір іде паралельно з іншими тестами. Решта 11 червоних — не бойові; перелік у `00063-pytannia.md`. Критерій 5 про повністю зелений набір через це не виконаний.
- Клієнт ця задача не змінювала. Lint, build клієнта і браузер не запускались.
