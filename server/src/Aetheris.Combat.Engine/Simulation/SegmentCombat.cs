namespace Aetheris.Combat.Engine;

internal sealed partial class SegmentRun
{
    private void FireShips(int tick)
    {
        RefreshCarriers();

        for (int shipIndex = 0; shipIndex < _shipCount; shipIndex++)
        {
            ref SimShip ship = ref _ships[shipIndex];

            if (ship.Destroyed || ship.Left || ship.Hull <= 0)
            {
                continue;
            }

            int moduleEnd = ship.ModuleStart + ship.ModuleCount;

            for (int moduleIndex = ship.ModuleStart; moduleIndex < moduleEnd; moduleIndex++)
            {
                FireModule(shipIndex, moduleIndex, tick);
            }
        }
    }

    private void FireDrones(int tick)
    {
        for (int droneIndex = 0; droneIndex < _droneCount; droneIndex++)
        {
            FireDrone(droneIndex, tick);
        }
    }

    private void FireModule(int shipIndex, int moduleIndex, int tick)
    {
        ref SimModule module = ref _modules[moduleIndex];

        if (!module.On || module.Integrity <= 0 || module.Rate <= 0)
        {
            return;
        }

        module.Cooldown -= _dt;

        if (module.Cooldown > 0)
        {
            return;
        }

        ref SimShip ship = ref _ships[shipIndex];

        if (!TryTarget(
            ship.SquadronIndex,
            ship.SideIndex,
            ship.SideId,
            ship.X,
            ship.Y,
            ship.Z,
            module.Range,
            out bool atDrone,
            out int targetIndex))
        {
            module.Cooldown = 0;

            return;
        }

        if (!TryConsumeAmmo(shipIndex, module.AmmoPartTypeId))
        {
            module.Cooldown = 0;

            return;
        }

        module.Cooldown = 1 / module.Rate;
        bool instant = module.DamageKind == HitEnergy || module.FlightSpeed <= 0;
        double distance = TargetDistance(ship.X, ship.Y, ship.Z, atDrone, targetIndex);
        int hullGroup = atDrone ? _drones[targetIndex].HullGroupId : _ships[targetIndex].HullGroupId;
        int toSquadron = atDrone ? _drones[targetIndex].SquadronIndex : _ships[targetIndex].SquadronIndex;
        PushShot(new SimShot
        {
            Id = _nextShotId++,
            Instant = instant,
            FromDrone = false,
            ShooterShip = shipIndex,
            ShooterDrone = -1,
            TargetShip = atDrone ? -1 : targetIndex,
            TargetDrone = atDrone ? targetIndex : -1,
            OriginX = ship.X,
            OriginY = ship.Y,
            OriginZ = ship.Z,
            Speed = module.FlightSpeed,
            Damage = module.Damage,
            Kind = module.DamageKind,
            ImpactTick = instant ? tick : ImpactTick(tick, distance, module.FlightSpeed),
            Accuracy = AccuracyFor(module.AccuracyStart, module.AccuracyCount, hullGroup),
            Range = module.Range,
            Interceptable = Interceptable(module.DamageKind, module.LauncherSize),
            FromSquadron = ship.SquadronIndex,
            ToSquadron = toSquadron,
            RadarBonus = ship.RadarBonus,
        });
    }

    private void FireDrone(int droneIndex, int tick)
    {
        ref SimDrone drone = ref _drones[droneIndex];

        if (drone.Destroyed || drone.OnArmor || !drone.HasWeapon || drone.Rate <= 0 || drone.Hull <= 0)
        {
            return;
        }

        if (drone.CarrierIndex >= 0 && _ships[drone.CarrierIndex].Destroyed)
        {
            return;
        }

        drone.Cooldown -= _dt;

        if (drone.Cooldown > 0)
        {
            return;
        }

        if (!TryTarget(
            drone.SquadronIndex,
            drone.SideIndex,
            drone.SideId,
            drone.X,
            drone.Y,
            drone.Z,
            drone.Range,
            out bool atDrone,
            out int targetIndex))
        {
            drone.Cooldown = 0;

            return;
        }

        drone.Cooldown = 1 / drone.Rate;
        bool instant = drone.DamageKind == HitEnergy || drone.FlightSpeed <= 0;
        double distance = TargetDistance(drone.X, drone.Y, drone.Z, atDrone, targetIndex);
        int hullGroup = atDrone ? _drones[targetIndex].HullGroupId : _ships[targetIndex].HullGroupId;
        int toSquadron = atDrone ? _drones[targetIndex].SquadronIndex : _ships[targetIndex].SquadronIndex;
        double radar = drone.CarrierIndex >= 0 ? _ships[drone.CarrierIndex].RadarBonus : 0;
        PushShot(new SimShot
        {
            Id = _nextShotId++,
            Instant = instant,
            FromDrone = true,
            ShooterShip = drone.CarrierIndex,
            ShooterDrone = droneIndex,
            TargetShip = atDrone ? -1 : targetIndex,
            TargetDrone = atDrone ? targetIndex : -1,
            OriginX = drone.X,
            OriginY = drone.Y,
            OriginZ = drone.Z,
            Speed = drone.FlightSpeed,
            Damage = drone.Damage,
            Kind = drone.DamageKind,
            ImpactTick = instant ? tick : ImpactTick(tick, distance, drone.FlightSpeed),
            Accuracy = AccuracyFor(drone.AccuracyStart, drone.AccuracyCount, hullGroup),
            Range = drone.Range,
            Interceptable = Interceptable(drone.DamageKind, drone.LauncherSize),
            FromSquadron = drone.SquadronIndex,
            ToSquadron = toSquadron,
            RadarBonus = radar,
        });
    }

