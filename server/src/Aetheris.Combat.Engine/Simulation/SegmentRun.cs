namespace Aetheris.Combat.Engine;

/// <summary>The mutable buffers of one segment. Records are written once, at the end.</summary>
internal sealed partial class SegmentRun
{
    private const byte BehaviorApproach = 1;
    private const byte BehaviorKeep = 2;
    private const byte BehaviorStand = 3;
    private const byte BehaviorEscort = 4;
    private const byte RuleNearest = 1;
    private const byte RuleWeakest = 2;
    private const byte RuleStrongest = 3;
    private const byte RuleCarriers = 4;
    private const byte RuleDrones = 5;
    private const byte ModuleEngine = 1;
    private const byte ModuleReactor = 2;
    private const byte ModuleWeapon = 3;
    private const byte ModuleShield = 4;
    private const byte ModuleArmor = 5;
    private const byte ModuleRadar = 6;
    private const byte ModuleAntiMissile = 7;
    private const byte ModuleRestorer = 8;
    private const byte ModuleCargo = 9;
    private const byte ModuleOther = 10;
    private const byte HitEnergy = 1;
    private const byte HitKinetic = 2;
    private const byte HitMissile = 3;
    private const byte HitTorpedo = 4;
    private const byte HitExplosive = 5;
    private const int EdgeLeft = 0;
    private const int EdgeBottom = 1;
    private const int EdgeRight = 2;
    private const int EdgeTop = 3;
    private const int OneSideLeft = 1;
    private const int TwoSidesLeft = 2;
    private const int ManySidesLeft = 3;

    private readonly struct Circle
    {
        public Circle(double x, double y, double z, double radius)
        {
            X = x;
            Y = y;
            Z = z;
            Radius = radius;
        }

        public double X { get; }

        public double Y { get; }

        public double Z { get; }

        public double Radius { get; }
    }

    private struct SimModule
    {
        public long ModuleId;
        public int PartTypeId;
        public byte Kind;
        public double Integrity;
        public double IntegrityMax;
        public int PowerPriority;
        public double Mass;
        public bool On;
        public double Cooldown;
        public double EnergyUse;
        public double Power;
        public double Speed;
        public double Detection;
        public double Damage;
        public double Rate;
        public double Range;
        public double FlightSpeed;
        public byte DamageKind;
        public int AmmoPartTypeId;
        public double EnergyBlock;
        public double KineticBlock;
        public double ShieldCapacity;
        public double ShieldRestore;
        public double AntiMissileChance;
        public double LauncherSize;
        public double Repair;
        public double Missiles;
        public double Magazine;
        public string KindName;
        public string DamageKindName;
        public int AccuracyStart;
        public int AccuracyCount;
        public bool DestructionLogged;
    }

    private struct SimShip
    {
        public long ShipId;
        public int SideId;
        public int SideIndex;
        public int SquadronId;
        public int SquadronIndex;
        public int HullGroupId;
        public double X;
        public double Y;
        public double Z;
        public double Vx;
        public double Vy;
        public double Vz;
        public double Heading;
        public double Hull;
        public double HullMax;
        public double Armor;
        public double ArmorMax;
        public double ShieldNow;
        public double ShieldCapacity;
        public double Signature;
        public double SignatureCut;
        public double EnergyBlock;
        public double KineticBlock;
        public double ArmorShare;
        public double LoadFactor;
        public double RadarBonus;
        public double Cost;
        public bool Retreating;
        public bool Destroyed;
        public bool Left;
        public bool IsCarrier;
        public double OffsetX;
        public double OffsetY;
        public double OffsetZ;
        public int ModuleStart;
        public int ModuleCount;
        public int AmmoStart;
        public int AmmoCount;
        public int HoldStart;
        public int HoldCount;
        public double NominalSpeed;
        public double ActualSpeed;
        public bool ShieldLogged;
        public bool DestructionLogged;
    }

    private struct SimStack
    {
        public int PartTypeId;
        public int Count;
    }

    private struct SimDrone
    {
        public long DroneId;
        public long CarrierShipId;
        public int CarrierIndex;
        public int SideId;
        public int SideIndex;
        public int SquadronIndex;
        public int PartTypeId;
        public int HullGroupId;
        public double X;
        public double Y;
        public double Z;
        public double Vx;
        public double Vy;
        public double Vz;
        public double Hull;
        public double HullMax;
        public double Speed;
        public double Signature;
        public double EwMalus;
        public double EwRadius;
        public double Repair;
        public double Leash;
        public bool RepairDrone;
        public bool OnArmor;
        public bool Destroyed;
        public bool DestructionLogged;
        public double Cooldown;
        public double Damage;
        public double Rate;
        public double Range;
        public double FlightSpeed;
        public byte DamageKind;
        public double LauncherSize;
        public int AccuracyStart;
        public int AccuracyCount;
        public bool HasWeapon;
        public ModuleFacts WeaponFacts;
    }

