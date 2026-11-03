using System;
using System.Collections.Generic;
using BrickBreaker.Balls;
using UnityEngine;

namespace BrickBreaker.Launch.Testing
{
    // Play 모드 전용. 실제 Block, BoxCollider2D, 공 효과를 쓰고, 시간만 직접 전진시킨다.
    public static class BallFlightChecks
    {
        [Serializable] private sealed class Report
        {
            public int passed, failed;
            public List<Case> cases = new List<Case>();
        }

        [Serializable] private sealed class Case
        {
            public string name, message;
            public bool passed;
        }

        public static string Run()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("Play 모드에서 실행하세요.");

            var report = new Report();
            Check(report, "좌우 벽 반사에는 가속 없음", f =>
            {
                BallFlight left = f.Ball(new Vector2(.4f, 3), Vector2.left);
                BallFlight right = f.Ball(new Vector2(5.6f, 3), Vector2.right);
                left.Advance(.1f);
                right.Advance(.1f);
                Expect(left.Direction.x > .99f && right.Direction.x < -.99f, "좌우 방향 반전");
                Near(left.transform.position.x, .8f);
                Near(right.transform.position.x, 5.2f);
                Near(left.Speed, 10f);
            });
            Check(report, "천장에서는 입사각과 같은 각도로 반사", f =>
            {
                BallFlight ball = f.Ball(new Vector2(3, 7.6f), new Vector2(3, 4));
                ball.Advance(.1f);
                Near(ball.Direction.x, .6f);
                Near(ball.Direction.y, -.8f);
                Near(ball.Speed, 10f);
            });
            Check(report, "벽과 천장을 동시에 맞는 모서리는 두 축 반전", f =>
            {
                Vector2 direction = new Vector2(2, 1).normalized;
                BallFlight ball = f.Ball(new Vector2(3.9f, 6.9f), direction);
                ball.Advance(.25f);
                Near(ball.Direction.x, -direction.x);
                Near(ball.Direction.y, -direction.y);
            });
            Check(report, "바닥 첫 접점 X를 한 번만 통지", f =>
            {
                int returned = 0;
                float landingX = -1;
                BallFlight ball = f.Ball(new Vector2(2, .6f), new Vector2(3, -4),
                    onReturn: (sender, x) => { returned++; landingX = x; });
                ball.Advance(.2f);
                ball.Advance(.2f);
                Expect(!ball.IsFlying && returned == 1, "회수 후 이동과 재통지 중단");
                Near(landingX, 2.375f);
                Near(ball.transform.position.y, .1f);
            });
            Check(report, "살아남은 블록 반사와 분리 중 중복 적중 방지", f =>
            {
                Block block = f.BlockAt(4, 3, 100);
                BallFlight ball = f.Ball(new Vector2(2.5f, 3), Vector2.up);
                ball.Advance(.10f);
                ball.Advance(.015f);
                Expect(block.CurrentHealth == 90 && ball.Direction.y < -.99f, "직접 피해 한 번과 반사");
                Near(ball.Speed, 10.3f);
            });
            Check(report, "천장을 맞고 되돌아오면 같은 블록을 다시 공격", f =>
            {
                Block block = f.BlockAt(4, 3, 100);
                BallFlight ball = f.Ball(new Vector2(2.5f, 6), Vector2.down);
                ball.Advance(.70f);
                Expect(block.CurrentHealth == 80 && ball.Direction.y > .99f, "정상 재충돌의 피해 반영");
                Near(ball.Speed, 10.6f);
            });
            Check(report, "한 스텝에서 파괴 블록을 통과한 뒤 다음 블록에 도달", f =>
            {
                Block first = f.BlockAt(4, 3, 3);
                Block second = f.BlockAt(3, 3, 100);
                BallFlight ball = f.Ball(new Vector2(2.5f, 3), Vector2.up);
                ball.Advance(.23f);
                Expect(first.IsDestroyed && second.CurrentHealth == 90 && ball.Direction.y < -.99f,
                    "이동 끝점만 검사해 블록을 건너뛰지 않음");
                Near(ball.Speed, 10.6f);
            });
            Check(report, "직접 피해를 버틴 블록도 즉시 레이저로 파괴되면 관통", f =>
            {
                Block block = f.BlockAt(4, 3, 12);
                BallFlight ball = f.Ball(new Vector2(2.5f, 3), Vector2.up, BallEffectType.HorizontalLaser);
                ball.Advance(.15f);
                Expect(block.IsDestroyed && ball.Direction.y > .99f, "즉시 효과가 끝난 생존 상태 사용");
                Near(ball.Speed, 10.3f);
            });
            Check(report, "피해 알림 수신자가 방금 파괴한 블록도 관통", f =>
            {
                Block block = f.BlockAt(4, 3, 100);
                f.Effects.DamageBatchCompleted += batch => block.TakeDamage(100);
                BallFlight ball = f.Ball(new Vector2(2.5f, 3), Vector2.up);
                ball.Advance(.15f);
                Expect(block.IsDestroyed && ball.Direction.y > .99f, "반사 직전 실제 생존 여부 확인");
            });
            Check(report, "각 공의 가속은 24가 최대이며 새 발사에서는 초기화", f =>
            {
                // 빈 한 칸의 위·아래에 블록을 놓아 반복 적중을 실제로 발생시킨다.
                Block top = f.BlockAt(3, 3, 10000);
                Block bottom = f.BlockAt(5, 3, 10000);
                Vector2 middle = new Vector2(2.5f, 4.5f);
                BallFlight ball = f.Ball(middle, Vector2.up);
                for (int i = 0; i < 300; i++) ball.Advance(.02f);
                Near(ball.Speed, 24f);
                Near(f.Ball(middle, Vector2.right).Speed, 10f);

                top.TakeDamage(10000);
                bottom.TakeDamage(10000);
                Block far = f.BlockAt(ball.Direction.y > 0f ? 2 : 7, 3, 1);
                Physics2D.SyncTransforms();
                ball.Advance(.25f); // 6칸을 이동해도 중간의 .92칸 블록을 놓치면 안 된다.
                Expect(far.IsDestroyed, "최고 속도에서도 경로 중간의 블록을 맞춤");
                f.NextShot();
                f.Launch(ball, middle, Vector2.up, BallEffectType.Normal, null);
                Near(ball.Speed, 10f);
            });
            Check(report, "다른 보드의 블록과 일반 콜라이더는 무시", f =>
            {
                Block outside = f.BlockAt(5, 3, 100, outsideBoard: true);
                var obstacle = new GameObject("unrelated collider", typeof(BoxCollider2D));
                obstacle.transform.SetParent(f.Root.transform, false);
                obstacle.transform.position = new Vector3(2.5f, 2.8f, 0f);
                Block own = f.BlockAt(4, 3, 100);
                BallFlight ball = f.Ball(new Vector2(2.5f, 2), Vector2.up);
                ball.Advance(.23f);
                Expect(outside.CurrentHealth == 100 && own.CurrentHealth == 90, "연결한 보드만 직접 적중");
                Near(ball.Speed, 10.3f);
            });
            Check(report, "조준 중 실제 콜라이더까지 그리고 1회 반사하되 피해 없음", f =>
            {
                Block block = f.BlockAt(4, 3, 100);
                f.Effects.EndShot(f.Effects.CurrentShotId);
                int damageEvents = 0;
                f.Effects.DamageBatchCompleted += batch => damageEvents++;
                var points = new Vector3[3];
                int count = BallFlightPath.GetPreviewPoints(f.Effects, f.Bounds, new Vector2(2.5f, 3), Vector2.up, points);
                Expect(count == 3 && damageEvents == 0 && block.CurrentHealth == 100, "조준선은 피해 없이 접점 2개 생성");
                Near(points[0].y, 3f);
                Near(points[1].y, 3.94f, .025f);
                Near(points[2].y, .1f);
            });
            Check(report, "이미 종료되거나 교체된 발사의 공은 이동과 피해 중단", f =>
            {
                Block block = f.BlockAt(4, 3, 100);
                int returns = 0;
                BallFlight ball = f.Ball(new Vector2(2.5f, 3), Vector2.up, onReturn: (sender, x) => returns++);
                f.NextShot();
                ball.Advance(.2f);
                Expect(!ball.IsFlying && returns == 0 && block.CurrentHealth == 100, "이전 공의 늦은 입력 무시");
            });
            return JsonUtility.ToJson(report);
        }