    private void ForceShots(int tick)
    {
        for (int i = 0; i < _shotCount; i++)
        {
            if (!_shots[i].Resolved && !_shots[i].Instant)
            {
                _shots[i].ImpactTick = tick;
            }
        }
    }

    private void ResolveInstants(int[] indexes, int count)
    {
        for (int i = 0; i < count; i++)
        {
            ResolveShot(indexes[i]);
        }
    }

    private void ResolveProjectiles(int tick)
    {
        for (int i = 0; i < _shotCount; i++)
        {
            if (_shots[i].Resolved || _shots[i].Instant || _shots[i].ImpactTick > tick)
            {
                continue;
            }

            ResolveShot(i);
        }
    }

    private void ResolveShot(int index)
    {
        if (_shots[index].Resolved)
        {
            return;
        }

        _shots[index].Resolved = true;
        SimShot shot = _shots[index];

        if (shot.TargetDrone >= 0)
        {
            ResolveDroneHit(shot);
        }
        else
        {
            ResolveShipHit(shot);
        }
    }

    private void ResolveShipHit(SimShot shot)
    {
        int target = shot.TargetShip;

        if (target < 0 || target >= _shipCount)
        {
            return;
        }

        ref SimShip ship = ref _ships[target];

        if (ship.Destroyed || ship.Left || ship.Hull <= 0)
        {
            return;
        }

        int sideIndex = ShotSide(shot);

        if (!ShipVisible(_visibleNow, sideIndex, target))
        {
            return;
        }

        if (PlanetBlocks(shot.OriginX, shot.OriginY, shot.OriginZ, ship.X, ship.Y, ship.Z))
        {
            return;
        }

        if (shot.Interceptable && StoppedByAntiMissile(target))
        {
            return;
        }

        if (_rng.NextDouble() >= HitOdds(shot, ship.X, ship.Y, ship.Z, ship.Vx, ship.Vy, ship.Vz, ship.SideId))
        {
            return;
        }

        ApplyShipDamage(shot, target);
    }

    private void ResolveDroneHit(SimShot shot)
    {
        int target = shot.TargetDrone;

        if (target < 0 || target >= _droneCount)
        {
            return;
        }

        ref SimDrone drone = ref _drones[target];

        if (drone.Destroyed || drone.Hull <= 0)
        {
            return;
        }

        int sideIndex = ShotSide(shot);

        if (!DroneVisible(_droneVisibleNow, sideIndex, target))
        {
            return;
        }

        if (PlanetBlocks(shot.OriginX, shot.OriginY, shot.OriginZ, drone.X, drone.Y, drone.Z))
        {
            return;
        }

        if (_rng.NextDouble() >= HitOdds(shot, drone.X, drone.Y, drone.Z, drone.Vx, drone.Vy, drone.Vz, drone.SideId))
        {
            return;
        }

        double before = drone.Hull;
        double after = before - shot.Damage;

        if (after < 0)
        {
            after = 0;
        }

        drone.Hull = after;
        NoteHit(shot.FromSquadron, shot.ToSquadron, 0, 0, before > after ? before - after : 0);

        if (after <= 0)
        {
            _pendingFromSquadron = SquadronIdOf(shot.FromSquadron);
            DestroyDrone(target);
        }
    }

    private void ApplyShipDamage(SimShot shot, int target)
    {
        ref SimShip ship = ref _ships[target];
        double shieldBefore = ship.ShieldNow;
        double armorBefore = ship.Armor;
        double hullBefore = ship.Hull;
        DamageHit hit = DamageResolution.Apply(
            shot.Damage,
            Block(target, shot.Kind),
            shieldBefore,
            armorBefore,
            ship.ArmorShare,
            hullBefore,
            0,
            _settings.ModuleDamageShare);
        ship.ShieldNow = hit.ShieldNow > 0 ? hit.ShieldNow : 0;
        ship.Armor = hit.Armor > 0 ? hit.Armor : 0;
        ship.Hull = hit.Hull > 0 ? hit.Hull : 0;
        NoteHit(
            shot.FromSquadron,
            shot.ToSquadron,
            shieldBefore > ship.ShieldNow ? shieldBefore - ship.ShieldNow : 0,
            armorBefore > ship.Armor ? armorBefore - ship.Armor : 0,
            hullBefore > ship.Hull ? hullBefore - ship.Hull : 0);
        int fromSquadron = SquadronIdOf(shot.FromSquadron);
        int toSquadron = SquadronIdOf(shot.ToSquadron);

        if (hit.ShieldDown && !ship.ShieldLogged)
        {
            ship.ShieldLogged = true;
            Log(LogTypes.ShieldDown, fromSquadron, toSquadron, ship.ShipId, 0, 0, 0);
        }

        double moduleLoss = hit.ModuleIntegrity < 0 ? -hit.ModuleIntegrity : 0;

        if (moduleLoss > 0)
        {
            StrikeModule(target, moduleLoss, fromSquadron, toSquadron);
        }

        if (ship.Hull <= 0)
        {
            DestroyShip(target, fromSquadron);
        }
    }

