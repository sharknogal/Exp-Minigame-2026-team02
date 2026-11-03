using UnityEngine;

namespace BrickBreaker.Launch.Testing
{
    // 전용 플레이 씬의 시작/실험 도구. 실제 게임은 기존 시작·구매 흐름을 그대로 사용한다.
    public sealed class BallLaunchPlaytest : MonoBehaviour
    {
        [SerializeField] private TurnManager turns;
        [SerializeField] private BallShotController shots;
        [SerializeField] private AugmentInventory inventory;
        [SerializeField] private AugmentData testBall;

        private void Start()
        {
            if (turns.State == TurnManager.TurnState.Idle)
            {
                turns.BeginGame();
                shots.ResetForNewGame();
            }
        }

        [ContextMenu("Add Selected Ball (Play Mode)")]
        public void AddSelectedBall()
        {
            if (!Application.isPlaying || !shots.CanAim || testBall == null ||
                testBall.Category != AugmentCategory.Ball)
                return;
            inventory.ApplySelection(testBall);
            shots.SyncInventory();
        }
    }
}
