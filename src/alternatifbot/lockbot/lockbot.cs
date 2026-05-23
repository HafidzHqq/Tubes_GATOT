using System;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

// OrbitLockGreedy
// Strategi Greedy: Candidate Position Scoring.
// Heuristic: jarak ideal, gerak lateral, jarak dinding, risiko pojok, dan kondisi energi.
public class OrbitLockGreedy : Bot
{
    static void Main(string[] args)
    {
        new OrbitLockGreedy().Start();
    }

    OrbitLockGreedy() : base(BotInfo.FromFile("lockbot.json")) { }

    private double targetX = -1;
    private double targetY = -1;
    private double lastTargetX = -1;
    private double lastTargetY = -1;

    private double targetEnergy = 100;
    private double enemyVelocity = 0;
    private double enemyHeading = 0;

    private int moveDirection = 1;
    private int radarDirection = 1;

    private long lastScanTurn = 0;
    private long lastFireTurn = -999;
    private long lastEnemyFireTurn = -999;
    private long wallEscapeUntil = 0;

    private const double WALL_MARGIN = 85;
    private const double IDEAL_DISTANCE = 280;
    private const double MIN_DISTANCE = 155;
    private const double MAX_DISTANCE = 430;

    public override void Run()
    {
        BodyColor = Color.Black;
        TurretColor = Color.Red;
        RadarColor = Color.Yellow;
        BulletColor = Color.White;
        ScanColor = Color.Gray;
        TracksColor = Color.Green;
        GunColor = Color.DarkRed;

        SetFireAssist(true);

        AdjustGunForBodyTurn = true;
        AdjustRadarForBodyTurn = true;
        AdjustRadarForGunTurn = true;

        while (IsRunning)
        {
            bool hasTarget = targetX >= 0 && targetY >= 0 && TurnNumber - lastScanTurn <= 18;

            if (IsNearWall(110) || TurnNumber < wallEscapeUntil)
            {
                WallEscapeMove();

                if (hasTarget)
                    LockRadar();
                else
                    SetTurnRadarRight(360 * radarDirection);
            }
            else if (!hasTarget)
            {
                SearchTarget();
            }
            else
            {
                LockRadar();
                ChooseGreedyMove();
            }

            Go();
        }
    }

    public override void OnScannedBot(ScannedBotEvent e)
    {
        lastTargetX = targetX;
        lastTargetY = targetY;

        targetX = e.X;
        targetY = e.Y;
        lastScanTurn = TurnNumber;

        UpdateEnemyMovement();
        DetectEnemyFire(e.Energy);

        double distance = DistanceTo(targetX, targetY);

        LockRadar();
        AimAndFire(distance);

        targetEnergy = e.Energy;
    }

    private void SearchTarget()
    {
        SetTurnRadarRight(360 * radarDirection);

        if (IsNearWall(120))
        {
            WallEscapeMove();
            return;
        }

        SetTurnRight(25 * moveDirection);
        SetForward(120);
    }

    // Radar dikunci agar data target tetap baru.
    private void LockRadar()
    {
        double absoluteBearing = AbsoluteBearingTo(X, Y, targetX, targetY);
        double radarTurn = NormalizeBearing(absoluteBearing - RadarDirection);

        SetTurnRadarRight(radarTurn * 2.0);
    }

    // Menembak hanya saat gun cukup akurat dan energi aman.
    private void AimAndFire(double distance)
    {
        double firePower = GetFirePower(distance);

        PredictTargetPosition(firePower, out double predictedX, out double predictedY);

        double gunTurn = GunBearingTo(predictedX, predictedY);
        SetTurnGunLeft(gunTurn);

        double aimLimit = GetAimLimit(distance);
        int cooldown = GetFireCooldown(distance);

        bool gunLocked = Math.Abs(gunTurn) <= aimLimit;
        bool cooldownReady = TurnNumber - lastFireTurn >= cooldown;
        bool enoughEnergy = Energy > firePower + 4.0;
        bool targetFresh = TurnNumber - lastScanTurn <= 1;

        if (distance > 430 && Math.Abs(gunTurn) > 2.5)
            return;

        if (Energy < 15 && distance > 230)
            return;

        if (gunLocked && cooldownReady && enoughEnergy && targetFresh)
        {
            Fire(firePower);
            lastFireTurn = TurnNumber;
        }
    }

