using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BrickBreaker.Skills
{
    // 충전 규칙은 모르고, 확정된 상태를 화면에 표시하는 역할만 맡는다.
    // 디자이너의 이미지로 Slider 배경/채움을 바꿔도 게이지 계산은 그대로 유지된다.
    [DisallowMultipleComponent]
    public sealed class SharedSkillGaugeView : MonoBehaviour
    {
        [SerializeField] private SharedSkillGaugeController controller;
        [SerializeField] private Slider gaugeSlider;
        [SerializeField] private TMP_Text chargeLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text activationLabel;

        private SharedSkillGauge observedGauge;

        private void OnEnable()
        {
            if (controller == null)
                return;

            observedGauge = controller.Gauge;
            observedGauge.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (observedGauge != null)
                observedGauge.Changed -= Refresh;

            observedGauge = null;
        }

        private void Refresh()
        {
            if (observedGauge == null)
                return;

            if (gaugeSlider != null)
            {
                gaugeSlider.interactable = false;
                gaugeSlider.SetValueWithoutNotify(observedGauge.NormalizedCharge);
            }

            if (chargeLabel != null)
            {
                int percent = Mathf.RoundToInt(observedGauge.NormalizedCharge * 100f);
                chargeLabel.text = $"{observedGauge.Charge} / {SharedSkillGauge.Capacity}   ({percent}%)";
            }

            if (statusLabel != null)
            {
                string ability = observedGauge.HasAbilities ? "Owned" : "None";
                string phase = observedGauge.IsShotActive ? "Shot active" : "Aiming";
                statusLabel.text = $"Ability: {ability}   |   {phase}";
            }

            if (activationLabel != null)
                activationLabel.text = $"Activation requests: {observedGauge.ActivationCount}";
        }
    }
}