    private void StrikeModule(int shipIndex, double moduleLoss, int fromSquadron, int toSquadron)
    {
        int moduleIndex = PickModule(shipIndex, moduleLoss);

        if (moduleIndex < 0)
        {
            return;
        }

        ref SimModule module = ref _modules[moduleIndex];
        double before = module.Integrity;
        module.Integrity = before - moduleLoss;

        if (module.Integrity < 0)
        {
            module.Integrity = 0;
        }

        if (before <= 0 || module.Integrity > 0)
        {
            return;
        }

        module.On = false;

        if (!module.DestructionLogged)
        {
            module.DestructionLogged = true;
            Log(LogTypes.ModuleDestroyed, fromSquadron, toSquadron, _ships[shipIndex].ShipId, module.ModuleId, 0, 0);
        }

        if (module.Kind == ModuleCargo)
        {
            ClearShipStores(shipIndex);
        }
    }

    private int PickModule(int shipIndex, double moduleLoss)
    {
        if (moduleLoss <= 0)
        {
            return -1;
        }

        ref SimShip ship = ref _ships[shipIndex];
        int lowest = -1;
        long lowestId = 0;
        double totalMass = 0;

        for (int offset = 0; offset < ship.ModuleCount; offset++)
        {
            ref SimModule module = ref _modules[ship.ModuleStart + offset];

            if (module.Integrity <= 0)
            {
                continue;
            }

            if (lowest < 0 || module.ModuleId < lowestId)
            {
                lowest = ship.ModuleStart + offset;
                lowestId = module.ModuleId;
            }

            if (module.Mass > 0)
            {
                totalMass += module.Mass;
            }
        }

        if (lowest < 0 || totalMass <= 0)
        {
            return lowest;
        }

        double mark = _rng.NextDouble() * totalMass;
        double walked = 0;

        for (int offset = 0; offset < ship.ModuleCount; offset++)
        {
            ref SimModule module = ref _modules[ship.ModuleStart + offset];

            if (module.Integrity <= 0 || module.Mass <= 0)
            {
                continue;
            }

            walked += module.Mass;

            if (mark < walked)
            {
                return ship.ModuleStart + offset;
            }
        }

        return lowest;
    }

    private void BalancePower()
    {
        for (int shipIndex = 0; shipIndex < _shipCount; shipIndex++)
        {
            if (!_ships[shipIndex].Destroyed && _ships[shipIndex].Hull > 0)
            {
                BalanceShip(shipIndex);
            }
        }
    }

    private void BalanceShip(int shipIndex)
    {
        ref SimShip ship = ref _ships[shipIndex];

        while (true)
        {
            double supply = 0;
            double demand = 0;
            int shut = -1;
            int shutPriority = 0;
            long shutId = 0;

            for (int offset = 0; offset < ship.ModuleCount; offset++)
            {
                ref SimModule module = ref _modules[ship.ModuleStart + offset];

                if (!module.On || module.Integrity <= 0)
                {
                    continue;
                }

                if (module.Kind == ModuleReactor)
                {
                    supply += module.Power;
                }

                if (module.EnergyUse <= 0)
                {
                    continue;
                }

                demand += module.EnergyUse;

                if (shut < 0
                    || module.PowerPriority > shutPriority
                    || (module.PowerPriority == shutPriority && module.ModuleId > shutId))
                {
                    shut = ship.ModuleStart + offset;
                    shutPriority = module.PowerPriority;
                    shutId = module.ModuleId;
                }
            }

            if (supply >= demand || shut < 0)
            {
                return;
            }

            _modules[shut].On = false;
        }
    }

    private void CompactShots()
    {
        int write = 0;

        for (int i = 0; i < _shotCount; i++)
        {
            if (!_shots[i].Resolved)
            {
                _shots[write++] = _shots[i];
            }
        }

        _shotCount = write;
    }

    private SegmentResult Finish()
    {
        AppendSummaries();
        BattleLogEntry[] entries = new BattleLogEntry[_log.Count];

        for (int i = 0; i < entries.Length; i++)
        {
            entries[i] = _log[i];
        }

        return new SegmentResult(BuildState(), BuildTracks(), entries, Decide());
    }

    private BattleState BuildState()
    {
        ShipState[] ships = new ShipState[_shipCount];

        for (int i = 0; i < _shipCount; i++)
        {
            ships[i] = BuildShip(i);
        }

        int flying = 0;

        for (int i = 0; i < _droneCount; i++)
        {
            if (!_drones[i].Destroyed && _drones[i].Hull > 0)
            {
                flying++;
            }
        }

        DroneState[] drones = new DroneState[flying];
        int droneCursor = 0;

        for (int i = 0; i < _droneCount; i++)
        {
            if (!_drones[i].Destroyed && _drones[i].Hull > 0)
            {
                drones[droneCursor++] = BuildDrone(i);
            }
        }

        _debris.Sort(CompareDebris);
        DebrisState[] debris = new DebrisState[_debris.Count];

        for (int i = 0; i < debris.Length; i++)
        {
            debris[i] = _debris[i];
        }

        return new BattleState(
            _source.BattleId,
            _source.Cycle,
            _source.Seed,
            _source.Field,
            BuildSides(),
            BuildSquadrons(),
            ships,
            drones,
            debris);
    }