    private struct SimShot
    {
        public long Id;
        public bool Instant;
        public bool Resolved;
        public bool FromDrone;
        public int ShooterShip;
        public int ShooterDrone;
        public int TargetShip;
        public int TargetDrone;
        public double OriginX;
        public double OriginY;
        public double OriginZ;
        public double Speed;
        public double Damage;
        public byte Kind;
        public int ImpactTick;
        public double Accuracy;
        public double Range;
        public bool Interceptable;
        public int FromSquadron;
        public int ToSquadron;
        public double RadarBonus;
    }

    private struct SimSquadron
    {
        public int SquadronId;
        public int SideId;
        public int SideIndex;
        public byte Behavior;
        public byte Rule;
        public long FocusShipId;
        public bool HasFocusShip;
        public long FocusSquadronId;
        public bool HasFocusSquadron;
        public bool Retreat;
        public int EscortSquadronId;
        public bool HasEscort;
        public int RouteStart;
        public int RouteCount;
        public int MemberStart;
        public int MemberCount;
        public double CenterX;
        public double CenterY;
        public bool HasCenter;
        public bool LeftLogged;
    }

    private readonly CombatSettings _settings;
    private readonly BattleState _source;
    private readonly double _dt;
    private readonly int _tickCount;
    private readonly int _tickMilliseconds;
    private readonly double _width;
    private readonly double _height;
    private readonly Xoshiro256Rng _rng;
    private readonly SpatialGrid _grid = new();
    private readonly List<BattleLogEntry> _log = [];
    private SimShip[] _ships = [];
    private SimModule[] _modules = [];
    private SimStack[] _ammo = [];
    private SimStack[] _hold = [];
    private SimDrone[] _drones = [];
    private SimShot[] _shots = [];
    private SimSquadron[] _squadrons = [];
    private AccuracyEntry[] _accuracy = [];
    private Vec3[] _route = [];
    private int[] _members = [];
    private Circle[] _planets = [];
    private Circle[] _asteroids = [];
    private int[] _sideIds = [];
    private IReadOnlyList<DronePattern>[] _patterns = [];
    private double[] _xs = [];
    private double[] _ys = [];
    private int[] _query = [];
    private byte[] _visible = [];
    private byte[] _visibleNow = [];
    private byte[] _droneVisible = [];
    private byte[] _droneVisibleNow = [];
    private bool[] _carrier = [];
    private int[] _shipInstants = [];
    private int[] _droneInstants = [];
    private int[] _fired = [];
    private int[] _hits = [];
    private double[] _shieldDealt = [];
    private double[] _armorDealt = [];
    private double[] _hullDealt = [];
    private Vec3[] _track = [];
    private int[] _trackCount = [];
    private int _shipCount;
    private int _moduleCount;
    private int _droneCount;
    private int _droneRoom;
    private int _shotCount;
    private int _squadronCount;
    private int _sideCount;
    private int _planetCount;
    private int _asteroidCount;
    private int _shipInstantCount;
    private int _droneInstantCount;
    private long _nextShotId = 1;
    private long _nextDroneId = 1;
    private long _nextDebrisId = 1;
    private double _referenceSpeed;
    private double _pad;
    private double _cellSize = 1;
    private double _signatureSpan = 1;
    private int _maxTrack;
    private int _currentTick;
    private int _accuracyCount;
    private int _pendingFromSquadron;
    private readonly List<DebrisState> _debris = [];

    private SegmentRun(BattleState state, SegmentInput input, CombatSettings settings)
    {
        _source = state;
        _settings = settings;
        _dt = settings.TickSeconds;
        _tickCount = settings.CountTicks(input.SegmentSeconds);
        _tickMilliseconds = settings.TickMilliseconds;
        _width = state.Field.Width;
        _height = state.Field.Height;
        _rng = new Xoshiro256Rng(Xoshiro256Rng.SplitMix64(state.Seed ^ (ulong)state.Cycle));
    }

    public static SegmentResult Execute(BattleState state, SegmentInput input, CombatSettings settings)
    {
        SegmentRun run = new(state, input, settings);
        run.Load();
        run.LaunchOrRecall();

        for (int tick = 0; tick < run._tickCount; tick++)
        {
            run.Tick(tick);
        }

        return run.Finish();
    }

    private void Tick(int tick)
    {
        _currentTick = tick;
        _pendingFromSquadron = 0;
        RefreshSpeeds();

        if ((tick % EngineTune.GridRebuildPeriod) == 0)
        {
            RebuildGrid();
        }

        MarkVisible(_visible, _droneVisible);
        MoveSquadrons();
        MoveDrones();
        _shipInstantCount = 0;
        _droneInstantCount = 0;
        FireShips(tick);
        FireDrones(tick);

        if (tick + 1 == _tickCount)
        {
            ForceShots(tick);
        }

        MarkVisible(_visibleNow, _droneVisibleNow);
        ResolveInstants(_shipInstants, _shipInstantCount);
        ResolveProjectiles(tick);
        ResolveInstants(_droneInstants, _droneInstantCount);
        BalancePower();
        Retreat();
        WriteTrack(tick);
        CompactShots();
    }
}
