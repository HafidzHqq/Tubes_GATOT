using System;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

// RangeKeeperGreedy
// Strategi Greedy: Ideal Distance.
// Heuristic: jarak musuh, energi musuh, arah gerak musuh, dan deteksi tembakan musuh.
public class RangeKeeperGreedy : Bot
{
    static void Main(string[] args)
    {
        new RangeKeeperGreedy().Start();
    }

    RangeKeeperGreedy() : base(BotInfo.FromFile("RangeKeeperGreedy.json")) { }

    private double targetX = -1;
    private double targetY = -1;

    private double enemyEnergy = 100;
    private double lastEnemyEnergy = 100;
    private double enemySpeed = 0;
    private double enemyDirection = 0;
    private double lastEnemyDirection = 0;

    private int moveDirection = 1;
    private int orbitDirection = 1;
    private long lastScanTurn = 0;
    private int dodgeTimer = 0;

    private const double WALL_MARGIN = 70;
    private const double MIN_DISTANCE = 180;
    private const double IDEAL_DISTANCE = 300;
    private const double MAX_DISTANCE = 430;

    public override void Run()
    {
        BodyColor = Color.Black;
        TurretColor = Color.Red;
        RadarColor = Color.Yellow;
        BulletColor = Color.Black;
        ScanColor = Color.Gray;
        TracksColor = Color.Green;
        GunColor = Color.White;

        SetFireAssist(false);

        AdjustGunForBodyTurn = true;
        AdjustRadarForBodyTurn = true;
        AdjustRadarForGunTurn = true;

        SetTurnRadarRight(double.PositiveInfinity);

        while (IsRunning)
        {
            if (targetX < 0 || targetY < 0 || TurnNumber - lastScanTurn > 25)
            {
                SearchMovement();
            }
            else
            {
                double distance = DistanceTo(targetX, targetY);

                if (dodgeTimer > 0)
                {
                    DodgeMovement();
                    dodgeTimer--;
                }
                else if (distance > MAX_DISTANCE)
                {
                    MoveTowardTarget(distance);
                }
                else if (distance < MIN_DISTANCE)
                {
                    MoveAwayFromTarget();
                }
                else
                {
                    StrafeAroundTarget();
                }
            }

            Go();
        }
    }

    public override void OnScannedBot(ScannedBotEvent e)
    {
        targetX = e.X;
        targetY = e.Y;
        lastScanTurn = TurnNumber;

        double distance = DistanceTo(targetX, targetY);

        lastEnemyDirection = enemyDirection;
        enemyDirection = e.Direction;
        enemySpeed = e.Speed;
        enemyEnergy = e.Energy;

        LockRadar();

        DetectEnemyFire();

        double firePower = GetFirePower(distance);
        PointD aimPoint = PredictTarget(firePower);

        double gunBearing = GunBearingTo(aimPoint.X, aimPoint.Y);
        SetTurnGunLeft(gunBearing);

        double tolerance = GetGunTolerance(distance);

        if (GunHeat == 0 && Math.Abs(gunBearing) < tolerance && Energy > firePower + 4)
        {
            SetFire(firePower);
        }

        if (TurnNumber % 37 == 0)
            moveDirection *= -1;

        if (TurnNumber % 61 == 0)
            orbitDirection *= -1;
    }

    private void LockRadar()
    {
        double radarTurn = NormalizeBearing(RadarBearingTo(targetX, targetY));
        SetTurnRadarLeft(radarTurn * 2.4);
    }

    // Fungsi greedy utama: mencari target jika belum ada data musuh.
    private void SearchMovement()
    {
        SetTurnRadarRight(double.PositiveInfinity);

        if (NearWall())
        {
            MoveToCenter();
            return;
        }

        SetTurnRight(18 * moveDirection);
        SetForward(130 * moveDirection);

        if (TurnNumber % 40 == 0)
            moveDirection *= -1;
    }

    // Jika musuh terlalu jauh, bot mendekat.
    private void MoveTowardTarget(double distance)
    {
        double bearing = BearingTo(targetX, targetY);

        SetTurnLeft(NormalizeBearing(bearing + 25 * orbitDirection));
        SetForward(Math.Min(distance - IDEAL_DISTANCE, 160));
    }

    // Jika musuh terlalu dekat, bot menjauh.
    private void MoveAwayFromTarget()
    {
        double bearing = BearingTo(targetX, targetY);

        SetTurnLeft(NormalizeBearing(bearing + 135 * orbitDirection));
        SetBack(160);
    }

    // Jika jarak ideal, bot bergerak menyamping.
    private void StrafeAroundTarget()
    {
        if (NearWall())
        {
            MoveToCenter();
            return;
        }

        double bearing = BearingTo(targetX, targetY);
        double distance = DistanceTo(targetX, targetY);

        double angle = bearing + 90 * orbitDirection;

        if (distance < IDEAL_DISTANCE - 40)
            angle += 28 * orbitDirection;
        else if (distance > IDEAL_DISTANCE + 40)
            angle -= 22 * orbitDirection;

        SetTurnLeft(NormalizeBearing(angle));
        SetForward(155 * moveDirection);
    }