    private ShipState BuildShip(int shipIndex)
    {
        ref SimShip ship = ref _ships[shipIndex];
        ModuleState[] modules = new ModuleState[ship.ModuleCount];

        for (int offset = 0; offset < ship.ModuleCount; offset++)
        {
            modules[offset] = BuildModule(ship.ModuleStart + offset);
        }

        bool destroyed = ship.Destroyed || ship.Hull <= 0;

        return new ShipState(
            ship.ShipId,
            ship.SideId,
            ship.SquadronId,
            ship.HullGroupId,
            new Vec3(ship.X, ship.Y, ship.Z),
            new Vec3(ship.Vx, ship.Vy, ship.Vz),
            ship.Heading,
            ship.Hull > 0 ? ship.Hull : 0,
            ship.HullMax,
            ship.Armor > 0 ? ship.Armor : 0,
            ship.ArmorMax,
            ship.ShieldNow > 0 ? ship.ShieldNow : 0,
            ship.ShieldCapacity,
            ship.Signature,
            ship.SignatureCut,
            modules,
            Pack(_ammo, ship.AmmoStart, ship.AmmoCount),
            Pack(_hold, ship.HoldStart, ship.HoldCount),
            ship.Retreating,
            destroyed,
            ship.Left,
            new Vec3(ship.OffsetX, ship.OffsetY, ship.OffsetZ),
            ship.LoadFactor,
            ship.RadarBonus,
            ship.EnergyBlock,
            ship.KineticBlock,
            ship.ArmorShare,
            ship.Cost,
            ship.IsCarrier,
            _patterns[shipIndex]);
    }

    private ModuleState BuildModule(int moduleIndex)
    {
        ref SimModule module = ref _modules[moduleIndex];
        string kind = string.IsNullOrEmpty(module.KindName) ? KindText(module.Kind) : module.KindName;
        double integrity = module.Integrity > 0 ? module.Integrity : 0;
        ModuleFacts facts = new(
            module.EnergyUse,
            module.Power,
            module.Speed,
            module.Detection,
            module.Damage,
            module.Rate,
            module.Range,
            module.FlightSpeed,
            module.DamageKindName ?? "",
            module.AmmoPartTypeId,
            module.EnergyBlock,
            module.KineticBlock,
            module.ShieldCapacity,
            module.ShieldRestore,
            module.Repair,
            module.AntiMissileChance,
            module.Missiles,
            module.LauncherSize,
            module.Magazine);

        return new ModuleState(
            module.ModuleId,
            module.PartTypeId,
            kind,
            integrity,
            module.IntegrityMax,
            module.PowerPriority,
            module.Mass,
            module.On && integrity > 0,
            module.Cooldown,
            facts,
            AccuracySlice(module.AccuracyStart, module.AccuracyCount));
    }

    private DroneState BuildDrone(int droneIndex)
    {
        ref SimDrone drone = ref _drones[droneIndex];

        return new DroneState(
            drone.DroneId,
            drone.CarrierShipId,
            drone.SideId,
            drone.PartTypeId,
            drone.HullGroupId,
            new Vec3(drone.X, drone.Y, drone.Z),
            new Vec3(drone.Vx, drone.Vy, drone.Vz),
            drone.Hull > 0 ? drone.Hull : 0,
            drone.HullMax,
            drone.Speed,
            drone.Signature,
            drone.EwMalus,
            drone.EwRadius,
            drone.Repair,
            drone.Leash,
            drone.RepairDrone,
            drone.OnArmor,
            false,
            drone.Cooldown,
            drone.WeaponFacts,
            AccuracySlice(drone.AccuracyStart, drone.AccuracyCount));
    }

    private SideState[] BuildSides()
    {
        SideState[] sides = new SideState[_sideCount];

        for (int i = 0; i < _sideCount; i++)
        {
            sides[i] = SourceSide(_sideIds[i]);
        }

        return sides;
    }

    private SideState SourceSide(int sideId)
    {
        for (int i = 0; i < _source.Sides.Count; i++)
        {
            if (_source.Sides[i].SideId == sideId)
            {
                return _source.Sides[i];
            }
        }

        return new SideState(sideId, false);
    }

    private SquadronState[] BuildSquadrons()
    {
        SquadronState[] squadrons = new SquadronState[_squadronCount];

        for (int i = 0; i < _squadronCount; i++)
        {
            squadrons[i] = SourceSquadron(_squadrons[i].SquadronId);
        }

        return squadrons;
    }

    private SquadronTrack[] BuildTracks()
    {
        SquadronTrack[] tracks = new SquadronTrack[_squadronCount];

        for (int i = 0; i < _squadronCount; i++)
        {
            int count = _trackCount[i];
            Vec3[] points = new Vec3[count];

            for (int point = 0; point < count; point++)
            {
                points[point] = _track[(i * _maxTrack) + point];
            }

            tracks[i] = new SquadronTrack(_squadrons[i].SquadronId, points);
        }

        return tracks;
    }

