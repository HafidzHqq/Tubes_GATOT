using System;
using System.Drawing;
using System.Collections.Generic;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

// GreedyDuelist
// Strategi Greedy: Hybrid Target Scoring.
// Heuristic: jarak musuh, energi musuh, kesegaran data scan, stabilitas target, dan jarak efektif tembakan.
public class AegisGreedy : Bot
{
    static void Main(string[] args)
    {
        new AegisGreedy().Start();
    }

    AegisGreedy() : base(BotInfo.FromFile("AegisGreedy.json")) { }

    private const double WALL_MARGIN = 95;
    private const double HARD_WALL_MARGIN = 55;

    private readonly Random rand = new Random();
    private readonly Dictionary<int, EnemyInfo> enemies = new Dictionary<int, EnemyInfo>();

    private int enemyId = -1;
    private bool hasEnemy = false;

    private double enemyX;
    private double enemyY;
    private double enemyEnergy = 100;
    private double enemyLastEnergy = 100;
    private double enemySpeed;
    private double enemyDirection;
    private double enemyLastDirection;
    private double enemyDistance;

    private int moveDirection = 1;
    private int orbitDirection = 1;
    private int dodgeTimer = 0;
    private int tick = 0;
    private int lastDirectionChange = 0;

    public override void Run()
    {
        BodyColor = Color.Black;
        TurretColor = Color.Blue;
        RadarColor = Color.Yellow;
        BulletColor = Color.White;
        ScanColor = Color.Cyan;
        TracksColor = Color.Gray;
        GunColor = Color.Red;

        SetFireAssist(false);

        AdjustGunForBodyTurn = true;
        AdjustRadarForBodyTurn = true;
        AdjustRadarForGunTurn = true;

        SetTurnRadarRight(double.PositiveInfinity);

        while (IsRunning)
        {
            tick++;

            SelectBestTarget();

            if (!hasEnemy)
            {
                SearchMove();
            }
            else
            {
                if (IsMeleeMode())
                    MultiWaveMovement();
                else
                    DuelMovement();

                RadarControl();
                AimAndFire();
            }

            Go();
        }
    }

    public override void OnScannedBot(ScannedBotEvent e)
    {
        UpdateEnemyData(e);
        SelectBestTarget();

        if (hasEnemy && e.ScannedBotId == enemyId)
        {
            double energyDrop = enemyLastEnergy - enemyEnergy;

            // Jika energi musuh turun, diasumsikan musuh menembak.
            if (energyDrop > 0.09 && energyDrop <= 3.05)
            {
                dodgeTimer = IsMeleeMode() ? 26 : 20;
                moveDirection *= -1;

                if (rand.NextDouble() < 0.55)
                    orbitDirection *= -1;
            }

            RadarControl();
            AimAndFire();
        }
    }

    private void UpdateEnemyData(ScannedBotEvent e)
    {
        if (!enemies.TryGetValue(e.ScannedBotId, out EnemyInfo info))
        {
            info = new EnemyInfo();
            info.Id = e.ScannedBotId;
            enemies[e.ScannedBotId] = info;
        }

        info.LastEnergy = info.Energy;
        info.LastDirection = info.Direction;

        info.X = e.X;
        info.Y = e.Y;
        info.Energy = e.Energy;
        info.Speed = e.Speed;
        info.Direction = e.Direction;
        info.Distance = DistanceTo(e.X, e.Y);
        info.LastSeen = tick;
        info.Alive = true;
    }