    private void PredictTargetPosition(double firePower, out double predictedX, out double predictedY)
    {
        double bulletSpeed = 20 - (3 * firePower);

        predictedX = targetX;
        predictedY = targetY;

        if (lastTargetX < 0 || lastTargetY < 0)
            return;

        for (int i = 0; i < 8; i++)
        {
            double distance = Distance(X, Y, predictedX, predictedY);
            double time = distance / bulletSpeed;

            predictedX = targetX + Math.Sin(ToRadians(enemyHeading)) * enemyVelocity * time;
            predictedY = targetY + Math.Cos(ToRadians(enemyHeading)) * enemyVelocity * time;

            predictedX = Clamp(predictedX, WALL_MARGIN, ArenaWidth - WALL_MARGIN);
            predictedY = Clamp(predictedY, WALL_MARGIN, ArenaHeight - WALL_MARGIN);
        }
    }

    // Fungsi seleksi greedy: memilih kandidat posisi dengan skor tertinggi.
    private void ChooseGreedyMove()
    {
        if (IsNearWall(115))
        {
            WallEscapeMove();
            return;
        }

        double bestScore = double.NegativeInfinity;
        double bestAngle = Direction;
        double bestDistance = 100;

        double enemyBearing = AbsoluteBearingTo(X, Y, targetX, targetY);

        double[] angleOffsets =
        {
            -145, -120, -95, -75, -55,
              55,   75,  95, 120, 145,
             170, -170
        };

        double[] moveDistances = { 90, 120, 150, 180 };

        foreach (double offset in angleOffsets)
        {
            foreach (double moveDistance in moveDistances)
            {
                double candidateAngle = NormalizeAbsolute(enemyBearing + offset);

                double nextX = X + Math.Sin(ToRadians(candidateAngle)) * moveDistance;
                double nextY = Y + Math.Cos(ToRadians(candidateAngle)) * moveDistance;

                double score = ScorePosition(nextX, nextY, candidateAngle);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestAngle = candidateAngle;
                    bestDistance = moveDistance;
                }
            }
        }

