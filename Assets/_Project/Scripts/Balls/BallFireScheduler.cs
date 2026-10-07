using System;
using System.Collections.Generic;

namespace BrickBreaker.Balls
{
    // 예약은 공 오브젝트가 아닌 효과 컨트롤러가 소유한다.
    internal sealed class BallFireScheduler
    {
        private const double TickInterval = 0.15;
        private const int TickCount = 3;
        private readonly List<PendingDamage> pending = new List<PendingDamage>();

        public int PendingCount => pending.Count;

        public void Schedule(double now, int shotId, BallEffectState ball,
            Block target, int row, int column)
        {
            if (target == null || target.IsDestroyed)
                return;

            for (int tick = 1; tick <= TickCount; tick++)
            {
                var damage = new PendingDamage(now + TickInterval * tick,
                    shotId, ball, target, row, column);

                // 여러 적중이 겹쳐도 실행 예정 시각 순서로 처리한다.
                int index = pending.FindIndex(item => item.DueTime > damage.DueTime);
                if (index < 0)
                    pending.Add(damage);
                else
                    pending.Insert(index, damage);
            }
        }

        public void Tick(double now, int activeShotId, Action<DamageBatch> report)
        {
            pending.RemoveAll(item => item.ShotId != activeShotId ||
                item.Target == null || item.Target.IsDestroyed);

            while (pending.Count > 0 && pending[0].DueTime <= now)
            {
                PendingDamage next = pending[0];
                pending.RemoveAt(0);

                DamageRecord record = BallDamageService.Apply(next.Target,
                    next.Ball.AttackPower, next.ShotId, next.Ball, next.Row, next.Column, false);

                if (record == null)
                    continue;

                // 콜백에서 EndShot을 호출해도 안전하도록 예약을 먼저 제거했다.
                report(new DamageBatch(next.ShotId, next.Ball.BallId,
                    false, new List<DamageRecord> { record }));
            }
        }

        public void Clear()
        {
            pending.Clear();
        }

        private sealed class PendingDamage
        {
            public double DueTime { get; }
            public int ShotId { get; }
            public BallEffectState Ball { get; }
            public Block Target { get; }
            public int Row { get; }
            public int Column { get; }

            public PendingDamage(double dueTime, int shotId, BallEffectState ball,
                Block target, int row, int column)
            {
                DueTime = dueTime;
                ShotId = shotId;
                Ball = ball;
                Target = target;
                Row = row;
                Column = column;
            }
        }
    }
}
