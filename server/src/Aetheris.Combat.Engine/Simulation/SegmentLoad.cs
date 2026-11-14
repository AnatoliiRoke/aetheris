namespace Aetheris.Combat.Engine;

internal sealed partial class SegmentRun
{
    private void Load()
    {
        LoadCircles();
        LoadSides();
        LoadSquadrons();
        LoadShips();
        LoadDrones();
        LoadDebris();
        LinkMembers();
        PrepareBuffers();
    }

    private void LoadCircles()
    {
        IReadOnlyList<FieldObject> objects = _source.Field.Objects;
        int planets = 0;
        int asteroids = 0;

        for (int i = 0; i < objects.Count; i++)
        {
            if (string.Equals(objects[i].Kind, FieldObjectKinds.Planet, StringComparison.Ordinal))
            {
                planets++;
            }
            else if (string.Equals(objects[i].Kind, FieldObjectKinds.AsteroidField, StringComparison.Ordinal))
            {
                asteroids++;
            }
        }

        _planets = new Circle[planets];
        _asteroids = new Circle[asteroids];

        for (int i = 0; i < objects.Count; i++)
        {
            FieldObject entry = objects[i];
            Circle circle = new(entry.Center.X, entry.Center.Y, entry.Center.Z, entry.Radius);

            if (string.Equals(entry.Kind, FieldObjectKinds.Planet, StringComparison.Ordinal))
            {
                _planets[_planetCount++] = circle;
            }
            else if (string.Equals(entry.Kind, FieldObjectKinds.AsteroidField, StringComparison.Ordinal))
            {
                _asteroids[_asteroidCount++] = circle;
            }
        }
    }

    private void LoadSides()
    {
        int count = _source.Sides.Count;
        int[] order = new int[count];

        for (int i = 0; i < count; i++)
        {
            order[i] = i;
        }

        Array.Sort(order, (left, right) => _source.Sides[left].SideId.CompareTo(_source.Sides[right].SideId));
        _sideIds = new int[count];
        _sideCount = count;

        for (int i = 0; i < count; i++)
        {
            _sideIds[i] = _source.Sides[order[i]].SideId;
        }
    }

    private void LoadSquadrons()
    {
        int count = _source.Squadrons.Count;
        int[] order = new int[count];

        for (int i = 0; i < count; i++)
        {
            order[i] = i;
        }

        Array.Sort(order, (left, right) => _source.Squadrons[left].SquadronId.CompareTo(_source.Squadrons[right].SquadronId));
        _squadrons = new SimSquadron[count];
        _squadronCount = count;
        int routeCount = 0;

        for (int i = 0; i < count; i++)
        {
            routeCount += _source.Squadrons[order[i]].Order.Route.Count;
        }

        _route = new Vec3[routeCount];
        int routeCursor = 0;

        for (int i = 0; i < count; i++)
        {
            SquadronState squadron = _source.Squadrons[order[i]];
            SquadronOrder command = squadron.Order;
            SimSquadron sim = new()
            {
                SquadronId = squadron.SquadronId,
                SideId = squadron.SideId,
                SideIndex = SideIndex(squadron.SideId),
                Behavior = BehaviorOf(command.BehaviorLogic),
                Rule = RuleOf(command.TargetRuleLogic),
                FocusShipId = command.FocusShipId ?? 0,
                HasFocusShip = command.FocusShipId.HasValue,
                FocusSquadronId = command.FocusSquadronId ?? 0,
                HasFocusSquadron = command.FocusSquadronId.HasValue,
                Retreat = command.Retreat,
                EscortSquadronId = command.EscortSquadronId ?? 0,
                HasEscort = command.EscortSquadronId.HasValue,
                RouteStart = routeCursor,
                RouteCount = command.Route.Count,
            };

            for (int point = 0; point < command.Route.Count; point++)
            {
                _route[routeCursor++] = command.Route[point];
            }

            _squadrons[i] = sim;
        }
    }

