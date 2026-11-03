using UnityEngine;

public class PauseUI : MonoBehaviour
{
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private AugmentPurchaseManager purchaseManager;

    [SerializeField] private UnityEngine.UI.Button pauseButton;
    [SerializeField] private GameObject pausePanel;

    public bool IsPaused { get; private set; }

    private float previousTimeScale;

    private void Awake()
    {
        pausePanel.SetActive(false);
    }

    private void Update()
    {
        bool canPause =
            turnManager.State == TurnManager.TurnState.Aiming ||
            turnManager.State == TurnManager.TurnState.Shooting;

        pauseButton.interactable =
            canPause &&
            !purchaseManager.IsSelecting &&
            !IsPaused;
    }
    public void Pause()//게임을 일시정지 시키는 함수
    {
        if (IsPaused || purchaseManager.IsSelecting)
            return;

        bool canPause =
            turnManager.State == TurnManager.TurnState.Aiming ||
            turnManager.State == TurnManager.TurnState.Shooting;

        if (!canPause)
            return;

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f; //게임 시간의 흐름을 멈추는 설정

        IsPaused = true;
        pausePanel.SetActive(true);
    }

    public void Resume()//게임을 이어서 진행하는 함수
    {
        if (!IsPaused)
            return;

        Time.timeScale = previousTimeScale;

        IsPaused = false;
        pausePanel.SetActive(false);
    }

    private void OnDisable()
    {
        // 종료나 장면 전환 후 시간이 멈춘 채 남지 않게 한다.
        if (IsPaused)
        {
            Time.timeScale = previousTimeScale;
            IsPaused = false;
        }
    }
}
