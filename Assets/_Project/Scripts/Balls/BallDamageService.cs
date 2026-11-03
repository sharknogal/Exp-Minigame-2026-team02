using System;

namespace BrickBreaker.Balls
{
    // 모든 공 피해가 통과하는 한 곳이다. 블록의 체력과 삭제는 Block이 담당한다.
    internal sealed class BallDamageService
    {
        private readonly Func<BallGaugeBalance> getBalance;
        private int remainingHundredths;

        public BallDamageService(Func<BallGaugeBalance> getBalance)
        {
            this.getBalance = getBalance;
        }

        public void EndShot()
        {
            remainingHundredths = 0;
        }

        public DamageRecord Apply(Block target, int amount, int shotId,
            BallEffectState ball, int row, int column, bool isDirect)
        {
            // Destroy는 프레임 끝에 반영되므로 null 검사만으로는 부족하다.
            if (target == null || target.IsDestroyed || amount <= 0)
                return null;

            int healthBefore = target.CurrentHealth;
            int maxHealth = target.MaxHealth;
            bool isGold = target.IsGold;

            bool destroyed = target.TakeDamage(amount);
            int actualDamage = healthBefore - target.CurrentHealth;

            long charge = CalculateCharge(ball.Type, isDirect, actualDamage);
            return new DamageRecord(shotId, ball, row, column, isDirect,
                amount, actualDamage, charge, destroyed, maxHealth, isGold);
        }

        private long CalculateCharge(BallEffectType type, bool isDirect, int actualDamage)
        {
            if (isDirect)
                return (long)actualDamage * (type == BallEffectType.Charge ? 2 : 1);

            // 예약된 화염도 피해를 주는 순간의 Inspector 설정을 사용한다.
            BallGaugeBalance balance = getBalance();
            int percent = balance == null ? 100 : balance.GetAdditionalPercent(type);

            // 1/100 단위로 보관한다. 같은 발사에서 0.5가 두 번 생기면 정확히 1이 된다.
            // 잔여분은 모든 공이 공유하되 발사 종료 시 버린다. 이미 보고한 기록은 바꾸지 않는다.
            long hundredths = (long)actualDamage * percent + remainingHundredths;
            remainingHundredths = (int)(hundredths % 100);
            return hundredths / 100;
        }
    }
}