    private void LoadShips()
    {
        int count = _source.Ships.Count;
        int[] order = new int[count];

        for (int i = 0; i < count; i++)
        {
            order[i] = i;
        }

        Array.Sort(order, (left, right) => _source.Ships[left].ShipId.CompareTo(_source.Ships[right].ShipId));
        int modules = 0;
        int ammo = 0;
        int hold = 0;
        int accuracy = 0;
        double signature = 0;

        for (int i = 0; i < count; i++)
        {
            ShipState ship = _source.Ships[order[i]];
            modules += ship.Modules.Count;
            ammo += ship.Ammo.Count;
            hold += ship.HoldDrones.Count;

            if (ship.Signature > signature)
            {
                signature = ship.Signature;
            }

            for (int moduleIndex = 0; moduleIndex < ship.Modules.Count; moduleIndex++)
            {
                accuracy += ship.Modules[moduleIndex].Accuracy.Count;
            }

            IReadOnlyList<DronePattern> patterns = ship.DronePatterns ?? [];

            for (int patternIndex = 0; patternIndex < patterns.Count; patternIndex++)
            {
                accuracy += patterns[patternIndex].Accuracy.Count;

                if (patterns[patternIndex].Signature > signature)
                {
                    signature = patterns[patternIndex].Signature;
                }
            }
        }

        for (int i = 0; i < _source.Drones.Count; i++)
        {
            accuracy += _source.Drones[i].Accuracy.Count;

            if (_source.Drones[i].Signature > signature)
            {
                signature = _source.Drones[i].Signature;
            }
        }

        _ships = new SimShip[count];
        _shipCount = count;
        _modules = new SimModule[modules];
        _ammo = new SimStack[ammo];
        _hold = new SimStack[hold];
        _accuracy = new AccuracyEntry[accuracy];
        _patterns = new IReadOnlyList<DronePattern>[count];
        _signatureSpan = signature > 1 ? signature : 1;
        int moduleCursor = 0;
        int ammoCursor = 0;
        int holdCursor = 0;
        int accuracyCursor = 0;

        for (int i = 0; i < count; i++)
        {
            ShipState ship = _source.Ships[order[i]];
            int squadronIndex = SquadronIndex(ship.SquadronId);
            SimShip sim = new()
            {
                ShipId = ship.ShipId,
                SideId = ship.SideId,
                SideIndex = SideIndex(ship.SideId),
                SquadronId = ship.SquadronId,
                SquadronIndex = squadronIndex,
                HullGroupId = ship.HullGroupId,
                X = ship.Position.X,
                Y = ship.Position.Y,
                Z = ship.Position.Z,
                Vx = ship.Velocity.X,
                Vy = ship.Velocity.Y,
                Vz = ship.Velocity.Z,
                Heading = ship.Heading,
                Hull = ship.Hull,
                HullMax = ship.HullMax,
                Armor = ship.Armor,
                ArmorMax = ship.ArmorMax,
                ShieldNow = ship.ShieldNow,
                ShieldCapacity = ship.ShieldCapacity,
                Signature = ship.Signature,
                SignatureCut = ship.SignatureCut,
                EnergyBlock = ship.EnergyBlock,
                KineticBlock = ship.KineticBlock,
                ArmorShare = ship.ArmorShare,
                LoadFactor = ship.LoadFactor > 0 ? ship.LoadFactor : 1,
                RadarBonus = ship.RadarBonus,
                Cost = ship.Cost,
                Retreating = ship.Retreating || (squadronIndex >= 0 && _squadrons[squadronIndex].Retreat),
                Destroyed = ship.Destroyed || ship.Hull <= 0,
                Left = ship.Left,
                IsCarrier = ship.IsCarrier,
                OffsetX = ship.FormationOffset.X,
                OffsetY = ship.FormationOffset.Y,
                OffsetZ = ship.FormationOffset.Z,
                ModuleStart = moduleCursor,
                ModuleCount = ship.Modules.Count,
                AmmoStart = ammoCursor,
                AmmoCount = ship.Ammo.Count,
                HoldStart = holdCursor,
                HoldCount = ship.HoldDrones.Count,
            };

            int[] moduleOrder = new int[ship.Modules.Count];

            for (int moduleIndex = 0; moduleIndex < ship.Modules.Count; moduleIndex++)
            {
                moduleOrder[moduleIndex] = moduleIndex;
            }

            Array.Sort(moduleOrder, (left, right) => ship.Modules[left].ModuleId.CompareTo(ship.Modules[right].ModuleId));

            for (int moduleIndex = 0; moduleIndex < ship.Modules.Count; moduleIndex++)
            {
                ModuleState module = ship.Modules[moduleOrder[moduleIndex]];
                ModuleFacts facts = module.Facts;
                SimModule simModule = new()
                {
                    ModuleId = module.ModuleId,
                    PartTypeId = module.PartTypeId,
                    Kind = KindOf(module.Kind),
                    Integrity = module.Integrity,
                    IntegrityMax = module.IntegrityMax,
                    PowerPriority = module.PowerPriority,
                    Mass = module.Mass,
                    On = module.On && module.Integrity > 0,
                    Cooldown = module.CooldownSeconds,
                    EnergyUse = facts.EnergyUse,
                    Power = facts.Power,
                    Speed = facts.Speed,
                    Detection = facts.Detection,
                    Damage = facts.Damage,
                    Rate = facts.Rate,
                    Range = facts.Range,
                    FlightSpeed = facts.FlightSpeed,
                    DamageKind = HitKindOf(facts.DamageKind),
                    AmmoPartTypeId = facts.AmmoPartTypeId,
                    EnergyBlock = facts.EnergyBlock,
                    KineticBlock = facts.KineticBlock,
                    ShieldCapacity = facts.ShieldCapacity,
                    ShieldRestore = facts.ShieldRestore,
                    AntiMissileChance = facts.AntiMissileChance,
                    LauncherSize = facts.LauncherSize,
                    Repair = facts.Repair,
                    Missiles = facts.Missiles,
                    Magazine = facts.Magazine,
                    KindName = module.Kind ?? "",
                    DamageKindName = facts.DamageKind ?? "",
                    AccuracyStart = accuracyCursor,
                    AccuracyCount = module.Accuracy.Count,
                };
                CopyAccuracy(module.Accuracy, ref accuracyCursor);
                _modules[moduleCursor++] = simModule;
            }

            CopyStacks(ship.Ammo, _ammo, ref ammoCursor);
            CopyStacks(ship.HoldDrones, _hold, ref holdCursor);
            _patterns[i] = ship.DronePatterns ?? [];
            _ships[i] = sim;
        }

        _moduleCount = moduleCursor;
    }