    private void AppendSummaries()
    {
        int tick = _tickCount > 0 ? _tickCount - 1 : 0;

        for (int from = 0; from < _squadronCount; from++)
        {
            for (int to = 0; to < _squadronCount; to++)
            {
                int slot = (from * _squadronCount) + to;

                if (_fired[slot] <= 0)
                {
                    continue;
                }

                _log.Add(new BattleLogEntry(
                    tick,
                    LogTypes.FireSummary,
                    _squadrons[from].SquadronId,
                    _squadrons[to].SquadronId,
                    0,
                    0,
                    0,
                    0,
                    _fired[slot],
                    _hits[slot],
                    _shieldDealt[slot],
                    _armorDealt[slot],
                    _hullDealt[slot]));
            }
        }
    }

    private BattleOutcome Decide()
    {
        int firstSide = 0;
        int secondSide = 0;
        int distinct = 0;

        for (int i = 0; i < _shipCount; i++)
        {
            ref SimShip ship = ref _ships[i];

            if (ship.Destroyed || ship.Left || ship.Hull <= 0)
            {
                continue;
            }

            if (distinct == 0)
            {
                firstSide = ship.SideId;
                distinct = OneSideLeft;
            }
            else if (distinct == OneSideLeft && ship.SideId != firstSide)
            {
                secondSide = ship.SideId;
                distinct = TwoSidesLeft;
            }
            else if (ship.SideId != firstSide && ship.SideId != secondSide)
            {
                distinct = ManySidesLeft;
            }
        }

        if (distinct == OneSideLeft)
        {
            return BattleOutcome.Won(firstSide);
        }

        if (distinct == 0 || _source.Cycle >= _settings.MaxCycles)
        {
            return BattleOutcome.Draw;
        }

        return BattleOutcome.Continue;
    }

    private void DestroyShip(int shipIndex, int fromSquadronId)
    {
        ref SimShip ship = ref _ships[shipIndex];

        if (ship.DestructionLogged)
        {
            ship.Destroyed = true;

            return;
        }

        ship.Destroyed = true;
        ship.DestructionLogged = true;

        if (ship.Hull > 0)
        {
            ship.Hull = 0;
        }

        ClearShipStores(shipIndex);
        Log(LogTypes.ShipDestroyed, fromSquadronId, SquadronIdOf(ship.SquadronIndex), ship.ShipId, 0, 0, 0);
        int savedFrom = _pendingFromSquadron;
        _pendingFromSquadron = fromSquadronId;

        for (int i = 0; i < _droneCount; i++)
        {
            if (_drones[i].CarrierIndex == shipIndex)
            {
                DestroyDrone(i);
            }
        }

        _pendingFromSquadron = savedFrom;
        double quantity = ship.Cost * _settings.DebrisShare;

        if (ship.Cost != 0 && quantity != 0 && double.IsFinite(quantity))
        {
            _debris.Add(new DebrisState(
                _nextDebrisId++,
                new Vec3(ship.X, ship.Y, ship.Z),
                quantity,
                false,
                ship.ShipId));
        }
    }

    private void DestroyDrone(int droneIndex)
    {
        ref SimDrone drone = ref _drones[droneIndex];

        if (drone.DestructionLogged)
        {
            drone.Destroyed = true;

            return;
        }

        drone.Destroyed = true;
        drone.DestructionLogged = true;

        if (drone.Hull > 0)
        {
            drone.Hull = 0;
        }

        Log(
            LogTypes.DroneDestroyed,
            _pendingFromSquadron,
            SquadronIdOf(drone.SquadronIndex),
            drone.CarrierShipId,
            0,
            drone.DroneId,
            0);
    }

    private void AddLog(BattleLogEntry entry)
    {
        _log.Add(entry);
    }

    private void Log(string type, int fromSquadronId, int toSquadronId, long shipId, long moduleId, long droneId, long debrisId)
    {
        _log.Add(new BattleLogEntry(
            _currentTick,
            type,
            fromSquadronId,
            toSquadronId,
            shipId,
            moduleId,
            droneId,
            debrisId,
            0,
            0,
            0,
            0,
            0));
    }

    private void ClearShipStores(int shipIndex)
    {
        ref SimShip ship = ref _ships[shipIndex];

        for (int i = 0; i < ship.AmmoCount; i++)
        {
            _ammo[ship.AmmoStart + i].Count = 0;
        }

        for (int i = 0; i < ship.HoldCount; i++)
        {
            _hold[ship.HoldStart + i].Count = 0;
        }
    }

    private bool TryConsumeAmmo(int shipIndex, int partTypeId)
    {
        if (partTypeId == 0)
        {
            return true;
        }

        ref SimShip ship = ref _ships[shipIndex];

        for (int i = 0; i < ship.AmmoCount; i++)
        {
            int slot = ship.AmmoStart + i;

            if (_ammo[slot].PartTypeId == partTypeId && _ammo[slot].Count > 0)
            {
                _ammo[slot].Count--;

                return true;
            }
        }

        return false;
    }