    // Fungsi seleksi greedy: memilih target dengan skor terbaik.
    private void SelectBestTarget()
    {
        int bestId = -1;
        double bestScore = double.NegativeInfinity;

        foreach (EnemyInfo enemy in enemies.Values)
        {
            if (!enemy.Alive)
                continue;

            int age = tick - enemy.LastSeen;
            bool currentTarget = enemy.Id == enemyId;

            int maxAge = currentTarget ? 150 : 85;

            if (age > maxAge)
                continue;

            double distanceScore = 1400 - Math.Min(enemy.Distance, 1400);
            double weakScore = 100 - enemy.Energy;
            double freshScore = Math.Max(0, 90 - age);

            double score = 0;

            score += distanceScore * 0.65;
            score += weakScore * 6.20;
            score += freshScore * 4.50;

            if (currentTarget)
                score += IsMeleeMode() ? 140 : 280;

            if (enemy.Energy < Energy)
                score += 75;

            if (enemy.Distance >= 180 && enemy.Distance <= 620)
                score += 110;

            if (enemy.Distance > 700)
                score += IsMeleeMode() ? -70 : 60;

            if (score > bestScore)
            {
                bestScore = score;
                bestId = enemy.Id;
            }
        }

        if (bestId == -1)
        {
            hasEnemy = false;
            enemyId = -1;
            return;
        }

        enemyId = bestId;
        hasEnemy = true;
        LoadTargetData();
    }

    private void LoadTargetData()
    {
        EnemyInfo enemy = enemies[enemyId];

        enemyX = enemy.X;
        enemyY = enemy.Y;
        enemyEnergy = enemy.Energy;
        enemyLastEnergy = enemy.LastEnergy;
        enemySpeed = enemy.Speed;
        enemyDirection = enemy.Direction;
        enemyLastDirection = enemy.LastDirection;
        enemyDistance = enemy.Distance;
    }

    // Radar dikunci ke target agar data musuh tetap segar.
    private void RadarControl()
    {
        if (!hasEnemy)
        {
            SetTurnRadarRight(double.PositiveInfinity);
            return;
        }

        EnemyInfo enemy = enemies[enemyId];
        int age = tick - enemy.LastSeen;

        if (age > 18)
        {
            double radarTurn = NormalizeBearing(RadarBearingTo(enemy.X, enemy.Y));
            SetTurnRadarLeft(radarTurn * 3.0);
            return;
        }

        if (IsMeleeMode() && enemyDistance < 600 && tick % 42 == 0)
        {
            SetTurnRadarRight(double.PositiveInfinity);
            return;
        }

        double turn = NormalizeBearing(RadarBearingTo(enemy.X, enemy.Y));
        double sign = turn >= 0 ? 1 : -1;
        double extra = Math.Min(36, Math.Max(12, enemyDistance / 45.0));

        SetTurnRadarLeft(turn + sign * extra);
    }

    // Movement 1 vs 1: menjaga jarak, mengejar target, dan menghindari peluru.
    private void DuelMovement()
    {
        if (NearWall())
        {
            WallEscape();
            return;
        }

        double bearing = BearingTo(enemyX, enemyY);
        int targetAge = GetTargetAge();

        if (targetAge > 22 && enemyDistance > 520)
        {
            SetTurnLeft(NormalizeBearing(bearing + 8 * orbitDirection));
            SetForward(230);
            return;
        }

        if (dodgeTimer > 0)
        {
            dodgeTimer--;

            double dodgeAngle = bearing + (92 + 12 * Math.Sin(tick / 4.0)) * orbitDirection;

            if (enemyDistance < 260)
                dodgeAngle = bearing + 135 * orbitDirection;

            if (dodgeTimer % 7 == 0)
                moveDirection *= -1;

            SetTurnLeft(NormalizeBearing(dodgeAngle));
            SetForward(185 * moveDirection);
            return;
        }

        if (tick - lastDirectionChange > 28)
        {
            if (tick % 41 == 0 || tick % 59 == 0 || rand.NextDouble() < 0.015)
            {
                moveDirection *= -1;
                lastDirectionChange = tick;
            }
        }

        if (tick % 83 == 0)
            orbitDirection *= -1;

        double angleOffset;

        if (enemyDistance < 230)
        {
            angleOffset = 140 * orbitDirection;
            SetTurnLeft(NormalizeBearing(bearing + angleOffset));
            SetBack(170);
        }
        else if (enemyDistance < 360)
        {
            angleOffset = 112 * orbitDirection;
            SetTurnLeft(NormalizeBearing(bearing + angleOffset));
            SetForward(170 * moveDirection);
        }
        else if (enemyDistance < 520)
        {
            angleOffset = 86 * orbitDirection;
            double jitter = 8 * Math.Sin(tick / 7.0);

            SetTurnLeft(NormalizeBearing(bearing + angleOffset + jitter));
            SetForward(175 * moveDirection);
        }
        else if (enemyDistance < 720)
        {
            angleOffset = 42 * orbitDirection;
            SetTurnLeft(NormalizeBearing(bearing + angleOffset));
            SetForward(210);
        }
        else
        {
            angleOffset = 12 * orbitDirection;
            SetTurnLeft(NormalizeBearing(bearing + angleOffset));
            SetForward(240);
        }
    }

