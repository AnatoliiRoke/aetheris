namespace Aetheris.Combat.Engine;

internal sealed partial class SegmentRun
{
    private void RefreshSpeeds()
    {
        _referenceSpeed = 0;

        for (int i = 0; i < _shipCount; i++)
        {
            ref SimShip ship = ref _ships[i];

            if (ship.Destroyed || ship.Left)
            {
                ship.NominalSpeed = 0;
                ship.ActualSpeed = 0;
                continue;
            }

            double speed = 0;

            for (int moduleIndex = 0; moduleIndex < ship.ModuleCount; moduleIndex++)
            {
                ref SimModule module = ref _modules[ship.ModuleStart + moduleIndex];

                if (module.On && module.Integrity > 0)
                {
                    speed += module.Speed;
                }
            }

            ship.NominalSpeed = speed / ship.LoadFactor;
            ship.ActualSpeed = ship.NominalSpeed * (InsideAsteroid(ship.X, ship.Y, ship.Z) ? _settings.AsteroidSpeedMultiplier : 1);

            if (ship.NominalSpeed > _referenceSpeed)
            {
                _referenceSpeed = ship.NominalSpeed;
            }
        }
    }

    private void RebuildGrid()
    {
        double fastest = 0;

        for (int i = 0; i < _shipCount; i++)
        {
            _xs[i] = _ships[i].X;
            _ys[i] = _ships[i].Y;

            if (_ships[i].ActualSpeed > fastest)
            {
                fastest = _ships[i].ActualSpeed;
            }
        }

        _pad = (EngineTune.GridRebuildPeriod - 1) * fastest * _dt * EngineTune.GridPadSides;
        _grid.Rebuild(_shipCount, _xs, _ys, _width, _height, _cellSize);
    }

    private void MarkVisible(byte[] ships, byte[] drones)
    {
        Array.Clear(ships);
        Array.Clear(drones);

        for (int observer = 0; observer < _shipCount; observer++)
        {
            ref SimShip ship = ref _ships[observer];

            if (ship.Destroyed || ship.Left || ship.SideIndex < 0)
            {
                continue;
            }

            double detect = DetectionOf(observer);

            if (detect <= 0)
            {
                continue;
            }

            double query = (detect * _signatureSpan) + _pad;
            int found = _grid.Collect(ship.X, ship.Y, query, _query);

            for (int index = 0; index < found; index++)
            {
                int target = _query[index];

                if (target == observer || _ships[target].SideId == ship.SideId)
                {
                    continue;
                }

                if (SeesShip(observer, target, detect))
                {
                    ships[(ship.SideIndex * _shipCount) + target] = 1;
                }
            }
        }

        double maxDetect = 0;

        for (int i = 0; i < _shipCount; i++)
        {
            double detect = DetectionOf(i);

            if (detect > maxDetect)
            {
                maxDetect = detect;
            }
        }

        if (maxDetect <= 0 || _droneCount == 0 || _sideCount == 0)
        {
            return;
        }

        for (int droneIndex = 0; droneIndex < _droneCount; droneIndex++)
        {
            ref SimDrone drone = ref _drones[droneIndex];

            if (drone.Destroyed)
            {
                continue;
            }

            int found = _grid.Collect(drone.X, drone.Y, (maxDetect * _signatureSpan) + _pad, _query);

            for (int index = 0; index < found; index++)
            {
                int observer = _query[index];
                ref SimShip ship = ref _ships[observer];

                if (ship.Destroyed || ship.Left || ship.SideIndex < 0 || ship.SideId == drone.SideId)
                {
                    continue;
                }

                double detect = DetectionOf(observer);

                if (SeesPoint(observer, drone.X, drone.Y, drone.Z, drone.Signature, 0, detect))
                {
                    drones[(ship.SideIndex * _droneRoom) + droneIndex] = 1;
                }
            }
        }
    }