    private void RefreshCarriers()
    {
        Array.Clear(_carrier);

        for (int i = 0; i < _shipCount; i++)
        {
            if (_ships[i].IsCarrier)
            {
                _carrier[i] = true;

                continue;
            }

            for (int stack = 0; stack < _ships[i].HoldCount; stack++)
            {
                if (_hold[_ships[i].HoldStart + stack].Count > 0)
                {
                    _carrier[i] = true;

                    break;
                }
            }
        }

        for (int i = 0; i < _droneCount; i++)
        {
            int carrier = _drones[i].CarrierIndex;

            if (!_drones[i].Destroyed && carrier >= 0 && carrier < _shipCount)
            {
                _carrier[carrier] = true;
            }
        }
    }

    private bool TryTarget(
        int squadronIndex,
        int sideIndex,
        int sideId,
        double fromX,
        double fromY,
        double fromZ,
        double range,
        out bool atDrone,
        out int targetIndex)
    {
        atDrone = false;
        targetIndex = -1;

        if (range < 0)
        {
            range = 0;
        }

        double reach2 = range * range;
        byte rule = RuleNearest;
        SimSquadron squadron = default;

        if (squadronIndex >= 0 && squadronIndex < _squadronCount)
        {
            squadron = _squadrons[squadronIndex];
            rule = squadron.Rule;

            if (FocusShipInReach(squadron, sideId, sideIndex, fromX, fromY, fromZ, reach2, out int focusShip))
            {
                targetIndex = focusShip;

                return true;
            }

            if (FocusSquadronInReach(squadron, sideId, sideIndex, fromX, fromY, fromZ, reach2, out int focusSquadronShip))
            {
                targetIndex = focusSquadronShip;

                return true;
            }
        }

        if (rule == RuleDrones && NearestDrone(sideId, sideIndex, fromX, fromY, fromZ, reach2, out int droneIndex))
        {
            atDrone = true;
            targetIndex = droneIndex;

            return true;
        }

        if (rule == RuleDrones)
        {
            rule = RuleNearest;
        }

        return NearestRuleShip(rule, sideId, sideIndex, fromX, fromY, fromZ, range, reach2, out targetIndex);
    }

    private bool FocusShipInReach(
        SimSquadron squadron,
        int sideId,
        int sideIndex,
        double fromX,
        double fromY,
        double fromZ,
        double reach2,
        out int targetIndex)
    {
        targetIndex = -1;

        if (!squadron.HasFocusShip)
        {
            return false;
        }

        int focus = FindShip(squadron.FocusShipId);

        if (focus < 0 || !ShipInReach(focus, sideId, sideIndex, fromX, fromY, fromZ, reach2, out _))
        {
            return false;
        }

        targetIndex = focus;

        return true;
    }

    private bool FocusSquadronInReach(
        SimSquadron squadron,
        int sideId,
        int sideIndex,
        double fromX,
        double fromY,
        double fromZ,
        double reach2,
        out int targetIndex)
    {
        targetIndex = -1;

        if (!squadron.HasFocusSquadron)
        {
            return false;
        }

        int focusIndex = SquadronIndex(squadron.FocusSquadronId);

        if (focusIndex < 0)
        {
            return false;
        }

        SimSquadron focus = _squadrons[focusIndex];
        int best = -1;
        double bestDistance = 0;
        long bestShipId = 0;

        for (int member = 0; member < focus.MemberCount; member++)
        {
            int candidate = _members[focus.MemberStart + member];

            if (!ShipInReach(candidate, sideId, sideIndex, fromX, fromY, fromZ, reach2, out double distance))
            {
                continue;
            }

            long shipId = _ships[candidate].ShipId;

            if (best < 0 || distance < bestDistance || (distance == bestDistance && shipId < bestShipId))
            {
                best = candidate;
                bestDistance = distance;
                bestShipId = shipId;
            }
        }

        if (best < 0)
        {
            return false;
        }

        targetIndex = best;

        return true;
    }

    private bool NearestDrone(
        int sideId,
        int sideIndex,
        double fromX,
        double fromY,
        double fromZ,
        double reach2,
        out int targetIndex)
    {
        targetIndex = -1;
        double bestDistance = 0;
        long bestDroneId = 0;

        for (int i = 0; i < _droneCount; i++)
        {
            ref SimDrone drone = ref _drones[i];

            if (drone.Destroyed || drone.Hull <= 0 || drone.SideId == sideId || !DroneVisible(_droneVisible, sideIndex, i))
            {
                continue;
            }

            double distance = Vec3.DistanceSquared(new Vec3(fromX, fromY, fromZ), new Vec3(drone.X, drone.Y, drone.Z));

            if (distance > reach2)
            {
                continue;
            }

            if (targetIndex < 0 || distance < bestDistance || (distance == bestDistance && drone.DroneId < bestDroneId))
            {
                targetIndex = i;
                bestDistance = distance;
                bestDroneId = drone.DroneId;
            }
        }

        return targetIndex >= 0;
    }