        MoveInAbsoluteDirection(bestAngle, bestDistance);
    }

    // Fungsi objektif greedy: memaksimalkan keamanan posisi dan peluang tembak.
    private double ScorePosition(double nextX, double nextY, double moveAngle)
    {
        if (!IsInsideSafeArea(nextX, nextY))
            return -999999;

        double score = 0;
        double nextDistance = Distance(nextX, nextY, targetX, targetY);

        score -= Math.Abs(nextDistance - IDEAL_DISTANCE) * 1.5;

        if (nextDistance < MIN_DISTANCE)
            score -= (MIN_DISTANCE - nextDistance) * 7.0;

        if (nextDistance > MAX_DISTANCE)
            score -= (nextDistance - MAX_DISTANCE) * 2.0;

        double enemyBearing = AbsoluteBearingTo(X, Y, targetX, targetY);
        double lateralAngle = Math.Abs(NormalizeBearing(moveAngle - enemyBearing));
        double lateralScore = Math.Abs(Math.Sin(ToRadians(lateralAngle)));

        score += lateralScore * 180;

        if (TurnNumber - lastEnemyFireTurn <= 10)
            score += lateralScore * 160;

        double wallDistance = DistanceToNearestWall(nextX, nextY);

        if (wallDistance < 120)
            score -= (120 - wallDistance) * 13.0;
        else
            score += Math.Min(wallDistance, 220) * 0.7;

        if (IsCornerDanger(nextX, nextY))
            score -= 250;

        double centerX = ArenaWidth / 2.0;
        double centerY = ArenaHeight / 2.0;
        double centerDistance = Distance(nextX, nextY, centerX, centerY);

        score -= centerDistance * 0.04;

        if (Energy < 20 && nextDistance < 230)
            score -= 220;

        double preferredAngle = NormalizeAbsolute(enemyBearing + 90 * moveDirection);
        double orbitDifference = Math.Abs(NormalizeBearing(moveAngle - preferredAngle));

        score -= orbitDifference * 0.25;

        return score;
    }

    private void WallEscapeMove()
    {
        wallEscapeUntil = Math.Max(wallEscapeUntil, TurnNumber + 8);

        double centerX = ArenaWidth / 2.0;
        double centerY = ArenaHeight / 2.0;
        double angleToCenter = AbsoluteBearingTo(X, Y, centerX, centerY);

        MoveInAbsoluteDirection(angleToCenter, 185);

        if (targetX >= 0 && targetY >= 0)
            LockRadar();
        else
            SetTurnRadarRight(360 * radarDirection);
    }

    private void MoveInAbsoluteDirection(double absoluteAngle, double distance)
    {
        double turnLeft = NormalizeBearing(Direction - absoluteAngle);

        if (Math.Abs(turnLeft) > 90)
        {
            turnLeft = NormalizeBearing(turnLeft + 180);
            SetTurnLeft(turnLeft);
            SetBack(distance);
        }
        else
        {
            SetTurnLeft(turnLeft);
            SetForward(distance);
        }
    }

    private void DetectEnemyFire(double newEnemyEnergy)
    {
        double energyDrop = targetEnergy - newEnemyEnergy;

        if (energyDrop > 0.1 && energyDrop <= 3.0)
        {
            lastEnemyFireTurn = TurnNumber;
            moveDirection *= -1;
        }
    }

    private void UpdateEnemyMovement()
    {
        if (lastTargetX < 0 || lastTargetY < 0)
            return;

        double movedDistance = Distance(lastTargetX, lastTargetY, targetX, targetY);

        enemyVelocity = Math.Min(8, movedDistance);
        enemyHeading = AbsoluteBearingTo(lastTargetX, lastTargetY, targetX, targetY);
    }

    public override void OnHitByBullet(HitByBulletEvent e)
    {
        moveDirection *= -1;
        lastEnemyFireTurn = TurnNumber;

        if (IsNearWall(120))
        {
            wallEscapeUntil = TurnNumber + 16;
            WallEscapeMove();
            return;
        }

        double bulletBearing = CalcBearing(e.Bullet.Direction);

        SetTurnLeft(90 - bulletBearing);
        SetForward(150 * moveDirection);
    }

    public override void OnHitBot(HitBotEvent e)
    {
        moveDirection *= -1;
        wallEscapeUntil = TurnNumber + 8;

        SetBack(150);
        SetTurnRight(70 * moveDirection);

        if (Energy > 25)
            Fire(1.5);

        Rescan();
    }

    public override void OnHitWall(HitWallEvent e)
    {
        moveDirection *= -1;
        wallEscapeUntil = TurnNumber + 20;

        WallEscapeMove();
    }

    private double GetFirePower(double distance)
    {
        if (Energy < 10)
            return 0.6;

        if (targetEnergy < 6 && distance < 260)
            return 1.2;

        if (distance < 130)
            return Energy > 35 ? 2.5 : 1.6;

        if (distance < 240)
            return Energy > 28 ? 2.0 : 1.2;

        if (distance < 380)
            return Energy > 35 ? 1.4 : 0.9;

        return 0.7;
    }

    private double GetAimLimit(double distance)
    {
        if (distance < 130)
            return 8.5;

        if (distance < 240)
            return 5.5;

        if (distance < 380)
            return 3.8;

        return 2.4;
    }

    private int GetFireCooldown(double distance)
    {
        if (distance < 150)
            return 5;

        if (distance < 280)
            return 7;

        if (distance < 420)
            return 10;

        return 14;
    }

    private bool IsNearWall(double margin)
    {
        return X < margin ||
               X > ArenaWidth - margin ||
               Y < margin ||
               Y > ArenaHeight - margin;
    }

    private bool IsInsideSafeArea(double x, double y)
    {
        return x > WALL_MARGIN &&
               x < ArenaWidth - WALL_MARGIN &&
               y > WALL_MARGIN &&
               y < ArenaHeight - WALL_MARGIN;
    }

    private bool IsCornerDanger(double x, double y)
    {
        bool nearLeft = x < WALL_MARGIN + 45;
        bool nearRight = x > ArenaWidth - WALL_MARGIN - 45;
        bool nearBottom = y < WALL_MARGIN + 45;
        bool nearTop = y > ArenaHeight - WALL_MARGIN - 45;

        return (nearLeft || nearRight) && (nearBottom || nearTop);
    }

    private double DistanceToNearestWall(double x, double y)
    {
        double left = x;
        double right = ArenaWidth - x;
        double bottom = y;
        double top = ArenaHeight - y;

        return Math.Min(Math.Min(left, right), Math.Min(bottom, top));
    }

    private double AbsoluteBearingTo(double fromX, double fromY, double toX, double toY)
    {
        double dx = toX - fromX;
        double dy = toY - fromY;

        return NormalizeAbsolute(ToDegrees(Math.Atan2(dx, dy)));
    }

    private double Distance(double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;

        return Math.Sqrt(dx * dx + dy * dy);
    }

    private double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }

    private double NormalizeBearing(double angle)
    {
        while (angle > 180)
            angle -= 360;

        while (angle < -180)
            angle += 360;

        return angle;
    }

    private double NormalizeAbsolute(double angle)
    {
        while (angle >= 360)
            angle -= 360;

        while (angle < 0)
            angle += 360;

        return angle;
    }

    private double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    private double ToDegrees(double radians)
    {
        return radians * 180.0 / Math.PI;
    }
}