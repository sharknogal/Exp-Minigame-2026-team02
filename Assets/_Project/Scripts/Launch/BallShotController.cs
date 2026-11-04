using System.Collections.Generic;
using BrickBreaker.Balls;
using BrickBreaker.Skills;
using UnityEngine;

namespace BrickBreaker.Launch
{
    // 한 발사 구간의 주인. 개별 공은 충돌/이동만 맡고 턴을 직접 넘기지 않는다.
    [DisallowMultipleComponent]
    public sealed class BallShotController : MonoBehaviour
    {
        [SerializeField] private TurnManager turns;
        [SerializeField] private AugmentInventory inventory;
        [SerializeField] private AugmentPurchaseManager purchase;
        [SerializeField] private PauseUI pause;
        [SerializeField] private BallEffectController effects;
        [SerializeField] private SharedSkillGaugeController gauge;
        [SerializeField] private BallFlight ballPrefab;
        [SerializeField] private Transform originMarker;

        public const float BallInterval = 0.04f;
        private const float Radius = BallFlight.Radius;
        public static readonly Rect Field = new Rect(0f, 0f, 6f, 8f);

        // 팀의 구입 목록에는 순서를 읽는 함수가 없다. 구매 한 건이 끝날 때마다
        // 공개 보유 수의 증가분을 기록해서, 같은 종류가 섞여도 취득 순서를 보존한다.
        private static readonly (AugmentId augment, BallEffectType effect)[] BallKinds =
        {
            (AugmentId.NormalBall, BallEffectType.Normal),
            (AugmentId.ExplosionBall, BallEffectType.Explosion),
            (AugmentId.ElectricBall, BallEffectType.Electric),
            (AugmentId.HorizontalLaserBall, BallEffectType.HorizontalLaser),
            (AugmentId.VerticalLaserBall, BallEffectType.VerticalLaser),
            (AugmentId.CrossLaserBall, BallEffectType.CrossLaser),
            (AugmentId.FireBall, BallEffectType.Fire),
            (AugmentId.ChargeBall, BallEffectType.Charge)
        };

        private readonly int[] knownCounts = new int[BallKinds.Length];
        private readonly List<BallEffectType> ownedOrder = new List<BallEffectType>();
        private readonly List<BallEffectType> shotOrder = new List<BallEffectType>();
        private readonly List<BallFlight> activeBalls = new List<BallFlight>();
        private Vector2 shotDirection;
        private float untilNextBall;
        private int nextIndex;
        private bool hasReturnX;
        private float firstReturnX;
        private bool quitting;

        public Vector2 LaunchOrigin { get; private set; } = new Vector2(3f, Radius);
        public int ActiveShotId { get; private set; } = -1;
        public int ActiveBallCount => activeBalls.Count;
        public int PendingBallCount => ActiveShotId < 0 ? 0 : shotOrder.Count - nextIndex;
        public bool IsPaused => Time.timeScale <= 0f || (pause != null && pause.IsPaused);
        public bool CanAim => isActiveAndEnabled && Application.isPlaying && !IsPaused &&
            ActiveShotId < 0 && turns != null && turns.isActiveAndEnabled && turns.State == TurnManager.TurnState.Aiming &&
            (purchase == null || !purchase.IsSelecting) && inventory != null && inventory.BallCount > 0 &&
            effects != null && effects.isActiveAndEnabled && effects.Board != null && effects.Board.IsAvailable &&
            !effects.IsShotActive && gauge != null && gauge.isActiveAndEnabled && !gauge.Gauge.IsShotActive && ballPrefab != null;

        private void OnEnable()
        {
            if (purchase != null)
                purchase.SelectionChanged += ObserveSelection;
            SyncInventory();
            UpdateMarker();
        }

        private void OnDisable()
        {
            if (purchase != null)
                purchase.SelectionChanged -= ObserveSelection;

            // 별도로 이 컴포넌트를 끄면 남은 공을 정리한다. 씬을 닫는 중에는 턴을 생성하지 않는다.
            if (ActiveShotId >= 0)
                FinishShot(!quitting && gameObject.scene.isLoaded && turns != null && turns.isActiveAndEnabled);
        }

        private void OnApplicationQuit() { quitting = true; }