    private void DetectEnemyFire()
    {
        double energyDrop = lastEnemyEnergy - enemyEnergy;

        if (energyDrop > 0.1 && energyDrop <= 3.0)
        {
            moveDirection *= -1;
            dodgeTimer = 10;

            if (TurnNumber % 2 == 0)
                orbitDirection *= -1;
        }

        lastEnemyEnergy = enemyEnergy;
    }

    // Dodge dilakukan ketika musuh diduga menembak.
    private void DodgeMovement()
    {
        if (NearWall())
        {
            MoveToCenter();
            return;
        }

        double bearing = BearingTo(targetX, targetY);

        if (dodgeTimer % 3 == 0)
        {
            SetForward(0);
            SetTurnRight(35 * orbitDirection);
        }
        else
        {
            SetTurnLeft(NormalizeBearing(bearing + 95 * orbitDirection));
            SetForward(175 * moveDirection);
        }
    }

    private PointD PredictTarget(double firePower)
    {
        double bulletSpeed = CalcBulletSpeed(firePower);

        double predictedX = targetX;
        double predictedY = targetY;

        double heading = enemyDirection * Math.PI / 180.0;
        double headingChange = NormalizeRadians((enemyDirection - lastEnemyDirection) * Math.PI / 180.0);

        headingChange = Clamp(headingChange, -0.10, 0.10);

        int time = 0;

        while (time * bulletSpeed < DistanceTo(predictedX, predictedY) && time < 70)
        {
            heading += headingChange;

            predictedX += Math.Cos(heading) * enemySpeed;
            predictedY += Math.Sin(heading) * enemySpeed;

            if (predictedX < WALL_MARGIN || predictedX > ArenaWidth - WALL_MARGIN ||
                predictedY < WALL_MARGIN || predictedY > ArenaHeight - WALL_MARGIN)
            {
                predictedX = Clamp(predictedX, WALL_MARGIN, ArenaWidth - WALL_MARGIN);
                predictedY = Clamp(predictedY, WALL_MARGIN, ArenaHeight - WALL_MARGIN);
                break;
            }

            time++;
        }

        return new PointD(predictedX, predictedY);
    }

    // Fire power dipilih berdasarkan jarak dan energi.
    private double GetFirePower(double distance)
    {
        double power;

        if (Energy < 12)
            power = 0.7;
        else if (enemyEnergy < 8)
            power = Math.Min(1.5, enemyEnergy / 3.0 + 0.2);
        else if (distance < 140)
            power = 3.0;
        else if (distance < 300)
            power = 2.4;
        else if (distance < 500)
            power = 1.7;
        else
            power = 1.1;

        if (Energy < 25)
            power = Math.Min(power, 1.4);

        return Clamp(power, 0.5, 3.0);
    }

    private double GetGunTolerance(double distance)
    {
        if (distance < 160)
            return 18;

        if (distance < 320)
            return 12;

        return 8;
    }

    private void MoveToCenter()
    {
        double bearing = BearingTo(ArenaWidth / 2.0, ArenaHeight / 2.0);

        SetTurnLeft(NormalizeBearing(bearing));
        SetForward(180);

        moveDirection *= -1;
    }

    public override void OnHitByBullet(HitByBulletEvent e)
    {
        moveDirection *= -1;
        orbitDirection *= -1;
        dodgeTimer = 14;

        SetTurnRight(50 * orbitDirection);
        SetForward(170 * moveDirection);
    }

    public override void OnHitBot(HitBotEvent e)
    {
        moveDirection *= -1;
        orbitDirection *= -1;

        SetBack(150);

        if (GunHeat == 0 && Energy > 15)
            SetFire(3.0);

        Rescan();
    }

    public override void OnHitWall(HitWallEvent e)
    {
        moveDirection *= -1;
        orbitDirection *= -1;

        SetBack(120);
        SetTurnRight(90);
    }

    private bool NearWall()
    {
        return X < WALL_MARGIN ||
               X > ArenaWidth - WALL_MARGIN ||
               Y < WALL_MARGIN ||
               Y > ArenaHeight - WALL_MARGIN;
    }

    private double NormalizeBearing(double angle)
    {
        while (angle > 180)
            angle -= 360;

        while (angle < -180)
            angle += 360;

        return angle;
    }

    private double NormalizeRadians(double angle)
    {
        while (angle > Math.PI)
            angle -= 2 * Math.PI;

        while (angle < -Math.PI)
            angle += 2 * Math.PI;

        return angle;
    }

    private double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }
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