    // Movement multi-bot: menjauhi ancaman dan tetap mendekati target utama.
    private void MultiWaveMovement()
    {
        if (NearWall())
        {
            WallEscape();
            return;
        }

        if (dodgeTimer > 0)
            dodgeTimer--;

        double forceX = 0;
        double forceY = 0;

        forceX += 7000 / Math.Max(45, X - WALL_MARGIN);
        forceX -= 7000 / Math.Max(45, ArenaWidth - WALL_MARGIN - X);
        forceY += 7000 / Math.Max(45, Y - WALL_MARGIN);
        forceY -= 7000 / Math.Max(45, ArenaHeight - WALL_MARGIN - Y);

        foreach (EnemyInfo enemy in enemies.Values)
        {
            if (!enemy.Alive || tick - enemy.LastSeen > 90)
                continue;

            double dx = X - enemy.X;
            double dy = Y - enemy.Y;
            double distanceSq = Math.Max(10000, dx * dx + dy * dy);
            double distance = Math.Sqrt(distanceSq);

            double force = enemy.Id == enemyId ? 65000 : 115000;

            if (distance < 280)
                force *= 2.5;

            forceX += dx / distance * force / distanceSq;
            forceY += dy / distance * force / distanceSq;
        }

        double targetXVector = enemyX - X;
        double targetYVector = enemyY - Y;
        double targetDistance = Math.Max(1, Math.Sqrt(targetXVector * targetXVector + targetYVector * targetYVector));

        if (targetDistance > 620)
        {
            forceX += targetXVector / targetDistance * 2.20;
            forceY += targetYVector / targetDistance * 2.20;
        }
        else if (targetDistance > 520)
        {
            forceX += targetXVector / targetDistance * 1.60;
            forceY += targetYVector / targetDistance * 1.60;
        }
        else if (targetDistance < 330)
        {
            forceX -= targetXVector / targetDistance * 1.65;
            forceY -= targetYVector / targetDistance * 1.65;
        }

        forceX += -targetYVector / targetDistance * orbitDirection * 1.9;
        forceY += targetXVector / targetDistance * orbitDirection * 1.9;

        if (dodgeTimer > 0)
        {
            forceX += -targetYVector / targetDistance * orbitDirection * 2.4;
            forceY += targetXVector / targetDistance * orbitDirection * 2.4;
        }

        double length = Math.Sqrt(forceX * forceX + forceY * forceY);

        if (length < 0.001)
        {
            DuelMovement();
            return;
        }

        double goalX = X + forceX / length * 230;
        double goalY = Y + forceY / length * 230;

        goalX = Clamp(goalX, WALL_MARGIN, ArenaWidth - WALL_MARGIN);
        goalY = Clamp(goalY, WALL_MARGIN, ArenaHeight - WALL_MARGIN);

        MoveToPoint(goalX, goalY, 185);
    }

    // Shooting greedy: menembak hanya saat peluang hit cukup baik.
    private void AimAndFire()
    {
        if (!hasEnemy)
            return;

        int targetAge = GetTargetAge();

        if (targetAge > 18 && enemyDistance > 520)
            return;

        double firePower = GetFirePower();

        if (firePower <= 0)
            return;

        PointD aimPoint = SmartPrediction(firePower);

        double gunTurn = GunBearingTo(aimPoint.X, aimPoint.Y);
        SetTurnGunLeft(gunTurn);

        double tolerance = GetGunTolerance();

        if (GunHeat == 0 &&
            Math.Abs(gunTurn) <= tolerance &&
            Energy > firePower + 3.5)
        {
            SetFire(firePower);
        }
    }

