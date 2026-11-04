using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BrickBreaker.Balls.Testing
{
    // Play 중 임시 보드와 메모리 안의 설정 자산만 사용한다. 팀의 자산에는 쓰지 않는다.
    public static class BallGaugeBalanceChecks
    {
        public static string LastReport { get; private set; } = "NOT RUN";
        private static GameObject runner;

        [MenuItem("Tools/Brick Breaker/Test Additional Gauge Balance (Play)")]
        private static void RunFromMenu() => Run();

        public static string Run()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("Play 모드에서 실행하세요.");
            if (runner != null)
                return LastReport;

            runner = new GameObject("Additional Gauge Balance Tests");
            LastReport = "STARTED";
            runner.AddComponent<BallEffectController>().StartCoroutine(RunCases());
            return LastReport;
        }

        private static IEnumerator RunCases()
        {
            var failures = new List<string>();
            int passed = 0;
            void Check(string name, Action test)
            {
                try { test(); passed++; }
                catch (Exception ex) { failures.Add(name + ": " + ex.Message); }
            }

            float previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            try
            {
                Check("100% fallback/default", CheckDefaults);
                Check("direct and Charge overkill", CheckDirect);
                // 공격력 20, 중앙과 오른쪽·위쪽 이웃에 각각 체력 200인 블록.
                Check("Explosion 10%", () => CheckArea(BallEffectType.Explosion, 40, 22));
                Check("Electric 20%", () => CheckArea(BallEffectType.Electric, 60, 28));
                Check("Horizontal 30%", () => CheckArea(BallEffectType.HorizontalLaser, 40, 26));
                Check("Vertical 40%", () => CheckArea(BallEffectType.VerticalLaser, 40, 28));
                Check("Cross 50%", () => CheckArea(BallEffectType.CrossLaser, 60, 40));
                Check("fractional actual damage across balls, reset on next shot", CheckRemainder);

                using (var f = new Fixture())
                {
                    var fireCharges = new List<long>();
                    // 첫 화염은 25%, 이후 Inspector의 같은 필드를 50%, 15%로 바꾼다.
                    f.Effects.DamageBatchCompleted += batch =>
                    {
                        if (batch.Records[0].IsDirect) return;
                        fireCharges.Add(batch.GaugeCharge);
                        if (fireCharges.Count == 1) f.SetPercent("firePercent", 50);
                        if (fireCharges.Count == 2) f.SetPercent("firePercent", 15);
                    };
                    Check("fire scheduling leaves future charge out", () =>
                    {
                        f.Begin(BallEffectType.Fire, 10);
                        HitResult hit = f.Hit(f.Block(4, 3, 100));
                        Equal(hit.ImmediateGaugeCharge, 10);
                        Equal(fireCharges.Count, 0);
                        f.SetPercent("firePercent", 25); // 예약 당시의 100%와 다르다.
                    });

                    double deadline = Time.realtimeSinceStartupAsDouble + 5;
                    while (fireCharges.Count < 3 && Time.realtimeSinceStartupAsDouble < deadline)
                        yield return null;
                    Check("fire uses live rate and carries fractions across ticks", () =>
                    {
                        // 2.50 + 5.00 + 1.50: 이미 보고한 값은 고치지 않고 2, 5, 2를 전달한다.
                        if (!fireCharges.SequenceEqual(new long[] { 2, 5, 2 }))
                            throw new Exception("Expected 2,5,2; got " + string.Join(",", fireCharges));
                    });
                }
                LastReport = (failures.Count == 0 ? "PASS" : "FAIL") +
                    $": {passed} passed, {failures.Count} failed. " + string.Join("; ", failures);
                Debug.Log(LastReport);
            }
            finally
            {
                if (LastReport == "STARTED") LastReport = "FAIL: test run interrupted";
                Time.timeScale = previousTimeScale;
                UnityEngine.Object.Destroy(runner);
                runner = null;
            }
        }

        private static void CheckDefaults()
        {
            using (var f = new Fixture())
            {
                foreach (BallEffectType type in Enum.GetValues(typeof(BallEffectType)))
                    Equal(f.Balance.GetAdditionalPercent(type), 100);
                f.Begin(BallEffectType.Explosion);
                Block target = f.Block(4, 3, 100);
                f.Block(4, 4, 100);
                f.Effects.ConfigureBalance(null);
                Equal(f.Hit(target).ImmediateGaugeCharge, 30);
                f.SetPercent("explosionPercent", 0);
                f.Effects.ConfigureBalance(f.Balance); // 진행 중인 발사에서 설정 자산 교체
                Equal(f.Hit(target).ImmediateGaugeCharge, 20);
            }
        }

        private static void CheckDirect()
        {
            using (var f = new Fixture())
            {
                f.Begin(BallEffectType.Normal);
                Equal(f.Hit(f.Block(4, 3, 7)).ImmediateGaugeCharge, 7);
                f.Begin(BallEffectType.Charge);
                Equal(f.Hit(f.Block(4, 4, 7)).ImmediateGaugeCharge, 14);
            }
        }

        private static void CheckArea(BallEffectType type, long expectedDamage, long expectedCharge)
        {
            using (var f = new Fixture())
            {
                f.SetPercent("explosionPercent", 10);
                f.SetPercent("electricPercent", 20);
                f.SetPercent("horizontalLaserPercent", 30);
                f.SetPercent("verticalLaserPercent", 40);
                f.SetPercent("crossLaserPercent", 50);
                f.SetPercent("firePercent", 60);
                f.Begin(type);
                f.Block(4, 4, 200);
                f.Block(3, 3, 200);
                DamageBatch reported = null;
                f.Effects.DamageBatchCompleted += batch => reported = batch;
                HitResult hit = f.Hit(f.Block(4, 3, 200));
                Equal(hit.ImmediateActualDamage, expectedDamage);
                Equal(hit.ImmediateGaugeCharge, expectedCharge);
                Equal(reported.GaugeCharge, expectedCharge);
                Equal(reported.Records.Sum(record => record.GaugeCharge), expectedCharge);
                Equal(reported.Records[0].GaugeCharge, 20);
            }
        }

        private static void CheckRemainder()
        {
            using (var f = new Fixture())
            {
                f.SetPercent("explosionPercent", 50);
                f.Begin(BallEffectType.Explosion);
                f.Effects.RegisterBall(f.Effects.CurrentShotId, 2, BallEffectType.Explosion, 20);
                Block center = f.Block(4, 3, 200);
                for (int i = 0; i < 3; i++)
                {
                    f.Block(4, 4, 1); // 주변 공격력은 10이지만 실제 감소량은 1뿐이다.
                    long charge = f.Hit(center, i == 1 ? 2 : 1).ImmediateGaugeCharge;
                    Equal(charge, i == 1 ? 21 : 20); // 첫 두 번의 0.5가 합쳐져 1이 된다.
                }
                f.Begin(BallEffectType.Explosion);
                f.Block(4, 4, 1);
                Equal(f.Hit(center).ImmediateGaugeCharge, 20); // 이전 발사의 마지막 0.5는 만료됨
            }
        }

        private static void Equal(long actual, long expected)
        {
            if (actual != expected) throw new Exception($"Expected {expected}, got {actual}");
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("Temporary Balance Board");
            private readonly GridManager grid;
            public readonly BallGaugeBalance Balance = ScriptableObject.CreateInstance<BallGaugeBalance>();
            public readonly BallEffectController Effects;

            public Fixture()
            {
                grid = root.AddComponent<GridManager>();
                Effects = root.AddComponent<BallEffectController>();
                Effects.Configure(grid, root.transform, 12345);
                Effects.ConfigureBalance(Balance);
            }

            public void SetPercent(string field, int value)
            {
                var serialized = new SerializedObject(Balance);
                serialized.FindProperty(field).intValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            public void Begin(BallEffectType type, int attack = 20)
            {
                Effects.BeginShot(Effects.CurrentShotId + 1);
                if (!Effects.RegisterBall(Effects.CurrentShotId, 1, type, attack))
                    throw new Exception("Could not register test ball");
            }

            public Block Block(int row, int column, int health)
            {
                var obj = new GameObject("Test Block");
                obj.transform.SetParent(root.transform, false);
                obj.transform.position = grid.GetCellPosition(row, column);
                Block block = obj.AddComponent<Block>();
                block.Initialize(health);
                return block;
            }

            public HitResult Hit(Block block, int ballId = 1) =>
                Effects.ApplyHit(Effects.CurrentShotId, ballId, block);

            public void Dispose()
            {
                root.SetActive(false);
                UnityEngine.Object.Destroy(root);
                UnityEngine.Object.Destroy(Balance);
            }
        }
    }
}