    private void LoadDrones()
    {
        int count = _source.Drones.Count;
        int extra = 0;

        for (int i = 0; i < _shipCount; i++)
        {
            IReadOnlyList<DronePattern> patterns = _patterns[i];

            for (int patternIndex = 0; patternIndex < patterns.Count; patternIndex++)
            {
                if (!patterns[patternIndex].RepairDrone && patterns[patternIndex].Count > 0)
                {
                    extra += patterns[patternIndex].Count;
                }
            }
        }

        _droneRoom = count + extra;
        if (_droneRoom < 1)
        {
            _droneRoom = 1;
        }

        _drones = new SimDrone[_droneRoom];
        int[] order = new int[count];

        for (int i = 0; i < count; i++)
        {
            order[i] = i;
        }

        Array.Sort(order, (left, right) => _source.Drones[left].DroneId.CompareTo(_source.Drones[right].DroneId));
        int accuracyCursor = AccuracyUsed();

        for (int i = 0; i < count; i++)
        {
            DroneState drone = _source.Drones[order[i]];
            int carrier = FindShip(drone.CarrierShipId);
            _drones[i] = ReadDrone(drone, carrier, ref accuracyCursor);

            if (drone.DroneId >= _nextDroneId)
            {
                _nextDroneId = drone.DroneId + 1;
            }
        }

        _droneCount = count;
        _accuracyCount = accuracyCursor;
    }

