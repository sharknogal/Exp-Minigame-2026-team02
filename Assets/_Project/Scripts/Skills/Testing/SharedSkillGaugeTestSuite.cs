using System;
using System.Collections;
using System.Collections.Generic;
using BrickBreaker.Balls;
using UnityEngine;

namespace BrickBreaker.Skills.Testing
{
    [Serializable]
    public sealed class SharedSkillGaugeTestReport
    {
        public bool completed;
        public int passed;
        public int failed;
        public List<SharedSkillGaugeTestCaseResult> cases = new List<SharedSkillGaugeTestCaseResult>();
    }

    [Serializable]
    public sealed class SharedSkillGaugeTestCaseResult
    {
        public string name;
        public bool passed;
        public string message;
    }

    // 테스트 전용이다. 게이지 규칙은 숫자로, 공 연결은 실제 Block과 효과 코드로 검증한다.
    // 기존 씬이나 팀원의 private 필드를 변경하지 않고 매 사례마다 임시 오브젝트를 만든다.
    public static class SharedSkillGaugeTestSuite
    {
        public static IEnumerator Run(SharedSkillGaugeTestReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));
            if (!Application.isPlaying)
                throw new InvalidOperationException("게이지 통합 테스트는 Play 모드에서 실행하세요.");

            report.completed = false;
            report.passed = 0;
            report.failed = 0;
            report.cases.Clear();
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            try
            {
                RunModelCases(report);
                RunImmediateIntegrationCases(report);
                yield return RunTimedCase(report, "화염은 실제 피해 시점에 세 번 더 충전", CheckFireTicks);
                yield return RunTimedCase(report, "발사 종료 시 남은 화염은 충전하지 않음", CheckFireCancellation);
                yield return RunTimedCase(report, "화염이 남은 체력만큼만 충전하고 파괴 후 중단", CheckFireOverkill);
                report.completed = true;
            }
            finally
            {
                Time.timeScale = previousTimeScale;
            }
        }

        private static void RunModelCases(SharedSkillGaugeTestReport report)
        {
            CheckCase(report, "새 게이지의 초기 상태", () =>
            {
                var gauge = new SharedSkillGauge();
                Expect(gauge.Charge == 0 && gauge.ActivationCount == 0, "충전량과 발동 횟수는 0");
                Expect(!gauge.HasAbilities && !gauge.IsShotActive, "능력 미보유, 발사 전 상태");
            });

            CheckCase(report, "599에서는 대기하고 정확히 600에서 발동", () =>
            {
                SharedSkillGauge gauge = StartGauge(true);
                gauge.AddBallCharge(599);
                Expect(gauge.Charge == 599 && gauge.ActivationCount == 0, "599는 발동 전");
                gauge.AddBallCharge(1);
                Expect(gauge.Charge == 0 && gauge.ActivationCount == 1, "600을 한 번 소모");
            });

            CheckCase(report, "능력 미보유 시 600까지만 저장", () =>
            {
                SharedSkillGauge gauge = StartGauge(false);
                gauge.AddBallCharge(1250);
                Expect(gauge.Charge == 600 && gauge.ActivationCount == 0, "초과분 버림, 발동 없음");
                gauge.AddBallCharge(100);
                Expect(gauge.Charge == 600, "이미 가득 찬 게이지 유지");
            });

            CheckCase(report, "미보유 때 버린 초과 충전량은 나중에 복원되지 않음", () =>
            {
                SharedSkillGauge gauge = StartGauge(false);
                gauge.AddBallCharge(1250);
                gauge.EndShot();
                gauge.SetHasAbilities(true);
                gauge.BeginShot();
                Expect(gauge.Charge == 0 && gauge.ActivationCount == 1, "저장된 600만 발동");
            });

            CheckCase(report, "큰 충전량도 미보유 상태에서는 600으로 제한", () =>
            {
                SharedSkillGauge gauge = StartGauge(false);
                gauge.AddBallCharge(long.MaxValue);
                Expect(gauge.Charge == 600, "덧셈 오버플로 없이 최대치 제한");
            });

            CheckCase(report, "1250 충전은 두 차례 발동하고 50 유지", () =>
            {
                SharedSkillGauge gauge = StartGauge(true);
                int requests = 0;
                gauge.AbilityActivationRequested += () => requests++;
                gauge.AddBallCharge(1250);
                Expect(gauge.Charge == 50 && gauge.ActivationCount == 2 && requests == 2,
                    "600마다 한 번 요청하고 잔량 유지");
            });

            CheckCase(report, "기존 590에 1250을 더하면 세 차례 발동하고 40 유지", () =>
            {
                SharedSkillGauge gauge = StartGauge(true);
                gauge.AddBallCharge(590);
                gauge.AddBallCharge(1250);
                Expect(gauge.Charge == 40 && gauge.ActivationCount == 3, "기존 충전량도 합산");
            });

            CheckCase(report, "첫 능력 구매 중에는 발동하지 않고 다음 발사에 발동", () =>
            {
                SharedSkillGauge gauge = StartGauge(false);
                gauge.AddBallCharge(600);
                gauge.EndShot();
                gauge.SetHasAbilities(true);
                Expect(gauge.Charge == 600 && gauge.ActivationCount == 0, "구매 시 발동 대기");
                gauge.BeginShot();
                Expect(gauge.Charge == 0 && gauge.ActivationCount == 1, "발사 시작 시 먼저 발동");
            });

            CheckCase(report, "발사 종료와 다음 발사 사이에 충전량 유지", () =>
            {
                SharedSkillGauge gauge = StartGauge(true);
                gauge.AddBallCharge(450);
                gauge.EndShot();
                Expect(gauge.Charge == 450 && gauge.HasAbilities, "턴 사이에 상태 유지");
                gauge.BeginShot();
                gauge.AddBallCharge(150);
                Expect(gauge.ActivationCount == 1 && gauge.Charge == 0, "다음 턴의 피해와 합산");
            });

            CheckCase(report, "새 게임은 충전량과 능력 보유 상태를 초기화", () =>
            {
                SharedSkillGauge gauge = StartGauge(true);
                gauge.AddBallCharge(750);
                gauge.ResetGame();
                Expect(gauge.Charge == 0 && gauge.ActivationCount == 0, "수치 초기화");
                Expect(!gauge.HasAbilities && !gauge.IsShotActive, "진행 상태 초기화");
            });

            CheckCase(report, "0과 음수 충전은 무시", () =>
            {
                SharedSkillGauge gauge = StartGauge(true);
                gauge.AddBallCharge(50);
                gauge.AddBallCharge(0);
                gauge.AddBallCharge(-10);
                gauge.AddBallCharge(long.MinValue);
                Expect(gauge.Charge == 50 && gauge.ActivationCount == 0, "잘못된 입력으로 차감하지 않음");
            });

            CheckCase(report, "발사 전과 발사 종료 후 피해 요청은 충전하지 않음", () =>
            {
                var gauge = new SharedSkillGauge();
                gauge.AddBallCharge(100);
                gauge.BeginShot();
                gauge.AddBallCharge(50);
                gauge.EndShot();
                gauge.AddBallCharge(100);
                Expect(gauge.Charge == 50, "진행 중 피해만 반영");
            });

            CheckCase(report, "발사 중 능력 보유 변경은 무시", () =>
            {
                SharedSkillGauge empty = StartGauge(false);
                empty.SetHasAbilities(true);
                Expect(!empty.HasAbilities, "발사 중 새 능력을 획득하지 않음");
                SharedSkillGauge owned = StartGauge(true);
                owned.SetHasAbilities(false);
                Expect(owned.HasAbilities, "발사 중 보유 능력을 제거하지 않음");
            });

            CheckCase(report, "중복 시작과 종료 호출은 충전량을 초기화하지 않음", () =>
            {
                SharedSkillGauge gauge = StartGauge(true);
                gauge.AddBallCharge(200);
                gauge.BeginShot();
                Expect(gauge.Charge == 200 && gauge.IsShotActive, "중복 시작 무시");
                gauge.EndShot();
                gauge.EndShot();
                Expect(gauge.Charge == 200 && !gauge.IsShotActive, "중복 종료에도 값 유지");
            });

            CheckCase(report, "UI 비율은 0, 절반, 최대치를 표현", () =>
            {
                SharedSkillGauge gauge = StartGauge(false);
                Expect(Mathf.Approximately(gauge.NormalizedCharge, 0f), "시작은 0%");
                gauge.AddBallCharge(300);
                Expect(Mathf.Approximately(gauge.NormalizedCharge, 0.5f), "300은 50%");
                gauge.AddBallCharge(1000);
                Expect(Mathf.Approximately(gauge.NormalizedCharge, 1f), "가득 차면 100%");
            });

            CheckCase(report, "각 발동 콜백은 600 소모가 끝난 값을 확인", () =>
            {
                SharedSkillGauge gauge = StartGauge(true);
                var charges = new List<long>();
                var counts = new List<long>();
                gauge.AbilityActivationRequested += () =>
                {
                    charges.Add(gauge.Charge);
                    counts.Add(gauge.ActivationCount);
                };
                gauge.AddBallCharge(1250);
                Expect(charges.Count == 2 && charges[0] == 650 && charges[1] == 50,
                    "한 번씩 소모한 뒤 순서대로 통지");
                Expect(counts[0] == 1 && counts[1] == 2, "각 콜백에서 누적 횟수 확인");
            });

            CheckCase(report, "발동 콜백에서 새 게임을 시작하면 이전 발동을 중단", () =>
            {
                SharedSkillGauge gauge = StartGauge(true);
                int requests = 0;
                gauge.AbilityActivationRequested += () =>
                {
                    requests++;
                    gauge.ResetGame();
                };
                gauge.AddBallCharge(1250);
                Expect(requests == 1 && gauge.Charge == 0 && gauge.ActivationCount == 0,
                    "초기화 후 예전 충전량으로 다시 발동하지 않음");
            });

            CheckCase(report, "발동 콜백에서 발사를 끝내면 남은 발동은 다음 발사로 넘김", () =>
            {
                SharedSkillGauge gauge = StartGauge(true);
                Action stop = () => gauge.EndShot();
                gauge.AbilityActivationRequested += stop;
                gauge.AddBallCharge(1250);
                Expect(gauge.ActivationCount == 1 && gauge.Charge == 650 && !gauge.IsShotActive,
                    "종료 후 추가 발동 중단, 남은 충전량 보존");
                gauge.AbilityActivationRequested -= stop;
                gauge.BeginShot();
                Expect(gauge.ActivationCount == 2 && gauge.Charge == 50, "다음 발사 시작에 잔량 처리");
            });

            CheckCase(report, "변경 통지 후 UI가 최종 잔량을 읽을 수 있음", () =>
            {
                SharedSkillGauge gauge = StartGauge(true);
                long displayedCharge = -1;
                gauge.Changed += () => displayedCharge = gauge.Charge;
                gauge.AddBallCharge(1250);
                Expect(displayedCharge == 50, "마지막 변경 통지는 최종 잔량을 반영");
                gauge.ResetGame();
                Expect(displayedCharge == 0, "새 게임 초기화도 UI에 전달");
            });

            CheckCase(report, "발동 콜백의 재귀 충전 요청은 다음 능력을 연쇄 발동하지 않음", () =>
            {
                SharedSkillGauge gauge = StartGauge(true);
                int requests = 0;
                gauge.AbilityActivationRequested += () =>
                {
                    requests++;
                    if (requests == 1)
                        gauge.AddBallCharge(600);
                };
                gauge.AddBallCharge(600);
                Expect(requests == 1 && gauge.Charge == 0, "능력 발동 중 재진입한 충전은 무시");
            });
        }

        private static void RunImmediateIntegrationCases(SharedSkillGaugeTestReport report)
        {
            CheckCase(report, "일반공의 실제 피해를 한 번만 충전", () =>
            {
                using (var fixture = new Fixture())
                {
                    Block target = fixture.AddBlock(4, 3, 100);
                    fixture.Begin(BallEffectType.Normal);
                    HitResult result = fixture.Hit(target);
                    Expect(result.IsValid && target.CurrentHealth == 90, "공 효과가 블록에 피해 적용");
                    Expect(fixture.Gauge.Charge == 10, "즉시 반환값과 이벤트를 중복 합산하지 않음");
                }
            });

            CheckCase(report, "충전공의 이미 계산된 2배를 다시 곱하지 않음", () =>
            {
                using (var fixture = new Fixture())
                {
                    fixture.Begin(BallEffectType.Charge);
                    fixture.Hit(fixture.AddBlock(4, 3, 100));
                    Expect(fixture.Gauge.Charge == 20, "10 피해는 20 충전");
                }
            });

            CheckCase(report, "남은 체력 3의 블록은 일반공으로 3만 충전", () =>
            {
                using (var fixture = new Fixture())
                {
                    fixture.Begin(BallEffectType.Normal);
                    fixture.Hit(fixture.AddBlock(4, 3, 3));
                    Expect(fixture.Gauge.Charge == 3, "초과 피해 제외");
                }
            });

            CheckCase(report, "남은 체력 3의 블록은 충전공으로 6만 충전", () =>
            {
                using (var fixture = new Fixture())
                {
                    fixture.Begin(BallEffectType.Charge);
                    fixture.Hit(fixture.AddBlock(4, 3, 3));
                    Expect(fixture.Gauge.Charge == 6, "실제 피해에만 2배 적용");
                }
            });

            CheckCase(report, "폭발 피해는 주변 블록의 실제 체력 감소도 합산", () =>
            {
                using (var fixture = new Fixture())
                {
                    fixture.Begin(BallEffectType.Explosion);
                    fixture.AddBlock(4, 4, 3);
                    fixture.AddBlock(5, 3, 100);
                    fixture.Hit(fixture.AddBlock(4, 3, 100));
                    Expect(fixture.Gauge.Charge == 18, "직접 10 + 주변 3 + 주변 5");
                }
            });

            CheckCase(report, "감전 피해도 실제 체력 감소만 충전", () =>
            {
                using (var fixture = new Fixture())
                {
                    fixture.Begin(BallEffectType.Electric);
                    fixture.AddBlock(5, 4, 3);
                    fixture.Hit(fixture.AddBlock(4, 3, 100));
                    Expect(fixture.Gauge.Charge == 13, "직접 10 + 감전 3");
                }
            });

            CheckCase(report, "가로 레이저 추가 피해도 충전", () =>
            {
                using (var fixture = new Fixture())
                {
                    fixture.Begin(BallEffectType.HorizontalLaser);
                    fixture.AddBlock(4, 4, 3);
                    fixture.Hit(fixture.AddBlock(4, 3, 100));
                    Expect(fixture.Gauge.Charge == 18, "직접 10 + 중심 추가 5 + 옆 블록 3");
                }
            });

            CheckCase(report, "세로 레이저 추가 피해도 충전", () =>
            {
                using (var fixture = new Fixture())
                {
                    fixture.Begin(BallEffectType.VerticalLaser);
                    fixture.AddBlock(5, 3, 7);
                    fixture.Hit(fixture.AddBlock(4, 3, 100));
                    Expect(fixture.Gauge.Charge == 20, "직접 10 + 중심 추가 5 + 아래 블록 5");
                }
            });

            CheckCase(report, "십자 레이저의 중심 추가 피해 두 번을 모두 충전", () =>
            {
                using (var fixture = new Fixture())
                {
                    fixture.Begin(BallEffectType.CrossLaser);
                    fixture.AddBlock(4, 4, 3);
                    fixture.AddBlock(5, 3, 7);
                    fixture.Hit(fixture.AddBlock(4, 3, 100));
                    Expect(fixture.Gauge.Charge == 28, "직접 10 + 중심 10 + 주변 3 + 주변 5");
                }
            });

            CheckCase(report, "유효하지 않은 공 적중은 충전하지 않음", () =>
            {
                using (var fixture = new Fixture())
                {
                    fixture.Begin(BallEffectType.Normal);
                    Expect(!fixture.Hit(null).IsValid && fixture.Gauge.Charge == 0, "빈 대상 무시");
                }
            });

            CheckCase(report, "같은 공의 정상적인 재충돌은 각각 충전", () =>
            {
                using (var fixture = new Fixture())
                {
                    Block target = fixture.AddBlock(4, 3, 100);
                    fixture.Begin(BallEffectType.Normal);
                    fixture.Hit(target);
                    fixture.Hit(target);
                    Expect(fixture.Gauge.Charge == 20, "같은 발사 번호와 공 번호여도 정상 피해 두 번 반영");
                }
            });

            CheckCase(report, "Configure를 여러 번 호출해도 구독은 한 번", () =>
            {
                using (var fixture = new Fixture())
                {
                    fixture.Controller.Configure(fixture.Effects);
                    fixture.Controller.Configure(fixture.Effects);
                    fixture.Begin(BallEffectType.Normal);
                    fixture.Hit(fixture.AddBlock(4, 3, 100));
                    Expect(fixture.Gauge.Charge == 10, "중복 구독으로 20 이상 충전되지 않음");
                }
            });

            CheckCase(report, "게이지 비활성화 중에는 충전하지 않고 재활성화 시 중복 구독 없음", () =>
            {
                using (var fixture = new Fixture())
                {
                    Block target = fixture.AddBlock(4, 3, 100);
                    fixture.Begin(BallEffectType.Normal);
                    fixture.Hit(target);
                    fixture.Controller.enabled = false;
                    fixture.Hit(target);
                    Expect(fixture.Gauge.Charge == 10 && !fixture.Gauge.IsShotActive, "비활성화 시 구독 해제와 구간 종료");
                    fixture.Controller.enabled = true;
                    fixture.Controller.BeginShot();
                    fixture.Hit(target);
                    Expect(fixture.Gauge.Charge == 20, "재활성화 후 한 번만 반영");
                }
            });

            CheckCase(report, "연결할 공 효과를 바꾸면 이전 효과의 구독을 해제", () =>
            {
                using (var first = new Fixture())
                using (var second = new Fixture())
                {
                    first.Begin(BallEffectType.Normal);
                    second.Begin(BallEffectType.Normal);
                    first.Controller.Configure(second.Effects);
                    first.Controller.BeginShot();
                    first.Hit(first.AddBlock(4, 3, 100));
                    Expect(first.Gauge.Charge == 0, "이전 효과 이벤트는 수신하지 않음");
                    second.Hit(second.AddBlock(4, 3, 100));
                    Expect(first.Gauge.Charge == 10, "새 효과 이벤트만 수신");
                }
            });

            CheckCase(report, "공 효과를 거치지 않은 피해는 게이지에 들어오지 않음", () =>
            {
                using (var fixture = new Fixture())
                {
                    fixture.Begin(BallEffectType.Normal);
                    Block target = fixture.AddBlock(4, 3, 100);
                    target.TakeDamage(25);
                    Expect(fixture.Gauge.Charge == 0 && target.CurrentHealth == 75,
                        "특수 능력과 부활은 공 적중 진입점을 호출하지 않아야 함");
                }
            });

            CheckCase(report, "컨트롤러의 새 게임은 기존 게이지를 초기화", () =>
            {
                using (var fixture = new Fixture())
                {
                    fixture.Begin(BallEffectType.Normal);
                    fixture.Hit(fixture.AddBlock(4, 3, 100));
                    fixture.Controller.BeginGame();
                    Expect(fixture.Gauge.Charge == 0 && !fixture.Gauge.IsShotActive, "컴포넌트 연결에서도 초기화");
                }
            });
        }

        private static IEnumerator CheckFireTicks(Fixture fixture)
        {
            fixture.Begin(BallEffectType.Fire);
            var times = new List<double>();
            double start = Time.timeAsDouble;
            fixture.Effects.DamageBatchCompleted += batch => times.Add(Time.timeAsDouble - start);
            fixture.Hit(fixture.AddBlock(4, 3, 100));
            Expect(fixture.Gauge.Charge == 10 && times.Count == 1, "직접 적중 시 미래 피해를 미리 충전하지 않음");
            yield return new WaitForSeconds(0.65f);
            Expect(times.Count == 4 && fixture.Gauge.Charge == 40, "직접 피해와 세 번의 화염 피해 반영");
            Expect(times[1] >= 0.149 && times[2] >= 0.299 && times[3] >= 0.449,
                "각 예약 시각 이전에는 피해가 발생하지 않음");
        }

        private static IEnumerator CheckFireCancellation(Fixture fixture)
        {
            fixture.Begin(BallEffectType.Fire);
            fixture.Hit(fixture.AddBlock(4, 3, 100));
            fixture.End();
            yield return new WaitForSeconds(0.65f);
            Expect(fixture.Gauge.Charge == 10 && fixture.Effects.PendingFireDamageCount == 0,
                "발사 종료 후 직접 피해의 충전량만 유지");
        }

        private static IEnumerator CheckFireOverkill(Fixture fixture)
        {
            fixture.Begin(BallEffectType.Fire);
            fixture.Hit(fixture.AddBlock(4, 3, 15));
            yield return new WaitForSeconds(0.65f);
            Expect(fixture.Gauge.Charge == 15 && fixture.Effects.PendingFireDamageCount == 0,
                "직접 10 + 화염 실제 피해 5만 충전");
        }

        // 코루틴 예외도 사례 결과에 남겨 다음 사례를 계속 실행한다.
        private static IEnumerator RunTimedCase(SharedSkillGaugeTestReport report, string name,
            Func<Fixture, IEnumerator> test)
        {
            Fixture fixture = null;
            IEnumerator routine = null;
            Exception failure = null;
            try
            {
                try
                {
                    fixture = new Fixture();
                    routine = test(fixture);
                }
                catch (Exception exception)
                {
                    failure = exception;
                }

                while (failure == null && routine != null)
                {
                    bool hasNext = false;
                    object instruction = null;
                    try
                    {
                        hasNext = routine.MoveNext();
                        if (hasNext)
                            instruction = routine.Current;
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                    }
                    if (!hasNext || failure != null)
                        break;
                    yield return instruction;
                }
            }
            finally
            {
                (routine as IDisposable)?.Dispose();
                fixture?.Dispose();
            }
            Record(report, name, failure);
        }

        private static SharedSkillGauge StartGauge(bool hasAbilities)
        {
            var gauge = new SharedSkillGauge();
            gauge.SetHasAbilities(hasAbilities);
            gauge.BeginShot();
            return gauge;
        }

        private static void CheckCase(SharedSkillGaugeTestReport report, string name, Action test)
        {
            Exception failure = null;
            try { test(); }
            catch (Exception exception) { failure = exception; }
            Record(report, name, failure);
        }

        private static void Record(SharedSkillGaugeTestReport report, string name, Exception failure)
        {
            report.cases.Add(new SharedSkillGaugeTestCaseResult
            {
                name = name,
                passed = failure == null,
                message = failure == null ? "통과" : failure.Message
            });
            if (failure == null)
                report.passed++;
            else
                report.failed++;
        }

        private static void Expect(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            private readonly GridManager grid;
            public BallEffectController Effects { get; }
            public SharedSkillGaugeController Controller { get; }
            public SharedSkillGauge Gauge => Controller.Gauge;

            public Fixture()
            {
                root = new GameObject("Temporary Gauge Test Fixture");
                grid = root.AddComponent<GridManager>();
                Effects = root.AddComponent<BallEffectController>();
                Effects.Configure(grid, root.transform, 12345);
                Controller = root.AddComponent<SharedSkillGaugeController>();
                Controller.Configure(Effects);
                Controller.BeginGame();
            }

            public Block AddBlock(int row, int column, int health)
            {
                var blockObject = new GameObject($"Test Block {row},{column}");
                blockObject.transform.SetParent(root.transform, false);
                Vector2 position = grid.GetCellPosition(row, column);
                blockObject.transform.position = new Vector3(position.x, position.y, 0f);
                Block block = blockObject.AddComponent<Block>();
                block.Initialize(health);
                return block;
            }

            public void Begin(BallEffectType type)
            {
                // 실제 발사 담당도 게이지의 발동 처리를 끝낸 뒤 공을 등록하고 발사한다.
                Controller.BeginShot();
                Effects.BeginShot(Effects.CurrentShotId + 1);
                Expect(Effects.RegisterBall(Effects.CurrentShotId, 1, type, 10), "테스트 공 등록 성공");
            }

            public HitResult Hit(Block target)
            {
                return Effects.ApplyHit(Effects.CurrentShotId, 1, target);
            }

            public void End()
            {
                Effects.EndShot(Effects.CurrentShotId);
                Controller.EndShot();
            }

            public void Dispose()
            {
                if (root == null)
                    return;
                End();
                root.SetActive(false);
                UnityEngine.Object.Destroy(root);
            }
        }
    }
}
