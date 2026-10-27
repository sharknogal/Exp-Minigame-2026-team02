using UnityEngine;

public class TurnManager: MonoBehaviour
{
    public enum TurnState //현재 상황을 구분하기 위한 이름
    {
        Idle, // 시작 전
        Aiming, // 조준과 구매가 가능한 단계
        Shooting, // 공 발사부터 모든 공 회수까지
        Resolving, // 하강.생성.패배 판정을 처리하는 단계
        GameOver // 게임 종료
    }

    [SerializeField] private GridManager gridManager;
    [SerializeField] private GoldManager goldManager;

    public int CurrentTurn { get; private set; } //현재 턴 번호를 저장
    public TurnState State { get; private set; } = TurnState.Idle;//현재 상태를 저장 Idle로 시작

    public void BeginGame()//첫 턴 준비
    {
        if (State != TurnState.Idle)
            return;

        if(gridManager == null)
        {
            Debug.LogWarning("GridManager를 연결해주세요.", this);
            return;
        }

        if (goldManager == null)
        {
            Debug.LogWarning("GoldManager를 연결해주세요.", this);
            return;
        }

        goldManager.BeginGame();//시작 골드와 점수 설정
        CurrentTurn = 1;//현재 턴을 1로 설정
        gridManager.SpawnNewRow(CurrentTurn);//GridManager에 1턴 벽돌 생성을 요청

        State = TurnState.Aiming;//조준 상태로 변경

        Debug.Log($"{CurrentTurn}턴: 조준 시작", this);
    }

    public void BeginShot()// 발사 단계 시작
    {
        if (State != TurnState.Aiming)
            return;

        goldManager.LockInterest();

        State = TurnState.Shooting;

        Debug.Log($"{CurrentTurn}턴: 발사 시작", this);

    }

    public void CompleteShot()//회수 완료 후 턴 정리
    {
        if (State != TurnState.Shooting)
            return;

        State = TurnState.Resolving;

        if (!gridManager.MoveBlocksDown())//하강에 실패하는 경우
        {
            goldManager.CancelInterest();//이자 지급취소
            State = TurnState.GameOver;//게임오버상태로 변결
            Debug.LogWarning("하강할 수 없어 진행을 중단합니다.", this);
            return;
        }

        int nextTurn = CurrentTurn + 1;

        gridManager.SpawnNewRow(nextTurn);

        if (gridManager.HasBlockInDangerRow())
        {
            goldManager.CancelInterest();//이자 지급취소
            State = TurnState.GameOver;

            Debug.Log($"{CurrentTurn}턴 종료: 게임오버", this);
            return;
        }

        // 다음 턴에 진입할 수 있을 때만 지급한다.
        goldManager.PayInterest();

        CurrentTurn = nextTurn;
        State = TurnState.Aiming;

        Debug.Log($"{CurrentTurn}턴: 조준 시작", this);

        
    }

    [ContextMenu("Test Begin Game")]
    private void TestBeginGame()
    {
        if (Application.isPlaying)
            BeginGame();
    }

    [ContextMenu("Test Begin Shot")]
    private void TestBeginShot()
    {
        if (Application.isPlaying)
            BeginShot();
    }

    [ContextMenu("Test Complete Shot")]
    private void TestCompleteShot()
    {
        if (Application.isPlaying)
            CompleteShot();
    }
}