        private void UpdateMarker()
        {
            if (originMarker != null)
                originMarker.position = new Vector3(LaunchOrigin.x, LaunchOrigin.y, originMarker.position.z);
        }

        // 새 판에서 TurnManager.BeginGame() 직후 호출한다. 턴을 넘길 때는 호출하지 않는다.
        public void ResetForNewGame()
        {
            if (ActiveShotId >= 0)
                return;
            ownedOrder.Clear();
            System.Array.Clear(knownCounts, 0, knownCounts.Length);
            LaunchOrigin = new Vector2(Field.center.x, Field.yMin + Radius);
            UpdateMarker();
            if (gauge != null) gauge.BeginGame();
            SyncInventory();
        }

        private void ObserveSelection() { SyncInventory(); }

        // 실제 상점은 SelectionChanged를 보낸다. 테스트에서 카드를 직접 줄 때는 한 장마다 호출한다.
        // 여러 종류를 이미 샀는데 관찰 기록이 없다면, 종류별로 재정렬해 잘못 발사하지 않는다.
        public bool SyncInventory()
        {
            if (inventory == null || ActiveShotId >= 0)
                return false;
            if (gauge != null)
            {
                bool hasAbility = inventory.GetOwnedCount(AugmentId.FrontAttack) > 0 ||
                    inventory.GetOwnedCount(AugmentId.RearAttack) > 0 ||
                    inventory.GetOwnedCount(AugmentId.OddColumnAttack) > 0 ||
                    inventory.GetOwnedCount(AugmentId.EvenColumnAttack) > 0;
                gauge.SetHasAbilities(hasAbility);
            }

            if (inventory.BallCount == 0) return false;
            if (ownedOrder.Count == 0)
            {
                if (inventory.GetOwnedCount(AugmentId.NormalBall) == 0) return false;
                ownedOrder.Add(BallEffectType.Normal); // BeginGame이 첫 공을 보장한다.
                knownCounts[0] = 1;
            }

            int addedKind = -1;
            int addedCount = 0;
            for (int i = 0; i < BallKinds.Length; ++i)
            {
                int delta = inventory.GetOwnedCount(BallKinds[i].augment) - knownCounts[i];
                if (delta == 0) continue;
                if (delta < 0 || addedKind >= 0) return false;
                addedKind = i;
                addedCount = delta;
            }
            if (ownedOrder.Count + addedCount != inventory.BallCount) return false;
            if (addedKind >= 0)
            {
                for (int i = 0; i < addedCount; ++i)
                    ownedOrder.Add(BallKinds[addedKind].effect);
                knownCounts[addedKind] += addedCount;
            }
            return true;
        }

        // 양의 y 방향만 조준하며 수평에 가까운 각도는 허용 범위의 끝에 맞춘다.
        public static bool TryGetDirection(Vector2 raw, out Vector2 direction)
        {
            direction = Vector2.zero;
            if (raw.y <= 0f || raw.sqrMagnitude < 0.000001f ||
                float.IsNaN(raw.x) || float.IsNaN(raw.y) || float.IsInfinity(raw.x) || float.IsInfinity(raw.y))
                return false;
            float angle = Mathf.Clamp(Mathf.Atan2(raw.y, raw.x) * Mathf.Rad2Deg, 10f, 170f) * Mathf.Deg2Rad;
            direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            return true;
        }

        public int GetPreviewPoints(Vector2 direction, Vector3[] points)
        {
            if (!CanAim || points == null || points.Length < 3 || !TryGetDirection(direction, out var valid))
                return 0;
            return BallFlightPath.GetPreviewPoints(effects, Field, LaunchOrigin, valid, points);
        }