    private bool NearestRuleShip(
        byte rule,
        int sideId,
        int sideIndex,
        double fromX,
        double fromY,
        double fromZ,
        double range,
        double reach2,
        out int targetIndex)
    {
        targetIndex = -1;
        int found = _grid.Collect(fromX, fromY, range + _pad, _query);
        double bestDistance = 0;
        double bestPool = 0;
        long bestShipId = 0;
        bool bestCarrier = false;

        for (int i = 0; i < found; i++)
        {
            int candidate = _query[i];

            if (!ShipInReach(candidate, sideId, sideIndex, fromX, fromY, fromZ, reach2, out double distance))
            {
                continue;
            }

            ref SimShip ship = ref _ships[candidate];
            double pool = ship.Hull + ship.Armor + ship.ShieldNow;
            bool carrier = _carrier[candidate];
            bool take;

            if (targetIndex < 0)
            {
                take = true;
            }
            else if (rule == RuleWeakest)
            {
                take = pool < bestPool || (pool == bestPool && ship.ShipId < bestShipId);
            }
            else if (rule == RuleStrongest)
            {
                take = pool > bestPool || (pool == bestPool && ship.ShipId < bestShipId);
            }
            else if (rule == RuleCarriers)
            {
                if (carrier != bestCarrier)
                {
                    take = carrier;
                }
                else
                {
                    take = distance < bestDistance || (distance == bestDistance && ship.ShipId < bestShipId);
                }
            }
            else
            {
                take = distance < bestDistance || (distance == bestDistance && ship.ShipId < bestShipId);
            }

            if (!take)
            {
                continue;
            }

            targetIndex = candidate;
            bestDistance = distance;
            bestPool = pool;
            bestShipId = ship.ShipId;
            bestCarrier = carrier;
        }

        return targetIndex >= 0;
    }

    private bool ShipInReach(
        int candidate,
        int sideId,
        int sideIndex,
        double fromX,
        double fromY,
        double fromZ,
        double reach2,
        out double distance)
    {
        distance = 0;
        ref SimShip ship = ref _ships[candidate];

        if (ship.Destroyed || ship.Left || ship.Hull <= 0 || ship.SideId == sideId)
        {
            return false;
        }

        if (!ShipVisible(_visible, sideIndex, candidate))
        {
            return false;
        }

        distance = Vec3.DistanceSquared(new Vec3(fromX, fromY, fromZ), new Vec3(ship.X, ship.Y, ship.Z));

        return distance <= reach2;
    }

    private bool StoppedByAntiMissile(int shipIndex)
    {
        double chance = 0;
        ref SimShip ship = ref _ships[shipIndex];

        for (int offset = 0; offset < ship.ModuleCount; offset++)
        {
            ref SimModule module = ref _modules[ship.ModuleStart + offset];

            if (module.Kind == ModuleAntiMissile && module.On && module.Integrity > 0 && module.AntiMissileChance > chance)
            {
                chance = module.AntiMissileChance;
            }
        }

        if (chance <= 0)
        {
            return false;
        }

        return _rng.NextDouble() < chance;
    }

    private double HitOdds(SimShot shot, double x, double y, double z, double vx, double vy, double vz, int sideId)
    {
        double distance = Math.Sqrt(Vec3.DistanceSquared(
            new Vec3(shot.OriginX, shot.OriginY, shot.OriginZ),
            new Vec3(x, y, z)));

        return HitChance.Compute(
            shot.Accuracy,
            distance,
            shot.Range,
            _settings.DistanceFalloff,
            Math.Sqrt((vx * vx) + (vy * vy) + (vz * vz)),
            _referenceSpeed,
            _settings.TargetSpeedFalloff,
            shot.RadarBonus,
            EwAt(sideId, x, y, z),
            InsideAsteroid(x, y, z) ? _settings.AsteroidHitMultiplier : 1);
    }

    private double EwAt(int sideId, double x, double y, double z)
    {
        double malus = 0;

        for (int i = 0; i < _droneCount; i++)
        {
            ref SimDrone drone = ref _drones[i];

            if (drone.Destroyed || drone.SideId == sideId || drone.EwMalus <= 0 || drone.EwRadius <= 0 || drone.EwMalus <= malus)
            {
                continue;
            }

            double distance = Vec3.DistanceSquared(new Vec3(drone.X, drone.Y, drone.Z), new Vec3(x, y, z));

            if (distance <= drone.EwRadius * drone.EwRadius)
            {
                malus = drone.EwMalus;
            }
        }

        return malus;
    }

    private double Block(int shipIndex, byte kind)
    {
        ref SimShip ship = ref _ships[shipIndex];
        double weight = 0;
        double energy = 0;
        double kinetic = 0;

        for (int offset = 0; offset < ship.ModuleCount; offset++)
        {
            ref SimModule module = ref _modules[ship.ModuleStart + offset];

            if (module.Kind != ModuleShield || !module.On || module.Integrity <= 0)
            {
                continue;
            }

            double cap = module.ShieldCapacity > 0 ? module.ShieldCapacity : module.Integrity;

            if (cap <= 0)
            {
                continue;
            }

            weight += cap;
            energy += cap * module.EnergyBlock;
            kinetic += cap * module.KineticBlock;
        }

        double energyBlock = weight > 0 ? energy / weight : ship.EnergyBlock;
        double kineticBlock = weight > 0 ? kinetic / weight : ship.KineticBlock;

        return kind == HitEnergy ? energyBlock : kineticBlock;
    }

    private double AccuracyFor(int start, int count, int hullGroupId)
    {
        for (int i = 0; i < count; i++)
        {
            if (_accuracy[start + i].HullGroupId == hullGroupId)
            {
                return _accuracy[start + i].Chance;
            }
        }

        return _settings.AccuracyDefault;
    }

