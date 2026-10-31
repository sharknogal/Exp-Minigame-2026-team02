using BrickBreaker.Balls;
using UnityEngine;

namespace BrickBreaker.Skills
{
    // 공 효과의 결과를 게이지로 전달한다. 피해 계산과 화면 표시는 맡지 않는다.
    [DisallowMultipleComponent]
    public sealed class SharedSkillGaugeController : MonoBehaviour
    {
        [SerializeField] private BallEffectController effects;

        private BallEffectController subscribedEffects;

        public SharedSkillGauge Gauge { get; } = new SharedSkillGauge();

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            Gauge.EndShot();
        }

        public void Configure(BallEffectController source)
        {
            Unsubscribe();
            effects = source;
            if (isActiveAndEnabled)
                Subscribe();
        }

        public void BeginGame()
        {
            Gauge.ResetGame();
        }

        public void SetHasAbilities(bool value)
        {
            Gauge.SetHasAbilities(value);
        }

        // 발사 담당 코드는 실제 첫 공을 내보내기 전에 호출한다.
        public void BeginShot()
        {
            if (isActiveAndEnabled)
                Gauge.BeginShot();
        }

        public void EndShot()
        {
            Gauge.EndShot();
        }

        private void Subscribe()
        {
            if (effects == null || subscribedEffects != null)
                return;

            subscribedEffects = effects;
            subscribedEffects.DamageBatchCompleted += HandleDamageBatch;
        }

        private void Unsubscribe()
        {
            if (subscribedEffects != null)
                subscribedEffects.DamageBatchCompleted -= HandleDamageBatch;
            subscribedEffects = null;
        }

        private void HandleDamageBatch(DamageBatch batch)
        {
            if (!isActiveAndEnabled || batch == null)
                return;

            // 실제 체력 감소와 충전공 2배가 이미 계산되어 있다.
            // HitResult를 더하거나 충전공 배율을 다시 곱하면 중복 충전된다.
            Gauge.AddBallCharge(batch.GaugeCharge);
        }
    }
}
