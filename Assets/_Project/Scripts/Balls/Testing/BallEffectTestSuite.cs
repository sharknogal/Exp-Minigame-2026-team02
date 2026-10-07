using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BrickBreaker.Balls.Testing
{
    [Serializable]
    public sealed class BallEffectTestReport
    {
        public bool completed;
        public int passed;
        public int failed;
        public List<BallEffectTestCaseResult> cases = new List<BallEffectTestCaseResult>();
    }

    [Serializable]
    public sealed class BallEffectTestCaseResult
    {
        public string name;
        public bool passed;
        public List<string> failures = new List<string>();
    }

    // 실제 Block과 GridManager를 사용한다. 효과 코드와 기대값을 공유하지 않는다.
    internal sealed class BallEffectTestSuite
    {
        private readonly BallEffectTestHarness harness;
        private readonly BallEffectTestReport report;
        private BallEffectTestCaseResult current;
        private string electricSelection;

        private BallEffectController Effects => harness.Effects;
        private GridManager Grid => harness.Grid;
        private BallBoardLookup Board => harness.Board;
        private int ShotId => harness.ShotId;

        public BallEffectTestSuite(BallEffectTestHarness harness, BallEffectTestReport report)
        {
            this.harness = harness;
            this.report = report;
        }

        public IEnumerator Run()
        {
            yield return Immediate("Grid lookup and bounds", CheckGridLookup);
            yield return Immediate("Board lookup rejects a block outside the connected parent", () =>
            {
                Block target = Board.GetBlock(4, 3);
                Transform originalParent = target.transform.parent;
                try
                {
                    target.transform.SetParent(null, true);
                    Register(BallEffectType.Normal);
                    Check(Board.GetBlock(4, 3) == null, "좌표가 같아도 다른 부모의 블록은 제외");
                    Check(!Effects.ApplyHit(ShotId, 1, target).IsValid, "범위 밖 부모의 블록에 피해 없음");
                    Check(target.CurrentHealth == 100, "외부 블록의 체력 유지");
                }
                finally
                {
                    target.transform.SetParent(originalParent, true);
                }
            });
            yield return Immediate("Board lookup excludes inactive blocks", () =>
            {
                Block target = Board.GetBlock(4, 3);
                target.gameObject.SetActive(false);
                Register(BallEffectType.Normal);
                Check(Board.GetBlock(4, 3) == null && !Effects.ApplyHit(ShotId, 1, target).IsValid,
                    "비활성 블록은 조회와 적중에서 제외");
                target.gameObject.SetActive(true);
                Check(Board.GetBlock(4, 3) == target, "활성화하면 재등록 없이 조회 가능");
            });
            yield return Immediate("Board lookup rejects positions between cells", () =>
            {
                Block target = Board.GetBlock(4, 3);
                target.transform.position += Vector3.right * 0.25f;
                Register(BallEffectType.Explosion);
                Check(Board.GetBlock(4, 3) == null && !Effects.ApplyHit(ShotId, 1, target).IsValid,
                    "격자에서 벗어난 블록은 적중 거부");
                ExpectTotals(0, 0, 0);
                Check(target.CurrentHealth == 100, "이동 중인 블록에 피해 없음");
            });
            yield return Immediate("Board lookup follows current positions after rearrangement", () =>
            {
                Block first = Board.GetBlock(4, 3);
                Block second = Board.GetBlock(5, 3);
                Vector3 firstPosition = first.transform.position;
                first.transform.position = second.transform.position;
                second.transform.position = firstPosition;
                Check(Board.GetBlock(5, 3) == first && Board.GetBlock(4, 3) == second,
                    "재배치 후 현재 위치를 조회");
                Register(BallEffectType.HorizontalLaser);
                Effects.ApplyHit(ShotId, 1, first);
                Check(first.CurrentHealth == 85 && second.CurrentHealth == 100, "새 행에만 레이저 적용");
                Check(harness.Batches[0].Records[0].Row == 5, "피해 기록도 새 행을 사용");
                ExpectTotals(40, 40, 1);
            });
            yield return Immediate("Board lookup rejects ambiguous overlapping blocks", () =>
            {
                Block first = Board.GetBlock(4, 3);
                Block second = Board.GetBlock(5, 3);
                second.transform.position = first.transform.position;
                Register(BallEffectType.Normal);
                Check(Board.GetBlock(4, 3) == null, "같은 칸의 중복 블록을 임의로 선택하지 않음");
                Check(!Effects.ApplyHit(ShotId, 1, first).IsValid &&
                    !Effects.ApplyHit(ShotId, 1, second).IsValid, "모호한 적중은 모두 거부");
                Check(first.CurrentHealth == 100 && second.CurrentHealth == 100, "두 블록의 체력 유지");
                ExpectTotals(0, 0, 0);
            });
            yield return Immediate("Normal damage and untouched neighbours", () =>
            {
                Register(BallEffectType.Normal);
                HitResult result = Hit();
                Check(result.IsValid && !result.DirectTargetDestroyed, "유효한 생존 대상 적중");
                Check(result.ImmediateActualDamage == 10 && result.ImmediateGaugeCharge == 10, "직접 피해와 충전 10");
                ExpectBoard((r, c) => r == 4 && c == 3 ? 90 : 100);
                ExpectTotals(10, 10, 1);
            });
            yield return Immediate("Explosion excludes centre from extra damage", () =>
            {
                Register(BallEffectType.Explosion);
                Hit();
                ExpectBoard((r, c) => r == 4 && c == 3 ? 90 :
                    Math.Abs(r - 4) <= 1 && Math.Abs(c - 3) <= 1 ? 95 : 100);
                ExpectTotals(50, 50, 1);
            });
            yield return Immediate("Electric unique targets within range", CheckElectric);
            yield return Immediate("Electric seed is reproducible", () =>
            {
                Register(BallEffectType.Electric);
                Hit();
                Check(SelectionKey() == electricSelection, "동일 시드로 같은 대상 순서");
            });
            yield return Immediate("Electric uses all two remaining candidates", () =>
            {
                KeepOnly((r, c) => (r == 4 && c == 3) || (r == 2 && c == 1) || (r == 6 && c == 5));
                Register(BallEffectType.Electric);
                Hit();
                Check(Health(2, 1) == 90 && Health(6, 5) == 90, "후보 두 개 모두 공격");
                ExpectTotals(30, 30, 1);
            });
            yield return Immediate("Horizontal laser includes centre", () => CheckLaser(BallEffectType.HorizontalLaser));
            yield return Immediate("Vertical laser includes centre", () => CheckLaser(BallEffectType.VerticalLaser));
            yield return Immediate("Cross laser damages centre twice additionally", () => CheckLaser(BallEffectType.CrossLaser));

            foreach (BallEffectType type in new[] { BallEffectType.HorizontalLaser, BallEffectType.VerticalLaser, BallEffectType.CrossLaser })
            {
                BallEffectType selected = type;
                yield return Immediate(selected + " second hit has no laser", () =>
                {
                    Register(selected);
                    Hit();
                    HitResult second = Hit();
                    Check(second.ImmediateActualDamage == 10, "두 번째 적중은 직접 피해 10");
                    Check(harness.TotalCombo == 2, "적중당 콤보 하나");
                });
            }

            yield return Immediate("Laser state is separate for each ball", () =>
            {
                Register(BallEffectType.HorizontalLaser, 1);
                Register(BallEffectType.HorizontalLaser, 2);
                Hit(1);
                Check(Hit(2).ImmediateActualDamage == 40, "다른 공도 첫 효과 발동");
            });
            yield return Immediate("Duplicate begin and registration cannot rearm laser", () =>
            {
                Register(BallEffectType.HorizontalLaser);
                Hit();
                Effects.BeginShot(ShotId);
                Check(!Effects.RegisterBall(ShotId, 1, BallEffectType.HorizontalLaser, 10), "같은 공 중복 등록 거부");
                Check(Hit().ImmediateActualDamage == 10, "중복 시작 후에도 첫 적중 상태 유지");
            });
            yield return Immediate("New shot resets laser and rejects stale commands", () =>
            {
                Register(BallEffectType.HorizontalLaser);
                Hit();
                int oldShot = ShotId;
                Effects.BeginShot(oldShot + 1);
                Register(BallEffectType.HorizontalLaser);
                Effects.BeginShot(oldShot);
                Effects.EndShot(oldShot);
                Check(!Effects.ApplyHit(oldShot, 1, Board.GetBlock(4, 3)).IsValid, "이전 구간 적중 거부");
                Check(Hit().ImmediateActualDamage == 40, "새 구간 레이저 초기화");
            });
            yield return Immediate("Invalid hits do not consume first laser hit", CheckInvalidHits);
            yield return Immediate("Registration validates type and attack power", CheckRegistration);
            yield return Immediate("Charge uses actual damage on overkill", () =>
            {
                Board.GetBlock(4, 3).Initialize(3);
                Register(BallEffectType.Charge);
                HitResult result = Hit();
                Check(result.DirectTargetDestroyed, "대상 파괴");
                ExpectTotals(3, 6, 1);
                Check(harness.TotalDestroyed == 1, "파괴 보고 한 번");
            });
            yield return Immediate("Destroyed gold block has a usable reward snapshot", CheckRewardSnapshot);
            yield return Immediate("Explosion survives direct centre destruction", () =>
            {
                Board.GetBlock(4, 3).Initialize(1);
                Register(BallEffectType.Explosion);
                Check(Hit().DirectTargetDestroyed, "중심 파괴");
                Check(Health(3, 2) == 95 && Health(5, 4) == 95, "중심 파괴 후 주변 공격");
                ExpectTotals(41, 41, 1);
            });
            yield return Immediate("Electric survives direct centre destruction", () =>
            {
                Board.GetBlock(4, 3).Initialize(1);
                Register(BallEffectType.Electric);
                Hit();
                ExpectTotals(31, 31, 1);
                Check(harness.Batches[0].Records.Count == 4, "중심과 다른 대상 3개");
            });
            yield return Immediate("Laser survives direct centre destruction", () =>
            {
                Board.GetBlock(4, 3).Initialize(1);
                Register(BallEffectType.HorizontalLaser);
                Hit();
                ExpectTotals(26, 26, 1);
            });
            yield return Immediate("Result includes destruction by immediate laser", () =>
            {
                Board.GetBlock(4, 3).Initialize(12);
                Register(BallEffectType.HorizontalLaser);
                Check(Hit().DirectTargetDestroyed, "직접 피해 후 생존했어도 추가 효과 파괴를 반환");
                ExpectTotals(37, 37, 1);
            });
            yield return Immediate("Explosion clips field edges and empty cells", () =>
            {
                Register(BallEffectType.Explosion);
                Effects.ApplyHit(ShotId, 1, Board.GetBlock(2, 1));
                ExpectBoard((r, c) => r == 2 && c == 1 ? 90 : r <= 3 && c <= 2 ? 95 : 100);
                ExpectTotals(25, 25, 1);
            });
            yield return Immediate("Attack upgrade applies to direct and area damage", () =>
            {
                Register(BallEffectType.Explosion, 1, 12);
                Hit();
                Check(Health(4, 3) == 88 && Health(3, 2) == 94, "강화 직접 피해 12와 주변 피해 6");
                ExpectTotals(60, 60, 1);
            });
            yield return Immediate("Additional damage does not chain or duplicate deaths", () =>
            {
                for (int r = 2; r <= 7; r++)
                    for (int c = 1; c <= 6; c++)
                        Board.GetBlock(r, c).Initialize(1);
                Register(BallEffectType.CrossLaser);
                Hit();
                Check(harness.TotalDestroyed == 11, "십자의 서로 다른 블록 11개만 파괴");
                ExpectTotals(11, 11, 1);
                Check(harness.Batches.Count == 1, "추가 적중 이벤트 없음");
            });

            yield return Timed("Fire timing and delayed reports", FireTiming);
            yield return Timed("Fire stacks six scheduled ticks on repeated hit", FireStacking);
            yield return Timed("Fire stops on target death and ignores replacement", FireTargetDeath);
            yield return Timed("EndShot cancels fire even inside a damage callback", FireEndShot);
            yield return Timed("New shot cancels old fire", FireNewShot);
            yield return Timed("Returning one ball does not cancel shared fire", FireIndependentOfBall);
            yield return Timed("Pause freezes fire game time", FirePause);
            yield return Timed("Disabling controller cancels fire", FireDisable);
            yield return Immediate("Fire does not schedule for directly destroyed target", () =>
            {
                Board.GetBlock(4, 3).Initialize(1);
                Register(BallEffectType.Fire);
                Hit();
                Check(Effects.PendingFireDamageCount == 0, "이미 파괴된 대상 예약 없음");
                ExpectTotals(1, 1, 1);
            });
            report.completed = true;
        }

        private void CheckGridLookup()
        {
            Check(Board.GetBlock(0, 1) == null && Board.GetBlock(9, 1) == null &&
                Board.GetBlock(2, 0) == null && Board.GetBlock(2, 7) == null, "범위 밖 조회는 null");
            Check(Board.GetBlock(1, 1) == null && Board.GetBlock(8, 1) == null, "빈 행 조회는 null");
            Block target = Board.GetBlock(4, 3);
            Check(Board.TryGetCell(target, out int row, out int column) && row == 4 && column == 3, "블록 좌표 조회");
            target.TakeDamage(100);
            Check(Board.GetBlock(4, 3) == null && !Board.TryGetCell(target, out _, out _), "지연 삭제 전에도 파괴 대상 제외");
        }

        private void CheckElectric()
        {
            Register(BallEffectType.Electric);
            Hit();
            var selected = new HashSet<Vector2Int>();
            foreach (DamageRecord record in harness.Batches[0].Records)
            {
                if (record.IsDirect)
                    continue;
                Check(record.Row >= 2 && record.Row <= 6 && record.Column >= 1 && record.Column <= 5, "5x5 범위");
                Check(record.Row != 4 || record.Column != 3, "중심 제외");
                Check(selected.Add(new Vector2Int(record.Row, record.Column)), "대상 중복 없음");
            }
            Check(selected.Count == 3, "추가 대상 3개");
            ExpectBoard((r, c) => r == 4 && c == 3 || selected.Contains(new Vector2Int(r, c)) ? 90 : 100);
            ExpectTotals(40, 40, 1);
            electricSelection = SelectionKey();
        }

        private string SelectionKey()
        {
            var cells = new List<string>();
            foreach (DamageRecord record in harness.Batches[0].Records)
                cells.Add(record.Row + ":" + record.Column);
            return string.Join(",", cells);
        }

        private void CheckLaser(BallEffectType type)
        {
            Register(type);
            Hit();
            bool horizontal = type != BallEffectType.VerticalLaser;
            bool vertical = type != BallEffectType.HorizontalLaser;
            ExpectBoard((r, c) => r == 4 && c == 3 ? type == BallEffectType.CrossLaser ? 80 : 85 :
                (horizontal && r == 4) || (vertical && c == 3) ? 95 : 100);
            long expected = type == BallEffectType.CrossLaser ? 70 : 40;
            ExpectTotals(expected, expected, 1);
        }

        private void CheckInvalidHits()
        {
            Register(BallEffectType.HorizontalLaser);
            Block destroyed = Board.GetBlock(2, 1);
            destroyed.TakeDamage(100);
            var unrelated = new GameObject("Unregistered Test Block");
            Block outside = unrelated.AddComponent<Block>();
            outside.transform.position = Grid.GetCellPosition(4, 3);
            try
            {
                Check(!Effects.ApplyHit(ShotId, 1, null).IsValid, "null 거부");
                Check(!Effects.ApplyHit(ShotId, 1, destroyed).IsValid, "파괴 대상 거부");
                Check(!Effects.ApplyHit(ShotId, 1, outside).IsValid, "격자 밖 대상 거부");
                Check(!Effects.ApplyHit(ShotId, 99, Board.GetBlock(4, 3)).IsValid, "미등록 공 거부");
                Check(!Effects.ApplyHit(ShotId - 1, 1, Board.GetBlock(4, 3)).IsValid, "잘못된 구간 거부");
                ExpectTotals(0, 0, 0);
                Check(Hit().ImmediateActualDamage == 40, "무효 입력이 레이저를 소비하지 않음");
                int ended = ShotId;
                Effects.EndShot(ended);
                Effects.BeginShot(ended);
                Check(!Hit().IsValid, "종료 구간의 번호를 재사용할 수 없음");
            }
            finally
            {
                UnityEngine.Object.Destroy(unrelated);
            }
        }

        private void CheckRegistration()
        {
            Check(!Effects.RegisterBall(ShotId, 1, BallEffectType.Normal, 9), "10 미만 거부");
            Check(!Effects.RegisterBall(ShotId, 1, BallEffectType.Normal, 11), "홀수 공격력 거부");
            Check(!Effects.RegisterBall(ShotId, 1, (BallEffectType)99, 10), "없는 종류 거부");
            Check(!Effects.RegisterBall(ShotId, -1, BallEffectType.Normal, 10), "음수 공 번호 거부");
            Register(BallEffectType.Normal);
        }

        private void CheckRewardSnapshot()
        {
            Board.GetBlock(4, 3).Initialize(5, true, true);
            Register(BallEffectType.Charge);
            Hit();
            Hit();
            DamageRecord death = harness.Batches[1].Records[0];
            Check(death.DestroyedNow && death.MaxHealth == 15 && death.IsGold, "파괴 전 최대 체력과 골드 속성 보존");
            ExpectTotals(15, 30, 2);
            Check(harness.TotalDestroyed == 1, "파괴 보고 한 번");
        }

        private IEnumerator FireTiming()
        {
            Register(BallEffectType.Fire);
            double start = Time.timeAsDouble;
            HitResult immediate = Hit();
            Check(immediate.ImmediateActualDamage == 10 && Effects.PendingFireDamageCount == 3, "반환값에 미래 피해 미합산");
            yield return new WaitForSeconds(0.55f);
            Check(Health(4, 3) == 60 && harness.Batches.Count == 4, "직접 1회와 지연 3회");
            for (int i = 1; i < harness.BatchTimes.Count; i++)
            {
                Check(harness.BatchTimes[i] >= start + 0.15 * i - 0.005, "예약 시각보다 일찍 실행하지 않음");
                Check(harness.Batches[i].ComboDelta == 0 && !harness.Batches[i].Records[0].IsDirect, "지연 피해는 추가 피해");
            }
            ExpectTotals(40, 40, 1);
            Check(Effects.PendingFireDamageCount == 0, "예약 모두 소진");
        }

        private IEnumerator FireStacking()
        {
            Register(BallEffectType.Fire);
            Hit();
            Hit();
            Check(Effects.PendingFireDamageCount == 6, "두 적중의 예약이 누적됨");
            yield return new WaitForSeconds(0.55f);
            Check(Health(4, 3) == 20, "직접 2회와 예약 6회 피해");
            ExpectTotals(80, 80, 2);
        }

        private IEnumerator FireTargetDeath()
        {
            Board.GetBlock(4, 3).Initialize(15);
            Register(BallEffectType.Fire);
            Hit();
            yield return new WaitForSeconds(0.2f);
            Check(Board.GetBlock(4, 3) == null, "첫 예약으로 대상 파괴");
            yield return null;
            Grid.CreateBlock(4, 3, 100);
            yield return new WaitForSeconds(0.4f);
            Check(Health(4, 3) == 100, "같은 칸의 새 블록은 이전 예약을 받지 않음");
            ExpectTotals(15, 15, 1);
            Check(harness.TotalDestroyed == 1, "화염 파괴 한 번");
        }

        private IEnumerator FireEndShot()
        {
            Register(BallEffectType.Fire);
            Hit();
            Action<DamageBatch> stopAfterFirstTick = batch =>
            {
                if (batch.ComboDelta == 0)
                    Effects.EndShot(batch.ShotId);
            };
            Effects.DamageBatchCompleted += stopAfterFirstTick;
            try
            {
                yield return new WaitForSeconds(0.55f);
                Check(Health(4, 3) == 80 && Effects.PendingFireDamageCount == 0, "첫 예약 이후 나머지 취소");
                ExpectTotals(20, 20, 1);
            }
            finally
            {
                Effects.DamageBatchCompleted -= stopAfterFirstTick;
            }
        }

        private IEnumerator FireNewShot()
        {
            Register(BallEffectType.Fire);
            Hit();
            int oldShot = ShotId;
            Effects.BeginShot(oldShot + 1);
            Register(BallEffectType.Normal);
            Effects.EndShot(oldShot);
            yield return new WaitForSeconds(0.55f);
            Check(Health(4, 3) == 90 && Effects.IsShotActive, "이전 화염 취소와 현재 구간 유지");
            Check(Hit().IsValid, "새 구간 적중 가능");
        }

        private IEnumerator FireIndependentOfBall()
        {
            var returnedBall = new GameObject("Returned Ball Placeholder");
            Register(BallEffectType.Fire);
            Hit();
            returnedBall.SetActive(false);
            UnityEngine.Object.Destroy(returnedBall);
            yield return new WaitForSeconds(0.55f);
            Check(Health(4, 3) == 60, "공 오브젝트 수명과 화염 예약 분리");
        }

        private IEnumerator FirePause()
        {
            Register(BallEffectType.Fire);
            Hit();
            Time.timeScale = 0f;
            try
            {
                yield return new WaitForSecondsRealtime(0.3f);
                Check(Health(4, 3) == 90 && Effects.PendingFireDamageCount == 3, "일시정지 중 지연 피해 없음");
            }
            finally
            {
                Time.timeScale = 1f;
            }
            yield return new WaitForSeconds(0.55f);
            Check(Health(4, 3) == 60, "재개 후 예약 실행");
        }

        private IEnumerator FireDisable()
        {
            Register(BallEffectType.Fire);
            Hit();
            Effects.enabled = false;
            yield return new WaitForSeconds(0.55f);
            Effects.enabled = true;
            Check(Health(4, 3) == 90 && !Effects.IsShotActive && Effects.PendingFireDamageCount == 0, "비활성화 시 예약과 구간 정리");
        }

        private IEnumerator PrepareCase(string name)
        {
            current = new BallEffectTestCaseResult { name = name };
            yield return harness.ResetFixture();
            Effects.BeginShot(Effects.CurrentShotId + 1);
        }

        private IEnumerator Immediate(string name, Action body)
        {
            yield return PrepareCase(name);
            try { body(); }
            catch (Exception exception) { Check(false, exception.ToString()); }
            FinishCase();
        }

        private IEnumerator Timed(string name, Func<IEnumerator> body)
        {
            yield return PrepareCase(name);
            IEnumerator routine = body();
            // 시간 대기는 Unity에 맡기고, 각 단계의 예외는 테스트 실패로 기록한다.
            while (true)
            {
                bool hasNext;
                object wait;
                try
                {
                    hasNext = routine.MoveNext();
                    wait = hasNext ? routine.Current : null;
                }
                catch (Exception exception)
                {
                    Check(false, exception.ToString());
                    break;
                }
                if (!hasNext)
                    break;
                yield return wait;
            }
            (routine as IDisposable)?.Dispose();
            FinishCase();
        }

        private void FinishCase()
        {
            current.passed = current.failures.Count == 0;
            report.cases.Add(current);
            if (current.passed)
            {
                report.passed++;
                Debug.Log("[BallEffects PASS] " + current.name);
            }
            else
            {
                report.failed++;
                Debug.LogError("[BallEffects FAIL] " + current.name + "\n" + string.Join("\n", current.failures));
            }
        }

        private void Register(BallEffectType type, int ballId = 1, int attackPower = 10)
        {
            Check(Effects.RegisterBall(ShotId, ballId, type, attackPower), "테스트 공 등록");
        }

        private HitResult Hit(int ballId = 1)
        {
            return Effects.ApplyHit(ShotId, ballId, Board.GetBlock(4, 3));
        }

        private int Health(int row, int column)
        {
            Block block = Board.GetBlock(row, column);
            return block != null ? block.CurrentHealth : 0;
        }

        private void Check(bool condition, string message)
        {
            if (!condition)
                current.failures.Add(message);
        }

        private void ExpectTotals(long damage, long gauge, int combo)
        {
            Check(harness.TotalActualDamage == damage, $"피해 기대 {damage}, 실제 {harness.TotalActualDamage}");
            Check(harness.TotalGaugeCharge == gauge, $"충전 기대 {gauge}, 실제 {harness.TotalGaugeCharge}");
            Check(harness.TotalCombo == combo, $"콤보 기대 {combo}, 실제 {harness.TotalCombo}");
        }

        private void ExpectBoard(Func<int, int, int> expectedHealth)
        {
            for (int row = 2; row <= 7; row++)
                for (int column = 1; column <= 6; column++)
                    Check(Health(row, column) == expectedHealth(row, column),
                        $"({row}, {column}) 체력 기대 {expectedHealth(row, column)}, 실제 {Health(row, column)}");
        }

        private void KeepOnly(Func<int, int, bool> keep)
        {
            for (int row = 2; row <= 7; row++)
                for (int column = 1; column <= 6; column++)
                    if (!keep(row, column))
                        Board.GetBlock(row, column).TakeDamage(100);
        }
    }
}