    private static bool Interceptable(byte kind, double launcherSize)
    {
        return launcherSize > 0 || kind == HitMissile || kind == HitTorpedo || kind == HitExplosive;
    }

    private double TargetDistance(double fromX, double fromY, double fromZ, bool atDrone, int targetIndex)
    {
        if (atDrone)
        {
            ref SimDrone drone = ref _drones[targetIndex];

            return Math.Sqrt(Vec3.DistanceSquared(new Vec3(fromX, fromY, fromZ), new Vec3(drone.X, drone.Y, drone.Z)));
        }

        ref SimShip ship = ref _ships[targetIndex];

        return Math.Sqrt(Vec3.DistanceSquared(new Vec3(fromX, fromY, fromZ), new Vec3(ship.X, ship.Y, ship.Z)));
    }

    private int ImpactTick(int tick, double distance, double speed)
    {
        if (speed <= 0 || distance <= 0 || _dt <= 0)
        {
            return tick;
        }

        double quotient = distance / (speed * _dt);

        if (quotient >= int.MaxValue)
        {
            return int.MaxValue;
        }

        int whole = (int)quotient;

        if (whole < quotient)
        {
            whole++;
        }

        long impact = (long)tick + whole;

        if (impact > int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)impact;
    }

    private int ShotSide(SimShot shot)
    {
        if (shot.FromDrone)
        {
            return shot.ShooterDrone >= 0 && shot.ShooterDrone < _droneCount ? _drones[shot.ShooterDrone].SideIndex : -1;
        }

        return shot.ShooterShip >= 0 && shot.ShooterShip < _shipCount ? _ships[shot.ShooterShip].SideIndex : -1;
    }

    private void PushShot(in SimShot shot)
    {
        EnsureShotRoom();
        int index = _shotCount;
        _shots[index] = shot;
        _shotCount = index + 1;

        if (shot.FromSquadron >= 0
            && shot.ToSquadron >= 0
            && shot.FromSquadron < _squadronCount
            && shot.ToSquadron < _squadronCount)
        {
            _fired[(shot.FromSquadron * _squadronCount) + shot.ToSquadron]++;
        }

        if (!shot.Instant)
        {
            return;
        }

        if (shot.FromDrone)
        {
            _droneInstants[_droneInstantCount++] = index;
        }
        else
        {
            _shipInstants[_shipInstantCount++] = index;
        }
    }

    private void EnsureShotRoom()
    {
        if (_shotCount < _shots.Length)
        {
            return;
        }

        int size = _shots.Length * EngineTune.GrowthFactor;

        if (size <= _shots.Length)
        {
            size = _shots.Length + 1;
        }

        Array.Resize(ref _shots, size);
        Array.Resize(ref _shipInstants, size);
        Array.Resize(ref _droneInstants, size);
    }

    private void NoteHit(int fromSquadron, int toSquadron, double shield, double armor, double hull)
    {
        if (fromSquadron < 0 || toSquadron < 0 || fromSquadron >= _squadronCount || toSquadron >= _squadronCount)
        {
            return;
        }

        int slot = (fromSquadron * _squadronCount) + toSquadron;
        _hits[slot]++;
        _shieldDealt[slot] += shield;
        _armorDealt[slot] += armor;
        _hullDealt[slot] += hull;
    }

    private int SquadronIdOf(int squadronIndex)
    {
        if (squadronIndex < 0 || squadronIndex >= _squadronCount)
        {
            return 0;
        }

        return _squadrons[squadronIndex].SquadronId;
    }

    private static SortedDictionary<int, int> Pack(SimStack[] stacks, int start, int count)
    {
        SortedDictionary<int, int> packed = new();

        for (int i = 0; i < count; i++)
        {
            SimStack stack = stacks[start + i];

            if (stack.Count > 0)
            {
                packed[stack.PartTypeId] = stack.Count;
            }
        }

        return packed;
    }

    private AccuracyEntry[] AccuracySlice(int start, int count)
    {
        if (count <= 0)
        {
            return [];
        }

        AccuracyEntry[] rows = new AccuracyEntry[count];

        for (int i = 0; i < count; i++)
        {
            rows[i] = _accuracy[start + i];
        }

        return rows;
    }

    private static string KindText(byte kind)
    {
        if (kind == ModuleEngine)
        {
            return ModuleKinds.Engine;
        }

        if (kind == ModuleReactor)
        {
            return ModuleKinds.Reactor;
        }

        if (kind == ModuleWeapon)
        {
            return ModuleKinds.Weapon;
        }

        if (kind == ModuleShield)
        {
            return ModuleKinds.Shield;
        }

        if (kind == ModuleArmor)
        {
            return ModuleKinds.Armor;
        }

        if (kind == ModuleRadar)
        {
            return ModuleKinds.Radar;
        }

        if (kind == ModuleAntiMissile)
        {
            return ModuleKinds.AntiMissile;
        }

        if (kind == ModuleRestorer)
        {
            return ModuleKinds.ShieldRestorer;
        }

        if (kind == ModuleCargo)
        {
            return ModuleKinds.Cargo;
        }

        return ModuleKinds.Other;
    }

    private static int CompareDebris(DebrisState left, DebrisState right) => left.DebrisId.CompareTo(right.DebrisId);
}
