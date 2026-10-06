namespace BrickBreaker.Balls
{
    // 모든 공 피해가 통과하는 한 곳이다. 블록의 체력과 삭제는 Block이 담당한다.
    internal static class BallDamageService
    {
        public static DamageRecord Apply(Block target, int amount, int shotId,
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

            return new DamageRecord(shotId, ball, row, column, isDirect,
                amount, actualDamage, destroyed, maxHealth, isGold);
        }
    }
}