        private static void Check(Report report, string name, Action<Fixture> test)
        {
            var result = new Case { name = name };
            var root = new GameObject("Temporary Flight Check");
            try
            {
                test(new Fixture(root));
                result.passed = true;
                report.passed++;
            }
            catch (Exception error)
            {
                result.message = error.Message;
                report.failed++;
            }
            finally
            {
                root.SetActive(false);
                UnityEngine.Object.Destroy(root);
                report.cases.Add(result);
            }
        }

        private static void Expect(bool ok, string description)
        {
            if (!ok) throw new InvalidOperationException(description);
        }

        private static void Near(float actual, float expected, float tolerance = .005f)
        {
            Expect(Mathf.Abs(actual - expected) <= tolerance, $"기대 {expected}, 실제 {actual}");
        }

        private sealed class Fixture
        {
            public readonly GameObject Root;
            public readonly Rect Bounds = new Rect(0, 0, 6, 8);
            public readonly BallEffectController Effects;
            private readonly GridManager grid;
            private int ballId;

            public Fixture(GameObject root)
            {
                Root = root;
                var board = new GameObject("Board", typeof(GridManager));
                board.transform.SetParent(root.transform, false);
                grid = board.GetComponent<GridManager>();
                Effects = root.AddComponent<BallEffectController>();
                Effects.Configure(grid, board.transform);
                NextShot();
            }

            public Block BlockAt(int row, int col, int hp, bool outsideBoard = false)
            {
                var obj = new GameObject("Block", typeof(BoxCollider2D), typeof(Block));
                obj.transform.SetParent(outsideBoard ? Root.transform : grid.transform, false);
                obj.transform.position = grid.GetCellPosition(row, col);
                obj.GetComponent<BoxCollider2D>().size = new Vector2(.92f, .92f);
                Block block = obj.GetComponent<Block>();
                block.Initialize(hp);
                return block;
            }

            public void NextShot() => Effects.BeginShot(Effects.CurrentShotId + 1);

            public BallFlight Ball(Vector2 from, Vector2 direction, BallEffectType type = BallEffectType.Normal,
                Action<BallFlight, float> onReturn = null)
            {
                var obj = new GameObject("Ball", typeof(BallFlight));
                obj.transform.SetParent(Root.transform, false);
                var ball = obj.GetComponent<BallFlight>();
                Launch(ball, from, direction, type, onReturn);
                return ball;
            }

            public void Launch(BallFlight ball, Vector2 from, Vector2 direction, BallEffectType type,
                Action<BallFlight, float> onReturn)
            {
                int id = ++ballId;
                Expect(Effects.RegisterBall(Effects.CurrentShotId, id, type, 10), "테스트 공 등록");
                ball.Launch(Effects, Effects.CurrentShotId, id, Bounds, from, direction, onReturn);
            }
        }
    }
}
