using System.Collections;
using BrickBreaker.Balls;
using TMPro;
using UnityEngine;

namespace BrickBreaker.Skills.Testing
{
    // 이 씬에서만 사용하는 실험 도구다. 실제 발사나 특수 능력 공격을 대신하지 않는다.
    public sealed class SharedSkillGaugeTestHarness : MonoBehaviour
    {
        [SerializeField] private GridManager grid;
        [SerializeField] private Transform blockRoot;
        [SerializeField] private BallEffectController effects;
        [SerializeField] private SharedSkillGaugeController controller;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private TMP_Text selectedBallLabel;

        private BallEffectType selectedBall = BallEffectType.Normal;

        public bool IsBusy { get; private set; }
        public long ReceivedActivations { get; private set; }
        public SharedSkillGaugeTestReport TestReport { get; private set; }
        public string TestReportJson => TestReport == null ? "{}" : JsonUtility.ToJson(TestReport, true);
        public string LastMessage { get; private set; }

        private void OnEnable()
        {
            if (controller != null)
                controller.Gauge.AbilityActivationRequested += ReceiveTestActivation;
        }

        private void OnDisable()
        {
            if (controller != null)
                controller.Gauge.AbilityActivationRequested -= ReceiveTestActivation;
        }

        private void Start()
        {
            controller.Configure(effects);
            NewGame();
        }

        public void NewGame()
        {
            if (Application.isPlaying && !IsBusy)
                StartCoroutine(PrepareGame());
        }

        private IEnumerator PrepareGame()
        {
            IsBusy = true;
            effects.EndShot(effects.CurrentShotId);
            controller.BeginGame();
            ReceivedActivations = 0;

            // GridManager가 가진 배열의 이전 Block 참조도 다음 프레임에는 null로 판정된다.
            foreach (Transform child in blockRoot)
                Destroy(child.gameObject);
            yield return null;

            effects.Configure(grid, blockRoot, 12345);
            for (int row = 2; row <= 7; row++)
            {
                for (int column = 1; column <= 6; column++)
                    grid.CreateBlock(row, column, 100);
            }

            selectedBall = BallEffectType.Normal;
            RefreshBallLabel();
            IsBusy = false;
            ShowMessage("Ready. Set abilities, then press Begin shot.");
        }

        public void BeginShot()
        {
            if (!CanUseControls() || controller.Gauge.IsShotActive)
                return;

            effects.BeginShot(effects.CurrentShotId + 1);
            if (!effects.IsShotActive)
            {
                ShowMessage("The block board is not ready.");
                return;
            }

            // 600에서 첫 능력을 얻었다면 여기서 먼저 발동 신호를 처리한다.
            // 실제 게임에서는 이 호출이 끝난 다음 공을 움직이기 시작해야 한다.
            controller.BeginShot();
            for (int type = 0; type < 8; type++)
                effects.RegisterBall(effects.CurrentShotId, type + 1, (BallEffectType)type, 10);

            ShowMessage("Shot started. Hit a block to charge the shared gauge.");
        }

        public void EndShot()
        {
            if (!CanUseControls())
                return;

            effects.EndShot(effects.CurrentShotId);
            controller.EndShot();
            ShowMessage("Shot ended. Charge is kept; pending fire damage is cancelled.");
        }

        public void ToggleAbilities()
        {
            if (!CanUseControls())
                return;
            if (controller.Gauge.IsShotActive)
            {
                ShowMessage("End the shot before changing ability ownership.");
                return;
            }

            controller.SetHasAbilities(!controller.Gauge.HasAbilities);
            ShowMessage(controller.Gauge.HasAbilities
                ? "Test ability owned. A full gauge activates on Begin shot."
                : "No abilities. Charge is capped at 600.");
        }

        public void CycleBallType()
        {
            if (!CanUseControls())
                return;
            selectedBall = (BallEffectType)(((int)selectedBall + 1) % 8);
            RefreshBallLabel();
        }

        public void HitSelectedBall()
        {
            if (!CanUseControls())
                return;
            if (!controller.Gauge.IsShotActive)
            {
                ShowMessage("Press Begin shot first.");
                return;
            }

            Block target = FindTarget();
            if (target == null)
            {
                ShowMessage("All blocks are gone. Press New game to rebuild the board.");
                return;
            }

            HitResult result = effects.ApplyHit(effects.CurrentShotId, (int)selectedBall + 1, target);
            ShowMessage(result.IsValid
                ? $"{selectedBall}: {result.ImmediateActualDamage} immediate damage. Fire charges later."
                : "This hit was rejected.");
            // 충전은 controller의 피해 이벤트 수신부가 담당한다. result로 다시 더하지 않는다.
        }

        public void Add1250ForTest()
        {
            if (!CanUseControls())
                return;
            if (!controller.Gauge.IsShotActive)
            {
                ShowMessage("Press Begin shot first.");
                return;
            }

            // 다중 발동 규칙만 빠르게 확인하는 테스트 입력이다. 게임의 피해 경로가 아니다.
            controller.Gauge.AddBallCharge(1250);
            ShowMessage("Test input: +1250 charge. No actual skill attacks are implemented here.");
        }

        public void RunAllCases()
        {
            if (CanUseControls())
                StartCoroutine(RunSuite());
        }

        private IEnumerator RunSuite()
        {
            IsBusy = true;
            effects.EndShot(effects.CurrentShotId);
            controller.EndShot();
            TestReport = new SharedSkillGaugeTestReport();
            ShowMessage("Running gauge rules and real ball-effect integration tests...");
            try
            {
                yield return SharedSkillGaugeTestSuite.Run(TestReport);
            }
            finally
            {
                IsBusy = false;
            }
            ShowMessage($"Tests: {TestReport.passed} passed / {TestReport.failed} failed. Charge is preserved.");
            Debug.Log(LastMessage, this);
        }

        private Block FindTarget()
        {
            // 먼저 중앙 칸을 사용해 폭발과 레이저를 관찰하기 쉽게 한다.
            Block center = effects.Board.GetBlock(4, 3);
            if (center != null)
                return center;
            for (int row = 2; row <= 7; row++)
            {
                for (int column = 1; column <= 6; column++)
                {
                    Block block = effects.Board.GetBlock(row, column);
                    if (block != null)
                        return block;
                }
            }
            return null;
        }

        private bool CanUseControls()
        {
            return Application.isPlaying && !IsBusy && controller != null && effects != null;
        }

        private void RefreshBallLabel()
        {
            if (selectedBallLabel != null)
                selectedBallLabel.text = $"Ball: {selectedBall}  |  Attack: 10";
        }

        private void ReceiveTestActivation()
        {
            // 요청을 받았다는 사실만 기록한다. 네 가지 능력의 실제 공격은 후속 작업이다.
            ReceivedActivations++;
            ShowMessage($"Activation signal received: {ReceivedActivations}. No skill damage in this prototype.");
        }

        private void ShowMessage(string message)
        {
            LastMessage = message;
            if (messageLabel != null)
                messageLabel.text = message;
        }
    }
}