    private double DetectionOf(int shipIndex)
    {
        ref SimShip ship = ref _ships[shipIndex];
        double detect = 0;

        for (int i = 0; i < ship.ModuleCount; i++)
        {
            ref SimModule module = ref _modules[ship.ModuleStart + i];

            if (module.On && module.Integrity > 0 && module.Detection > detect)
            {
                detect = module.Detection;
            }
        }

        return detect;
    }

    private bool SeesShip(int observer, int target, double detect)
    {
        ref SimShip ship = ref _ships[target];

        if (ship.Destroyed || ship.Left)
        {
            return false;
        }

        return SeesPoint(observer, ship.X, ship.Y, ship.Z, ship.Signature, ship.SignatureCut, detect);
    }

    private bool SeesPoint(int observer, double x, double y, double z, double signature, double signatureCut, double detect)
    {
        ref SimShip ship = ref _ships[observer];
        double factor = signature * (1 - signatureCut) * (InsideAsteroid(x, y, z) ? _settings.AsteroidDetectionMultiplier : 1);

        if (factor < 0)
        {
            factor = 0;
        }

        double reach = detect * factor;
        double dx = ship.X - x;
        double dy = ship.Y - y;
        double dz = ship.Z - z;

        if (((dx * dx) + (dy * dy) + (dz * dz)) > reach * reach)
        {
            return false;
        }

        return !PlanetBlocks(ship.X, ship.Y, ship.Z, x, y, z);
    }

    private bool ShipVisible(byte[] matrix, int sideIndex, int shipIndex)
    {
        if (sideIndex < 0 || shipIndex < 0 || _shipCount == 0)
        {
            return false;
        }

        return matrix[(sideIndex * _shipCount) + shipIndex] != 0;
    }

    private bool DroneVisible(byte[] matrix, int sideIndex, int droneIndex)
    {
        if (sideIndex < 0 || droneIndex < 0 || _droneRoom == 0)
        {
            return false;
        }

        return matrix[(sideIndex * _droneRoom) + droneIndex] != 0;
    }

    private void MoveSquadrons()
    {
        for (int squadronIndex = 0; squadronIndex < _squadronCount; squadronIndex++)
        {
            SimSquadron squadron = _squadrons[squadronIndex];

            if (squadron.Retreat)
            {
                continue;
            }

            if (!CenterOf(squadronIndex, out double centerX, out double centerY, out double speed))
            {
                continue;
            }

            double step = speed * _dt;
            Vec3 center = new(centerX, centerY, 0);
            Vec3 goal = GoalOf(squadronIndex, center, step);
            Vec3 next = StepAroundPlanets(center, goal, step);
            next = Clamp(next);
            double deltaX = next.X - centerX;
            double deltaY = next.Y - centerY;

            for (int member = 0; member < squadron.MemberCount; member++)
            {
                int shipIndex = _members[squadron.MemberStart + member];
                ref SimShip ship = ref _ships[shipIndex];

                if (ship.Destroyed || ship.Left)
                {
                    continue;
                }

                Vec3 from = new(ship.X, ship.Y, 0);
                Vec3 moved = StepAroundPlanets(from, new Vec3(ship.X + deltaX, ship.Y + deltaY, 0), Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)));
                moved = Clamp(moved);
                ship.Vx = _dt > 0 ? (moved.X - ship.X) / _dt : 0;
                ship.Vy = _dt > 0 ? (moved.Y - ship.Y) / _dt : 0;
                ship.Vz = 0;
                ship.X = moved.X;
                ship.Y = moved.Y;
                ship.Z = 0;
                ship.OffsetX = ship.X - next.X;
                ship.OffsetY = ship.Y - next.Y;
                ship.OffsetZ = 0;

                if (ship.Vx != 0 || ship.Vy != 0)
                {
                    ship.Heading = Math.Atan2(ship.Vy, ship.Vx);
                }
            }

