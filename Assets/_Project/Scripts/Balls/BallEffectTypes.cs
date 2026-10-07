namespace BrickBreaker.Balls
{
    // Inspector와 호출 코드에서 사용하는 공 종류. 숫자는 저장 후 바꾸지 않는다.
    public enum BallEffectType
    {
        Normal = 0,
        Explosion = 1,
        Electric = 2,
        HorizontalLaser = 3,
        VerticalLaser = 4,
        CrossLaser = 5,
        Fire = 6,
        Charge = 7
    }

    // 한 발사 구간에 참여하는 공 하나의 상태다. 실제 공 GameObject는 필요 없다.
    internal sealed class BallEffectState
    {
        public int BallId { get; }
        public BallEffectType Type { get; }
        public int AttackPower { get; }
        public bool HasHitBlock { get; set; }

        public BallEffectState(int ballId, BallEffectType type, int attackPower)
        {
            BallId = ballId;
            Type = type;
            AttackPower = attackPower;
        }
    }
}
