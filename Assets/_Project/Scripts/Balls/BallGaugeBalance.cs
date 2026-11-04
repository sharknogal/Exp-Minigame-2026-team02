using UnityEngine;

namespace BrickBreaker.Balls
{
    // 직접 적중은 그대로 두고, 각 공의 추가 피해에서 얻는 게이지만 조정한다.
    [CreateAssetMenu(menuName = "Brick Breaker/Ball Gauge Balance")]
    public sealed class BallGaugeBalance : ScriptableObject
    {
        [Header("Additional damage → gauge (%)")]
        [SerializeField, Range(0, 100)] private int explosionPercent = 100;
        [SerializeField, Range(0, 100)] private int electricPercent = 100;
        [SerializeField, Range(0, 100)] private int horizontalLaserPercent = 100;
        [SerializeField, Range(0, 100)] private int verticalLaserPercent = 100;
        [SerializeField, Range(0, 100)] private int crossLaserPercent = 100;
        [SerializeField, Range(0, 100)] private int firePercent = 100;

        public int GetAdditionalPercent(BallEffectType type)
        {
            int percent;
            switch (type)
            {
                case BallEffectType.Explosion:       percent = explosionPercent; break;
                case BallEffectType.Electric:        percent = electricPercent; break;
                case BallEffectType.HorizontalLaser: percent = horizontalLaserPercent; break;
                case BallEffectType.VerticalLaser:   percent = verticalLaserPercent; break;
                case BallEffectType.CrossLaser:      percent = crossLaserPercent; break;
                case BallEffectType.Fire:            percent = firePercent; break;
                default: return 100;
            }
            return Mathf.Clamp(percent, 0, 100);
        }
    }
}