    private PointD SmartPrediction(double firePower)
    {
        double speedAbs = Math.Abs(enemySpeed);
        double headingChange = NormalizeBearing(enemyDirection - enemyLastDirection);

        if (speedAbs < 0.7)
            return new PointD(enemyX, enemyY);

        if (Math.Abs(headingChange) < 2.2)
            return LinearPrediction(firePower);

        if (enemyDistance < 650)
            return CircularPrediction(firePower);

        return LinearPrediction(firePower);
    }

    private PointD LinearPrediction(double firePower)
    {
        double bulletSpeed = CalcBulletSpeed(firePower);

        double predictedX = enemyX;
        double predictedY = enemyY;
        double heading = enemyDirection * Math.PI / 180.0;

        int time = 0;

        while (time * bulletSpeed < DistanceTo(predictedX, predictedY) && time < 90)
        {
            predictedX += Math.Cos(heading) * enemySpeed;
            predictedY += Math.Sin(heading) * enemySpeed;

            if (!IsSafe(predictedX, predictedY))
            {
                predictedX = Clamp(predictedX, WALL_MARGIN, ArenaWidth - WALL_MARGIN);
                predictedY = Clamp(predictedY, WALL_MARGIN, ArenaHeight - WALL_MARGIN);
                break;
            }

            time++;
        }

        return new PointD(predictedX, predictedY);
    }

    private PointD CircularPrediction(double firePower)
    {
        double bulletSpeed = CalcBulletSpeed(firePower);

        double predictedX = enemyX;
        double predictedY = enemyY;

        double heading = enemyDirection * Math.PI / 180.0;
        double headingChange = NormalizeBearing(enemyDirection - enemyLastDirection) * Math.PI / 180.0;

        headingChange = Clamp(headingChange, -0.16, 0.16);

        int time = 0;

        while (time * bulletSpeed < DistanceTo(predictedX, predictedY) && time < 90)
        {
            heading += headingChange;

            predictedX += Math.Cos(heading) * enemySpeed;
            predictedY += Math.Sin(heading) * enemySpeed;

            if (!IsSafe(predictedX, predictedY))
            {
                predictedX = Clamp(predictedX, WALL_MARGIN, ArenaWidth - WALL_MARGIN);
                predictedY = Clamp(predictedY, WALL_MARGIN, ArenaHeight - WALL_MARGIN);
                break;
            }

            time++;
        }

        return new PointD(predictedX, predictedY);
    }

    // Fire power disesuaikan dengan jarak, energi sendiri, dan energi musuh.
    private double GetFirePower()
    {
        double power;
        bool melee = IsMeleeMode();

        if (Energy < 8)
            return 0.55;

        if (enemyEnergy < 3)
            return 0.65;

        if (enemyDistance < 160)
            power = 3.0;
        else if (enemyDistance < 280)
            power = 2.65;
        else if (enemyDistance < 430)
            power = 2.05;
        else if (enemyDistance < 620)
            power = 1.45;
        else if (enemyDistance < 780)
            power = 1.05;
        else
            power = 0.75;

        if (melee)
            power *= 0.75;

        if (Energy < enemyEnergy && enemyDistance > 430)
            power = Math.Min(power, 1.10);

        if (Energy < 25)
            power = Math.Min(power, 1.15);

        if (enemyEnergy < 12)
            power = Math.Min(power, Math.Max(0.60, enemyEnergy / 4.0 + 0.15));

        return Clamp(power, 0.55, 3.0);
    }

    private double GetGunTolerance()
    {
        if (enemyDistance < 180)
            return 14;

        if (enemyDistance < 320)
            return 10;

        if (enemyDistance < 520)
            return 6.5;

        if (enemyDistance < 720)
            return 4.5;

        return 3.2;
    }

    private void SearchMove()
    {
        SetTurnRadarRight(double.PositiveInfinity);

        if (NearWall())
        {
            WallEscape();
            return;
        }

        EnemyInfo latestEnemy = null;
        int bestAge = 9999;

        foreach (EnemyInfo enemy in enemies.Values)
        {
            if (!enemy.Alive)
                continue;

            int age = tick - enemy.LastSeen;

            if (age < bestAge && age <= 170)
            {
                bestAge = age;
                latestEnemy = enemy;
            }
        }

        if (latestEnemy != null)
        {
            double goalX = Clamp(latestEnemy.X, WALL_MARGIN, ArenaWidth - WALL_MARGIN);
            double goalY = Clamp(latestEnemy.Y, WALL_MARGIN, ArenaHeight - WALL_MARGIN);

            MoveToPoint(goalX, goalY, 210);
            return;
        }

        SetTurnRight(28 * moveDirection);
        SetForward(165 * moveDirection);

        if (tick % 38 == 0)
            moveDirection *= -1;
    }