    private SimDrone ReadDrone(DroneState drone, int carrier, ref int accuracyCursor)
    {
        int squadron = carrier >= 0 ? _ships[carrier].SquadronIndex : -1;
        double radius = drone.EwRadius;

        if (radius <= 0 && drone.EwMalus > 0)
        {
            radius = drone.LeashRadius;
        }

        SimDrone sim = new()
        {
            DroneId = drone.DroneId,
            CarrierShipId = drone.CarrierShipId,
            CarrierIndex = carrier,
            SideId = drone.SideId,
            SideIndex = SideIndex(drone.SideId),
            SquadronIndex = squadron,
            PartTypeId = drone.PartTypeId,
            HullGroupId = drone.HullGroupId,
            X = drone.Position.X,
            Y = drone.Position.Y,
            Z = drone.Position.Z,
            Vx = drone.Velocity.X,
            Vy = drone.Velocity.Y,
            Vz = drone.Velocity.Z,
            Hull = drone.Hull,
            HullMax = drone.HullMax,
            Speed = drone.Speed,
            Signature = drone.Signature,
            EwMalus = drone.EwMalus,
            EwRadius = radius,
            Repair = drone.Repair,
            Leash = drone.LeashRadius,
            RepairDrone = drone.RepairDrone,
            OnArmor = drone.OnArmor,
            Destroyed = drone.Destroyed || drone.Hull <= 0,
            Cooldown = drone.CooldownSeconds,
            Damage = drone.Weapon.Damage,
            Rate = drone.Weapon.Rate,
            Range = drone.Weapon.Range,
            FlightSpeed = drone.Weapon.FlightSpeed,
            DamageKind = HitKindOf(drone.Weapon.DamageKind),
            LauncherSize = drone.Weapon.LauncherSize,
            AccuracyStart = accuracyCursor,
            AccuracyCount = drone.Accuracy.Count,
            HasWeapon = drone.Weapon.Rate > 0,
            WeaponFacts = drone.Weapon,
        };
        CopyAccuracy(drone.Accuracy, ref accuracyCursor);

        return sim;
    }

    private void LoadDebris()
    {
        for (int i = 0; i < _source.Debris.Count; i++)
        {
            _debris.Add(_source.Debris[i]);

            if (_source.Debris[i].DebrisId >= _nextDebrisId)
            {
                _nextDebrisId = _source.Debris[i].DebrisId + 1;
            }
        }
    }

    private void LinkMembers()
    {
        int[] counts = new int[_squadronCount];

        for (int i = 0; i < _shipCount; i++)
        {
            int squadron = _ships[i].SquadronIndex;

            if (squadron >= 0)
            {
                counts[squadron]++;
            }
        }

        _members = new int[_shipCount];
        int cursor = 0;

        for (int i = 0; i < _squadronCount; i++)
        {
            SimSquadron squadron = _squadrons[i];
            squadron.MemberStart = cursor;
            squadron.MemberCount = counts[i];
            _squadrons[i] = squadron;
            cursor += counts[i];
            counts[i] = 0;
        }

        for (int i = 0; i < _shipCount; i++)
        {
            int squadron = _ships[i].SquadronIndex;

            if (squadron < 0)
            {
                continue;
            }

            int slot = _squadrons[squadron].MemberStart + counts[squadron];
            _members[slot] = i;
            counts[squadron]++;
        }
    }

    private void PrepareBuffers()
    {
        int shots = _moduleCount + _droneRoom;

        if (shots < 1)
        {
            shots = 1;
        }

        _shots = new SimShot[shots];
        _shipInstants = new int[shots];
        _droneInstants = new int[shots];
        _xs = new double[_shipCount];
        _ys = new double[_shipCount];
        _query = new int[Math.Max(_shipCount, _droneRoom)];
        _carrier = new bool[_shipCount];
        int cells = _sideCount * Math.Max(_shipCount, 1);
        _visible = new byte[cells];
        _visibleNow = new byte[cells];
        int droneCells = _sideCount * Math.Max(_droneRoom, 1);
        _droneVisible = new byte[droneCells];
        _droneVisibleNow = new byte[droneCells];
        int pairs = Math.Max(_squadronCount * _squadronCount, 1);
        _fired = new int[pairs];
        _hits = new int[pairs];
        _shieldDealt = new double[pairs];
        _armorDealt = new double[pairs];
        _hullDealt = new double[pairs];
        int step = _settings.TrackStepSeconds;
        _maxTrack = step > 0 ? (_tickCount * _tickMilliseconds / (step * CombatSettings.MillisecondsPerSecond)) + 1 : 1;
        _track = new Vec3[Math.Max(_squadronCount, 1) * _maxTrack];
        _trackCount = new int[_squadronCount];
        _cellSize = CellSize();
    }