            squadron.CenterX = next.X;
            squadron.CenterY = next.Y;
            squadron.HasCenter = true;
            _squadrons[squadronIndex] = squadron;
        }
    }

    private bool CenterOf(int squadronIndex, out double centerX, out double centerY, out double speed)
    {
        SimSquadron squadron = _squadrons[squadronIndex];
        double sumX = 0;
        double sumY = 0;
        int living = 0;
        speed = 0;
        bool haveSpeed = false;

        for (int member = 0; member < squadron.MemberCount; member++)
        {
            ref SimShip ship = ref _ships[_members[squadron.MemberStart + member]];

            if (ship.Destroyed || ship.Left)
            {
                continue;
            }

            sumX += ship.X;
            sumY += ship.Y;
            living++;

            if (!haveSpeed || ship.ActualSpeed < speed)
            {
                speed = ship.ActualSpeed;
                haveSpeed = true;
            }
        }

        if (living == 0)
        {
            centerX = squadron.CenterX;
            centerY = squadron.CenterY;

            return false;
        }

        centerX = sumX / living;
        centerY = sumY / living;

        return true;
    }

    private Vec3 GoalOf(int squadronIndex, Vec3 center, double step)
    {
        SimSquadron squadron = _squadrons[squadronIndex];
        int route = NextRoutePoint(squadron, center, step);

        if (route >= 0)
        {
            return _route[route];
        }

        if (squadron.Behavior == BehaviorApproach)
        {
            return ApproachGoal(squadronIndex, center);
        }

        if (squadron.Behavior == BehaviorKeep)
        {
            return KeepGoal(squadronIndex, center);
        }

        if (squadron.Behavior == BehaviorEscort)
        {
            return EscortGoal(squadronIndex, center);
        }

        return center;
    }

    private int NextRoutePoint(SimSquadron squadron, Vec3 center, double step)
    {
        double reach = step > 0 ? step * step : 0;

        for (int i = 0; i < squadron.RouteCount; i++)
        {
            Vec3 point = _route[squadron.RouteStart + i];
            double dx = point.X - center.X;
            double dy = point.Y - center.Y;
            double dz = point.Z - center.Z;

            if (((dx * dx) + (dy * dy) + (dz * dz)) > reach)
            {
                return squadron.RouteStart + i;
            }
        }

        return -1;
    }

    private Vec3 ApproachGoal(int squadronIndex, Vec3 center)
    {
        if (!NearestVisibleShip(squadronIndex, center, out int target, out double distance))
        {
            return center;
        }

        double range = MaxRange(squadronIndex);

        if (distance <= range)
        {
            return center;
        }

        return new Vec3(_ships[target].X, _ships[target].Y, 0);
    }

    private Vec3 KeepGoal(int squadronIndex, Vec3 center)
    {
        if (!NearestVisibleShip(squadronIndex, center, out int target, out double distance))
        {
            return center;
        }

        double wanted = EngineTune.KeepDistanceShare * MaxRange(squadronIndex);

        if (wanted <= 0 || distance == wanted)
        {
            return center;
        }

        double targetX = _ships[target].X;
        double targetY = _ships[target].Y;

        if (distance > wanted)
        {
            return new Vec3(targetX, targetY, 0);
        }

        double dx = center.X - targetX;
        double dy = center.Y - targetY;
        double length = Math.Sqrt((dx * dx) + (dy * dy));

        if (length <= 0)
        {
            return new Vec3(center.X + wanted, center.Y, 0);
        }

        return new Vec3(targetX + (dx / length * wanted), targetY + (dy / length * wanted), 0);
    }

    private Vec3 EscortGoal(int squadronIndex, Vec3 center)
    {
        SimSquadron squadron = _squadrons[squadronIndex];

        if (!squadron.HasEscort || !CenterOf(SquadronIndex(squadron.EscortSquadronId), out double escortX, out double escortY, out _))
        {
            return center;
        }

        double wanted = EngineTune.KeepDistanceShare * MaxRange(squadronIndex);
        double dx = center.X - escortX;
        double dy = center.Y - escortY;
        double length = Math.Sqrt((dx * dx) + (dy * dy));

        if (wanted <= 0)
        {
            return new Vec3(escortX, escortY, 0);
        }

        if (length <= 0)
        {
            return new Vec3(escortX + wanted, escortY, 0);
        }

        return new Vec3(escortX + (dx / length * wanted), escortY + (dy / length * wanted), 0);
    }

    private bool NearestVisibleShip(int squadronIndex, Vec3 center, out int target, out double distance)
    {
        SimSquadron squadron = _squadrons[squadronIndex];
        target = -1;
        distance = 0;

        for (int i = 0; i < _shipCount; i++)
        {
            ref SimShip ship = ref _ships[i];

            if (ship.SideId == squadron.SideId || ship.Destroyed || ship.Left || !ShipVisible(_visible, squadron.SideIndex, i))
            {
                continue;
            }

            double dx = ship.X - center.X;
            double dy = ship.Y - center.Y;
            double dz = ship.Z - center.Z;
            double dist2 = (dx * dx) + (dy * dy) + (dz * dz);

            if (target < 0 || dist2 < distance || (dist2 == distance && ship.ShipId < _ships[target].ShipId))
            {
                target = i;
                distance = dist2;
            }
        }

        if (target < 0)
        {
            return false;
        }

        distance = Math.Sqrt(distance);

        return true;
    }

    private double MaxRange(int squadronIndex)
    {
        SimSquadron squadron = _squadrons[squadronIndex];
        double range = 0;

        for (int member = 0; member < squadron.MemberCount; member++)
        {
            ref SimShip ship = ref _ships[_members[squadron.MemberStart + member]];

            if (ship.Destroyed || ship.Left)
            {
                continue;
            }

            for (int moduleIndex = 0; moduleIndex < ship.ModuleCount; moduleIndex++)
            {
                ref SimModule module = ref _modules[ship.ModuleStart + moduleIndex];

                if (module.Kind == ModuleWeapon && module.On && module.Integrity > 0 && module.Range > range)
                {
                    range = module.Range;
                }
            }
        }

        return range;
    }

    private void MoveDrones()
    {
        for (int i = 0; i < _droneCount; i++)
        {
            ref SimDrone drone = ref _drones[i];

            if (drone.Destroyed)
            {
                continue;
            }

            if (drone.CarrierIndex < 0 || _ships[drone.CarrierIndex].Destroyed)
            {
                DestroyDrone(i);
                continue;
            }

            ref SimShip carrier = ref _ships[drone.CarrierIndex];

            if (drone.OnArmor || carrier.Left)
            {
                drone.X = carrier.X;
                drone.Y = carrier.Y;
                drone.Z = 0;
                drone.Vx = 0;
                drone.Vy = 0;
                drone.Vz = 0;
                continue;
            }

            Vec3 from = new(drone.X, drone.Y, 0);
            Vec3 goal = DroneGoal(drone, carrier);
            Vec3 next = StepAroundPlanets(from, goal, drone.Speed * _dt);
            PullToLeash(ref next, carrier, drone.Leash);
            drone.Vx = _dt > 0 ? (next.X - drone.X) / _dt : 0;
            drone.Vy = _dt > 0 ? (next.Y - drone.Y) / _dt : 0;
            drone.Vz = 0;
            drone.X = next.X;
            drone.Y = next.Y;
            drone.Z = 0;
        }
    }

    private Vec3 DroneGoal(SimDrone drone, SimShip carrier)
    {
        int squadronIndex = drone.SquadronIndex;

        if (squadronIndex >= 0)
        {
            SimSquadron squadron = _squadrons[squadronIndex];

            if (squadron.HasFocusShip)
            {
                int focus = FindShip(squadron.FocusShipId);

                if (focus >= 0 && WithinLeash(carrier, _ships[focus].X, _ships[focus].Y, drone.Leash)
                    && ShipVisible(_visible, drone.SideIndex, focus))
                {
                    return new Vec3(_ships[focus].X, _ships[focus].Y, 0);
                }
            }
        }

        int nearest = -1;
        double nearestDistance = 0;

        for (int i = 0; i < _shipCount; i++)
        {
            ref SimShip ship = ref _ships[i];

            if (ship.SideId == drone.SideId || ship.Destroyed || ship.Left || !ShipVisible(_visible, drone.SideIndex, i))
            {
                continue;
            }

            if (!WithinLeash(carrier, ship.X, ship.Y, drone.Leash))
            {
                continue;
            }

            double dx = ship.X - drone.X;
            double dy = ship.Y - drone.Y;
            double dist2 = (dx * dx) + (dy * dy);

            if (nearest < 0 || dist2 < nearestDistance || (dist2 == nearestDistance && ship.ShipId < _ships[nearest].ShipId))
            {
                nearest = i;
                nearestDistance = dist2;
            }
        }

        if (nearest >= 0)
        {
            return new Vec3(_ships[nearest].X, _ships[nearest].Y, 0);
        }

        return new Vec3(drone.X, drone.Y, 0);
    }

    private static bool WithinLeash(SimShip carrier, double x, double y, double leash)
    {
        double dx = x - carrier.X;
        double dy = y - carrier.Y;

        return ((dx * dx) + (dy * dy)) <= leash * leash;
    }

    private static void PullToLeash(ref Vec3 point, SimShip carrier, double leash)
    {
        double dx = point.X - carrier.X;
        double dy = point.Y - carrier.Y;
        double dist2 = (dx * dx) + (dy * dy);

        if (leash < 0)
        {
            leash = 0;
        }

        if (dist2 > leash * leash && dist2 > 0)
        {
            double scale = leash / Math.Sqrt(dist2);
            point = new Vec3(carrier.X + (dx * scale), carrier.Y + (dy * scale), 0);
        }
    }

    private void Retreat()
    {
        for (int squadronIndex = 0; squadronIndex < _squadronCount; squadronIndex++)
        {
            SimSquadron squadron = _squadrons[squadronIndex];

            if (!squadron.Retreat)
            {
                continue;
            }

            for (int member = 0; member < squadron.MemberCount; member++)
            {
                int shipIndex = _members[squadron.MemberStart + member];
                ref SimShip ship = ref _ships[shipIndex];

                if (ship.Destroyed || ship.Left)
                {
                    continue;
                }

                int edge = NearestEdge(ship.X, ship.Y);
                double goalX = ship.X;
                double goalY = ship.Y;

                if (edge == EdgeLeft)
                {
                    goalX = ship.X - ship.ActualSpeed;
                }
                else if (edge == EdgeBottom)
                {
                    goalY = ship.Y - ship.ActualSpeed;
                }
                else if (edge == EdgeRight)
                {
                    goalX = ship.X + ship.ActualSpeed;
                }
                else
                {
                    goalY = ship.Y + ship.ActualSpeed;
                }

                Vec3 next = Pathing.StepToward(new Vec3(ship.X, ship.Y, 0), new Vec3(goalX, goalY, 0), ship.ActualSpeed * _dt);
                ship.Vx = _dt > 0 ? (next.X - ship.X) / _dt : 0;
                ship.Vy = _dt > 0 ? (next.Y - ship.Y) / _dt : 0;
                ship.Vz = 0;
                ship.X = next.X;
                ship.Y = next.Y;
                ship.Z = 0;

                if (ship.Vx != 0 || ship.Vy != 0)
                {
                    ship.Heading = Math.Atan2(ship.Vy, ship.Vx);
                }

                if (ship.X < 0 || ship.Y < 0 || ship.X > _width || ship.Y > _height)
                {
                    ship.Left = true;
                    ship.Retreating = true;
                }
            }

            LogSquadronLeft(squadronIndex);
        }
    }

    private int NearestEdge(double x, double y)
    {
        double best = x;
        int edge = EdgeLeft;
        double bottom = y;

        if (bottom < best)
        {
            best = bottom;
            edge = EdgeBottom;
        }

        double right = _width - x;

        if (right < best)
        {
            best = right;
            edge = EdgeRight;
        }

        double top = _height - y;

        if (top < best)
        {
            edge = EdgeTop;
        }

        return edge;
    }

    private void LogSquadronLeft(int squadronIndex)
    {
        SimSquadron squadron = _squadrons[squadronIndex];

        if (squadron.LeftLogged)
        {
            return;
        }

        bool anyLiving = false;
        bool anyAboard = false;

        for (int member = 0; member < squadron.MemberCount; member++)
        {
            ref SimShip ship = ref _ships[_members[squadron.MemberStart + member]];

            if (ship.Destroyed)
            {
                continue;
            }

            anyLiving = true;

            if (!ship.Left)
            {
                anyAboard = true;
            }
        }

        if (anyLiving && !anyAboard)
        {
            squadron.LeftLogged = true;
            _squadrons[squadronIndex] = squadron;
            AddLog(new BattleLogEntry(
                _currentTick,
                LogTypes.SquadronLeft,
                squadron.SquadronId,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0));
        }
    }

    private void WriteTrack(int tick)
    {
        int stepMs = _settings.TrackStepSeconds * CombatSettings.MillisecondsPerSecond;

        if (stepMs <= 0 || (((tick + 1) * _tickMilliseconds) % stepMs) != 0)
        {
            return;
        }

        for (int squadronIndex = 0; squadronIndex < _squadronCount; squadronIndex++)
        {
            if (_trackCount[squadronIndex] >= _maxTrack)
            {
                continue;
            }

            if (!CenterOf(squadronIndex, out double centerX, out double centerY, out _))
            {
                centerX = _squadrons[squadronIndex].CenterX;
                centerY = _squadrons[squadronIndex].CenterY;
            }

            _track[(squadronIndex * _maxTrack) + _trackCount[squadronIndex]] = new Vec3(centerX, centerY, 0);
            _trackCount[squadronIndex]++;
            SimSquadron squadron = _squadrons[squadronIndex];
            squadron.CenterX = centerX;
            squadron.CenterY = centerY;
            squadron.HasCenter = true;
            _squadrons[squadronIndex] = squadron;
        }
    }

    private Vec3 StepAroundPlanets(Vec3 from, Vec3 goal, double step)
    {
        Vec3 next = Pathing.StepToward(from, goal, step);

        for (int i = 0; i < _planetCount; i++)
        {
            Circle planet = _planets[i];
            Vec3 center = new(planet.X, planet.Y, planet.Z);

            if (LineOfSight.Blocks(from, next, center, planet.Radius))
            {
                next = Pathing.AvoidCircle(from, goal, center, planet.Radius, step);
            }
        }

        return next;
    }

    private Vec3 Clamp(Vec3 point)
    {
        double x = point.X;
        double y = point.Y;

        if (x < 0)
        {
            x = 0;
        }
        else if (x > _width)
        {
            x = _width;
        }

        if (y < 0)
        {
            y = 0;
        }
        else if (y > _height)
        {
            y = _height;
        }

        return new Vec3(x, y, 0);
    }

    private bool InsideAsteroid(double x, double y, double z)
    {
        for (int i = 0; i < _asteroidCount; i++)
        {
            Circle field = _asteroids[i];
            double dx = x - field.X;
            double dy = y - field.Y;
            double dz = z - field.Z;

            if (((dx * dx) + (dy * dy) + (dz * dz)) <= field.Radius * field.Radius)
            {
                return true;
            }
        }

        return false;
    }

    private bool PlanetBlocks(double fromX, double fromY, double fromZ, double toX, double toY, double toZ)
    {
        Vec3 from = new(fromX, fromY, fromZ);
        Vec3 to = new(toX, toY, toZ);

        for (int i = 0; i < _planetCount; i++)
        {
            Circle planet = _planets[i];

            if (LineOfSight.Blocks(from, to, new Vec3(planet.X, planet.Y, planet.Z), planet.Radius))
            {
                return true;
            }
        }

        return false;
    }
}