        public bool TryLaunch(Vector2 direction)
        {
            if (!CanAim || !TryGetDirection(direction, out var validDirection)) return false;
            if (!SyncInventory())
            {
                Debug.LogWarning("공을 획득한 순서를 확인할 수 없습니다. 구매 전에 발사 관리자를 연결하세요.", this);
                return false;
            }
            int attack = inventory.BallAttackPower;
            if (attack < 10 || (attack % 2) != 0 || effects.CurrentShotId == int.MaxValue) return false;

            shotOrder.Clear();
            shotOrder.AddRange(ownedOrder);
            turns.BeginShot(); // 구매 잠금/이자 확정은 팀의 턴 관리자가 담당한다.
            if (turns.State != TurnManager.TurnState.Shooting) return false;
            int id = effects.CurrentShotId + 1;
            ActiveShotId = id;
            nextIndex = 0;
            hasReturnX = false;
            shotDirection = validDirection;
            untilNextBall = BallInterval;
            effects.BeginShot(id);
            gauge.BeginShot(); // 가득 찬 첫 능력은 첫 공보다 먼저 발동한다.
            if (ActiveShotId != id) return false; // 발동 수신자가 회수를 요청한 경우
            if (!gauge.Gauge.IsShotActive || !effects.IsShotActive || effects.CurrentShotId != id ||
                turns.State != TurnManager.TurnState.Shooting)
            {
                FinishShot(true);
                return false;
            }
            for (int i = 0; i < shotOrder.Count; ++i)
            {
                if (!effects.RegisterBall(id, i + 1, shotOrder[i], attack))
                {
                    FinishShot(true);
                    return false;
                }
            }
            LaunchNextBall();
            return true;
        }

        private void FixedUpdate()
        {
            if (ActiveShotId < 0 || IsPaused) return;
            if (turns == null || turns.State != TurnManager.TurnState.Shooting ||
                effects == null || !effects.IsShotActive || effects.CurrentShotId != ActiveShotId ||
                gauge == null || !gauge.Gauge.IsShotActive)
            {
                FinishShot(true);
                return;
            }
            // 외부에서 비행 공을 비활성화/제거해도 돌아오지 않을 공을 영원히 기다리지 않는다.
            for (int i = activeBalls.Count - 1; i >= 0; --i)
            {
                BallFlight ball = activeBalls[i];
                if (ball != null && ball.IsFlying && ball.isActiveAndEnabled) continue;
                activeBalls.RemoveAt(i);
                if (ball != null) Destroy(ball.gameObject);
            }
            if (PendingBallCount == 0)
            {
                if (activeBalls.Count == 0) FinishShot(true);
                return;
            }
            untilNextBall -= Time.fixedDeltaTime;
            while (PendingBallCount > 0 && untilNextBall <= 0.000001f)
            {
                LaunchNextBall();
                untilNextBall += BallInterval;
            }
        }

        private void LaunchNextBall()
        {
            int index = nextIndex++;
            BallFlight ball = Instantiate(ballPrefab, LaunchOrigin, Quaternion.identity);
            ball.name = $"Ball {index + 1} ({shotOrder[index]})";
            ball.gameObject.SetActive(true);
            activeBalls.Add(ball);
            ball.Launch(effects, ActiveShotId, index + 1, Field, LaunchOrigin, shotDirection, OnNaturalReturn);
        }

        private void OnNaturalReturn(BallFlight ball, float x)
        {
            if (ActiveShotId < 0 || !activeBalls.Remove(ball)) return;
            if (!hasReturnX)
            {
                hasReturnX = true;
                firstReturnX = Mathf.Clamp(x, Field.xMin + Radius, Field.xMax - Radius);
            }
            Destroy(ball.gameObject);
            if (PendingBallCount == 0 && activeBalls.Count == 0) FinishShot(true);
        }

        public void RecallAll()
        {
            if (ActiveShotId >= 0 && !IsPaused) FinishShot(true);
        }

        private void FinishShot(bool advanceTurn)
        {
            int id = ActiveShotId;
            if (id < 0) return;
            ActiveShotId = -1; // 정리 도중 재호출되어도 CompleteShot은 한 번만 실행한다.
            foreach (BallFlight ball in activeBalls)
            {
                if (ball == null) continue;
                ball.Cancel();
                Destroy(ball.gameObject);
            }
            activeBalls.Clear();
            shotOrder.Clear();
            if (hasReturnX) LaunchOrigin = new Vector2(firstReturnX, Field.yMin + Radius);
            UpdateMarker();
            if (effects != null) effects.EndShot(id);
            if (gauge != null) gauge.EndShot();
            if (advanceTurn && turns != null && turns.State == TurnManager.TurnState.Shooting)
                turns.CompleteShot();
        }
    }
}