    private double CellSize()
    {
        double reach = 0;

        for (int i = 0; i < _moduleCount; i++)
        {
            if (_modules[i].Range > reach)
            {
                reach = _modules[i].Range;
            }

            if (_modules[i].Detection > reach)
            {
                reach = _modules[i].Detection;
            }
        }

        for (int i = 0; i < _droneCount; i++)
        {
            if (_drones[i].Range > reach)
            {
                reach = _drones[i].Range;
            }
        }

        if (reach <= 0)
        {
            return _width > 0 ? _width : 1;
        }

        return reach / EngineTune.GridCellsPerRange;
    }

    private void LaunchOrRecall()
    {
        for (int squadronIndex = 0; squadronIndex < _squadronCount; squadronIndex++)
        {
            SquadronOrder command = SourceSquadron(_squadrons[squadronIndex].SquadronId).Order;
            string? logic = command.DroneCommand.Logic;

            if (string.Equals(logic, DroneCommandLogics.Recall, StringComparison.Ordinal))
            {
                Recall(squadronIndex, command.DroneCommand.PartTypeId);
            }
            else if (string.Equals(logic, DroneCommandLogics.Launch, StringComparison.Ordinal))
            {
                Launch(squadronIndex, command.DroneCommand.PartTypeId);
            }
        }
    }

    private void Recall(int squadronIndex, int? partTypeId)
    {
        int write = 0;

        for (int i = 0; i < _droneCount; i++)
        {
            SimDrone drone = _drones[i];
            bool matches = drone.SquadronIndex == squadronIndex
                && !drone.RepairDrone
                && !drone.OnArmor
                && !drone.Destroyed
                && (partTypeId is null || drone.PartTypeId == partTypeId.Value);

            if (matches && drone.CarrierIndex >= 0)
            {
                AddHold(drone.CarrierIndex, drone.PartTypeId, 1);
            }
            else
            {
                _drones[write++] = drone;
            }
        }

        _droneCount = write;
    }

    private void Launch(int squadronIndex, int? partTypeId)
    {
        SimSquadron squadron = _squadrons[squadronIndex];

        for (int member = 0; member < squadron.MemberCount; member++)
        {
            int shipIndex = _members[squadron.MemberStart + member];
            IReadOnlyList<DronePattern> patterns = _patterns[shipIndex];

            for (int patternIndex = 0; patternIndex < patterns.Count; patternIndex++)
            {
                DronePattern pattern = patterns[patternIndex];

                if (pattern.RepairDrone || pattern.Count <= 0)
                {
                    continue;
                }

                if (partTypeId is not null && pattern.PartTypeId != partTypeId.Value)
                {
                    continue;
                }

                if (DroneAlreadyOut(shipIndex, pattern.PartTypeId))
                {
                    continue;
                }

                int stored = HoldCount(shipIndex, pattern.PartTypeId);
                int launched = stored < pattern.Count ? stored : pattern.Count;

                if (launched <= 0)
                {
                    continue;
                }

                AddHold(shipIndex, pattern.PartTypeId, -launched);
                ref SimShip carrier = ref _ships[shipIndex];

                for (int copy = 0; copy < launched; copy++)
                {
                    SpawnDrone(carrier, shipIndex, pattern);
                }
            }
        }
    }

