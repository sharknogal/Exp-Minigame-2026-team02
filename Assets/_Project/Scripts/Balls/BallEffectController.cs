using System;
using System.Collections.Generic;
using UnityEngine;

namespace BrickBreaker.Balls
{
    // 읽기 시작점: ApplyHit → 효과별 메서드 → BallDamageService → 결과 보고.
    [DisallowMultipleComponent]
    public sealed class BallEffectController : MonoBehaviour
    {
        [SerializeField] private GridManager grid;
        [SerializeField] private Transform blockRoot;

        private readonly Dictionary<int, BallEffectState> balls = new Dictionary<int, BallEffectState>();
        private readonly BallFireScheduler fire = new BallFireScheduler();
        private BallAreaEffects areaEffects;
        private int latestShotId = -1;

        public bool IsShotActive { get; private set; }
        public int CurrentShotId => latestShotId;
        public int PendingFireDamageCount => fire.PendingCount;
        public BallBoardLookup Board { get; private set; }
        public event Action<DamageBatch> DamageBatchCompleted;

        private void Awake()
        {
            if (grid != null && blockRoot != null)
                Configure(grid, blockRoot);
        }

        // seed는 감전 테스트를 재현할 때만 지정한다. 게임에서는 생략한다.
        public void Configure(GridManager board, Transform blocksParent, int? randomSeed = null)
        {
            var lookup = new BallBoardLookup(board, blocksParent);
            CloseCurrentShot();
            grid = board;
            blockRoot = blocksParent;
            Board = lookup;
            var random = randomSeed.HasValue ? new System.Random(randomSeed.Value) : new System.Random();
            areaEffects = new BallAreaEffects(Board, random);
        }

        public void BeginShot(int shotId)
        {
            // 구간 번호는 0부터 증가시킨다. 늦게 도착한 예전 요청으로 되돌아가지 않는다.
            if (!Application.isPlaying || !isActiveAndEnabled || Board == null || !Board.IsAvailable ||
                areaEffects == null || shotId <= latestShotId)
                return;

            CloseCurrentShot();
            latestShotId = shotId;
            IsShotActive = true;
        }

        public bool RegisterBall(int shotId, int ballId, BallEffectType type, int attackPower)
        {
            if (!IsCurrentShot(shotId) || ballId < 0 || balls.ContainsKey(ballId) ||
                !Enum.IsDefined(typeof(BallEffectType), type) || attackPower < 10 || attackPower % 2 != 0)
                return false;

            balls.Add(ballId, new BallEffectState(ballId, type, attackPower));
            return true;
        }

        public HitResult ApplyHit(int shotId, int ballId, Block target)
        {
            if (!IsCurrentShot(shotId) || Board == null ||
                !balls.TryGetValue(ballId, out BallEffectState ball) ||
                !Board.TryGetCell(target, out int row, out int column))
                return HitResult.Invalid;

            bool isFirstHit = !ball.HasHitBlock;
            ball.HasHitBlock = true;
            var records = new List<DamageRecord>();

            // 좌표는 파괴 전에 확보했고, 직접 피해는 모든 공에 공통으로 한 번만 준다.
            records.Add(BallDamageService.Apply(target, ball.AttackPower,
                shotId, ball, row, column, true));
            ApplyAdditionalEffect(ball, target, row, column, isFirstHit, records);

            var batch = new DamageBatch(shotId, ballId, true, records);
            var result = new HitResult(target == null || target.IsDestroyed, batch);
            Publish(batch);
            return result;
        }

        public void EndShot(int shotId)
        {
            if (IsCurrentShot(shotId))
                CloseCurrentShot();
        }

        private void ApplyAdditionalEffect(BallEffectState ball, Block target,
            int row, int column, bool isFirstHit, List<DamageRecord> records)
        {
            switch (ball.Type)
            {
                case BallEffectType.Explosion:
                    areaEffects.ApplyExplosion(row, column, latestShotId, ball, records);
                    break;
                case BallEffectType.Electric:
                    areaEffects.ApplyElectric(row, column, latestShotId, ball, records);
                    break;
                case BallEffectType.HorizontalLaser:
                    if (isFirstHit)
                        areaEffects.ApplyRow(row, latestShotId, ball, records);
                    break;
                case BallEffectType.VerticalLaser:
                    if (isFirstHit)
                        areaEffects.ApplyColumn(column, latestShotId, ball, records);
                    break;
                case BallEffectType.CrossLaser:
                    if (isFirstHit)
                    {
                        // 중심의 추가 피해 두 번은 의도된 규칙이므로 중복 제거하지 않는다.
                        areaEffects.ApplyRow(row, latestShotId, ball, records);
                        areaEffects.ApplyColumn(column, latestShotId, ball, records);
                    }
                    break;
                case BallEffectType.Fire:
                    fire.Schedule(Time.timeAsDouble, latestShotId, ball, target, row, column);
                    break;
                // 일반공은 추가 효과가 없고, 충전공의 배율은 피해 기록에서 계산한다.
                case BallEffectType.Normal:
                case BallEffectType.Charge:
                    break;
            }
        }

        private bool IsCurrentShot(int shotId)
        {
            return Application.isPlaying && isActiveAndEnabled &&
                IsShotActive && shotId == latestShotId;
        }

        private void Update()
        {
            if (IsShotActive && Time.deltaTime > 0f)
                fire.Tick(Time.timeAsDouble, latestShotId, Publish);
        }

        private void Publish(DamageBatch batch)
        {
            DamageBatchCompleted?.Invoke(batch);
        }

        private void CloseCurrentShot()
        {
            IsShotActive = false;
            balls.Clear();
            fire.Clear();
        }

        private void OnDisable()
        {
            CloseCurrentShot();
        }
    }
}