    private void MoveToPoint(double x, double y, double distance)
    {
        double turn = NormalizeBearing(BearingTo(x, y));

        if (Math.Abs(turn) > 90)
        {
            if (turn > 0)
                turn -= 180;
            else
                turn += 180;

            SetTurnLeft(NormalizeBearing(turn));
            SetBack(distance);
        }
        else
        {
            SetTurnLeft(turn);
            SetForward(distance);
        }
    }

    private void WallEscape()
    {
        double centerX = ArenaWidth / 2.0;
        double centerY = ArenaHeight / 2.0;

        double bearing = BearingTo(centerX, centerY);

        SetTurnLeft(NormalizeBearing(bearing + 18 * orbitDirection));
        SetForward(190);

        if (tick % 10 == 0)
        {
            moveDirection *= -1;
            orbitDirection *= -1;
        }
    }

    public override void OnHitByBullet(HitByBulletEvent e)
    {
        dodgeTimer = IsMeleeMode() ? 30 : 23;

        moveDirection *= -1;

        if (rand.NextDouble() < 0.65)
            orbitDirection *= -1;

        SetTurnRight((55 + rand.Next(35)) * orbitDirection);
        SetForward(190 * moveDirection);
    }

    public override void OnHitWall(HitWallEvent e)
    {
        moveDirection *= -1;
        orbitDirection *= -1;

        SetBack(170);
        SetTurnRight(110);
    }

    public override void OnHitBot(HitBotEvent e)
    {
        moveDirection *= -1;
        orbitDirection *= -1;

        SetBack(220);

        if (GunHeat == 0 && Energy > 18)
            SetFire(3.0);
    }

    public override void OnBotDeath(BotDeathEvent e)
    {
        if (enemies.ContainsKey(e.VictimId))
            enemies[e.VictimId].Alive = false;

        if (e.VictimId == enemyId)
        {
            hasEnemy = false;
            enemyId = -1;
            SelectBestTarget();
            SetTurnRadarRight(double.PositiveInfinity);
        }
    }

    private bool IsMeleeMode()
    {
        return KnownEnemyCount() > 1;
    }

    private int KnownEnemyCount()
    {
        int count = 0;

        foreach (EnemyInfo enemy in enemies.Values)
        {
            if (enemy.Alive && tick - enemy.LastSeen <= 100)
                count++;
        }

        return count;
    }

    private int GetTargetAge()
    {
        if (!hasEnemy || enemyId == -1 || !enemies.ContainsKey(enemyId))
            return 9999;

        return tick - enemies[enemyId].LastSeen;
    }

    private bool NearWall()
    {
        return X < WALL_MARGIN ||
               X > ArenaWidth - WALL_MARGIN ||
               Y < WALL_MARGIN ||
               Y > ArenaHeight - WALL_MARGIN;
    }

    private bool IsSafe(double x, double y)
    {
        return x > HARD_WALL_MARGIN &&
               x < ArenaWidth - HARD_WALL_MARGIN &&
               y > HARD_WALL_MARGIN &&
               y < ArenaHeight - HARD_WALL_MARGIN;
    }

    private double NormalizeBearing(double angle)
    {
        while (angle > 180)
            angle -= 360;

        while (angle < -180)
            angle += 360;

        return angle;
    }

    private double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }
}

public class EnemyInfo
{
    public int Id;

    public double X;
    public double Y;

    public double Energy = 100;
    public double LastEnergy = 100;

    public double Speed;
    public double Direction;
    public double LastDirection;

    public double Distance;
    public int LastSeen;

    public bool Alive = true;
}

public struct PointD
{
    public double X;
    public double Y;

    public PointD(double x, double y)
    {
        X = x;
        Y = y;
    }
}