    private void SpawnDrone(SimShip carrier, int shipIndex, DronePattern pattern)
    {
        if (_droneCount >= _drones.Length)
        {
            Array.Resize(ref _drones, _drones.Length * EngineTune.GrowthFactor);
        }

        double radius = pattern.EwMalus > 0 ? pattern.LeashRadius : 0;
        RememberAccuracy(pattern.Accuracy, out int accuracyStart, out int accuracyCount);
        _drones[_droneCount++] = new SimDrone
        {
            DroneId = _nextDroneId++,
            CarrierShipId = carrier.ShipId,
            CarrierIndex = shipIndex,
            SideId = carrier.SideId,
            SideIndex = carrier.SideIndex,
            SquadronIndex = carrier.SquadronIndex,
            PartTypeId = pattern.PartTypeId,
            HullGroupId = pattern.HullGroupId,
            X = carrier.X,
            Y = carrier.Y,
            Z = 0,
            Hull = pattern.Hull,
            HullMax = pattern.Hull,
            Speed = pattern.Speed,
            Signature = pattern.Signature,
            EwMalus = pattern.EwMalus,
            EwRadius = radius,
            Repair = pattern.Repair,
            Leash = pattern.LeashRadius,
            RepairDrone = false,
            OnArmor = false,
            Damage = pattern.Weapon.Damage,
            Rate = pattern.Weapon.Rate,
            Range = pattern.Weapon.Range,
            FlightSpeed = pattern.Weapon.FlightSpeed,
            DamageKind = HitKindOf(pattern.Weapon.DamageKind),
            LauncherSize = pattern.Weapon.LauncherSize,
            AccuracyStart = accuracyStart,
            AccuracyCount = accuracyCount,
            HasWeapon = pattern.Weapon.Rate > 0,
            WeaponFacts = pattern.Weapon,
        };
    }

    private void RememberAccuracy(IReadOnlyList<AccuracyEntry>? rows, out int start, out int count)
    {
        if (rows is null || rows.Count == 0)
        {
            start = 0;
            count = 0;

            return;
        }

        int needed = _accuracyCount + rows.Count;

        if (needed > _accuracy.Length)
        {
            int size = _accuracy.Length > 0 ? _accuracy.Length : needed;

            while (size < needed)
            {
                size *= EngineTune.GrowthFactor;
            }

            Array.Resize(ref _accuracy, size);
        }

        start = _accuracyCount;
        count = rows.Count;

        for (int i = 0; i < rows.Count; i++)
        {
            _accuracy[_accuracyCount++] = rows[i];
        }
    }

    private bool DroneAlreadyOut(int shipIndex, int partTypeId)
    {
        long carrierId = _ships[shipIndex].ShipId;

        for (int i = 0; i < _droneCount; i++)
        {
            if (!_drones[i].Destroyed && _drones[i].CarrierShipId == carrierId && _drones[i].PartTypeId == partTypeId)
            {
                return true;
            }
        }

        return false;
    }

    private int HoldCount(int shipIndex, int partTypeId)
    {
        ref SimShip ship = ref _ships[shipIndex];

        for (int i = 0; i < ship.HoldCount; i++)
        {
            if (_hold[ship.HoldStart + i].PartTypeId == partTypeId)
            {
                return _hold[ship.HoldStart + i].Count;
            }
        }

        return 0;
    }

    private void AddHold(int shipIndex, int partTypeId, int delta)
    {
        ref SimShip ship = ref _ships[shipIndex];

        for (int i = 0; i < ship.HoldCount; i++)
        {
            int slot = ship.HoldStart + i;

            if (_hold[slot].PartTypeId == partTypeId)
            {
                int next = _hold[slot].Count + delta;
                _hold[slot].Count = next > 0 ? next : 0;

                return;
            }
        }
    }

    private void CopyStacks(IReadOnlyDictionary<int, int> source, SimStack[] destination, ref int cursor)
    {
        int[] keys = new int[source.Count];
        int written = 0;

        foreach (KeyValuePair<int, int> pair in source)
        {
            keys[written++] = pair.Key;
        }

        Array.Sort(keys);

        for (int i = 0; i < written; i++)
        {
            destination[cursor++] = new SimStack { PartTypeId = keys[i], Count = source[keys[i]] };
        }
    }

    private void CopyAccuracy(IReadOnlyList<AccuracyEntry> source, ref int cursor)
    {
        for (int i = 0; i < source.Count; i++)
        {
            _accuracy[cursor++] = source[i];
        }
    }

    private int AccuracyUsed()
    {
        int used = 0;

        for (int i = 0; i < _moduleCount; i++)
        {
            int end = _modules[i].AccuracyStart + _modules[i].AccuracyCount;

            if (end > used)
            {
                used = end;
            }
        }

        return used;
    }

    private int SideIndex(int sideId)
    {
        for (int i = 0; i < _sideCount; i++)
        {
            if (_sideIds[i] == sideId)
            {
                return i;
            }
        }

        return -1;
    }

