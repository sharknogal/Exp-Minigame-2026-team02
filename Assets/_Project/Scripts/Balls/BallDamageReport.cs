using System;
using System.Collections.Generic;

namespace BrickBreaker.Balls
{
    // 파괴된 GameObject를 다시 읽지 않아도 보상과 게이지를 계산할 수 있는 기록이다.
    public sealed class DamageRecord
    {
        public int ShotId { get; }
        public int BallId { get; }
        public BallEffectType BallType { get; }
        public int Row { get; }
        public int Column { get; }
        public bool IsDirect { get; }
        public int RequestedDamage { get; }
        public int ActualDamage { get; }
        public long GaugeCharge { get; }
        public bool DestroyedNow { get; }
        public int MaxHealth { get; }
        public bool IsGold { get; }

        internal DamageRecord(int shotId, BallEffectState ball, int row, int column,
            bool isDirect, int requestedDamage, int actualDamage,
            bool destroyedNow, int maxHealth, bool isGold)
        {
            ShotId = shotId;
            BallId = ball.BallId;
            BallType = ball.Type;
            Row = row;
            Column = column;
            IsDirect = isDirect;
            RequestedDamage = requestedDamage;
            ActualDamage = actualDamage;
            GaugeCharge = (long)actualDamage * (ball.Type == BallEffectType.Charge ? 2 : 1);
            DestroyedNow = destroyedNow;
            MaxHealth = maxHealth;
            IsGold = isGold;
        }
    }

    // 직접 적중 + 즉시 효과, 또는 화염 예약 1회의 결과를 한 묶음으로 전달한다.
    public sealed class DamageBatch
    {
        public int ShotId { get; }
        public int BallId { get; }
        public IReadOnlyList<DamageRecord> Records { get; }
        public long ActualDamage { get; }
        public long GaugeCharge { get; }
        public int DestroyedCount { get; }
        public int ComboDelta { get; }

        internal DamageBatch(int shotId, int ballId, bool isDirectHit,
            List<DamageRecord> records)
        {
            ShotId = shotId;
            BallId = ballId;
            ComboDelta = isDirectHit ? 1 : 0;
            Records = Array.AsReadOnly(records.ToArray());

            // 여러 블록의 합계는 int 범위를 넘을 수 있어 long으로 합산한다.
            foreach (DamageRecord record in Records)
            {
                ActualDamage += record.ActualDamage;
                GaugeCharge += record.GaugeCharge;
                if (record.DestroyedNow)
                    DestroyedCount++;
            }
        }
    }

    // ApplyHit의 즉시 반환값. 미래의 화염 피해는 나중에 이벤트로 전달한다.
    public readonly struct HitResult
    {
        public bool IsValid { get; }
        public bool DirectTargetDestroyed { get; }
        public long ImmediateActualDamage { get; }
        public long ImmediateGaugeCharge { get; }
        public int ComboDelta { get; }

        public static HitResult Invalid => default;

        internal HitResult(bool directTargetDestroyed, DamageBatch batch)
        {
            IsValid = true;
            DirectTargetDestroyed = directTargetDestroyed;
            ImmediateActualDamage = batch.ActualDamage;
            ImmediateGaugeCharge = batch.GaugeCharge;
            ComboDelta = batch.ComboDelta;
        }
    }
}