    private int SquadronIndex(long squadronId)
    {
        for (int i = 0; i < _squadronCount; i++)
        {
            if (_squadrons[i].SquadronId == squadronId)
            {
                return i;
            }
        }

        return -1;
    }

    private int FindShip(long shipId)
    {
        int low = 0;
        int high = _shipCount - 1;

        while (low <= high)
        {
            int mid = (low + high) >> 1;
            long found = _ships[mid].ShipId;

            if (found == shipId)
            {
                return mid;
            }

            if (found < shipId)
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return -1;
    }

    private SquadronState SourceSquadron(int squadronId)
    {
        for (int i = 0; i < _source.Squadrons.Count; i++)
        {
            if (_source.Squadrons[i].SquadronId == squadronId)
            {
                return _source.Squadrons[i];
            }
        }

        return _source.Squadrons[0];
    }

    private static byte BehaviorOf(string? logic)
    {
        if (string.Equals(logic, BehaviorLogics.Approach, StringComparison.Ordinal))
        {
            return BehaviorApproach;
        }

        if (string.Equals(logic, BehaviorLogics.KeepDistance, StringComparison.Ordinal))
        {
            return BehaviorKeep;
        }

        if (string.Equals(logic, BehaviorLogics.Escort, StringComparison.Ordinal))
        {
            return BehaviorEscort;
        }

        return BehaviorStand;
    }

    private static byte RuleOf(string? logic)
    {
        if (string.Equals(logic, TargetRuleLogics.Weakest, StringComparison.Ordinal))
        {
            return RuleWeakest;
        }

        if (string.Equals(logic, TargetRuleLogics.Strongest, StringComparison.Ordinal))
        {
            return RuleStrongest;
        }

        if (string.Equals(logic, TargetRuleLogics.Carriers, StringComparison.Ordinal))
        {
            return RuleCarriers;
        }

        if (string.Equals(logic, TargetRuleLogics.Drones, StringComparison.Ordinal))
        {
            return RuleDrones;
        }

        return RuleNearest;
    }

    private static byte KindOf(string? kind)
    {
        if (string.Equals(kind, ModuleKinds.Engine, StringComparison.Ordinal))
        {
            return ModuleEngine;
        }

        if (string.Equals(kind, ModuleKinds.Reactor, StringComparison.Ordinal))
        {
            return ModuleReactor;
        }

        if (string.Equals(kind, ModuleKinds.Weapon, StringComparison.Ordinal))
        {
            return ModuleWeapon;
        }

        if (string.Equals(kind, ModuleKinds.Shield, StringComparison.Ordinal))
        {
            return ModuleShield;
        }

        if (string.Equals(kind, ModuleKinds.Armor, StringComparison.Ordinal))
        {
            return ModuleArmor;
        }

        if (string.Equals(kind, ModuleKinds.Radar, StringComparison.Ordinal))
        {
            return ModuleRadar;
        }

        if (string.Equals(kind, ModuleKinds.AntiMissile, StringComparison.Ordinal))
        {
            return ModuleAntiMissile;
        }

        if (string.Equals(kind, ModuleKinds.ShieldRestorer, StringComparison.Ordinal))
        {
            return ModuleRestorer;
        }

        if (string.Equals(kind, ModuleKinds.Cargo, StringComparison.Ordinal))
        {
            return ModuleCargo;
        }

        return ModuleOther;
    }

    private static byte HitKindOf(string? kind)
    {
        if (string.Equals(kind, DamageKinds.Energy, StringComparison.Ordinal)
            || string.Equals(kind, DamageKinds.Thermal, StringComparison.Ordinal)
            || string.Equals(kind, DamageKinds.Ion, StringComparison.Ordinal))
        {
            return HitEnergy;
        }

        if (string.Equals(kind, DamageKinds.Missile, StringComparison.Ordinal))
        {
            return HitMissile;
        }

        if (string.Equals(kind, DamageKinds.Torpedo, StringComparison.Ordinal))
        {
            return HitTorpedo;
        }

        if (string.Equals(kind, DamageKinds.Explosive, StringComparison.Ordinal))
        {
            return HitExplosive;
        }

        return HitKinetic;
    }
}
