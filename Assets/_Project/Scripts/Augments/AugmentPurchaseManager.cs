using System;
using UnityEngine;

public class AugmentPurchaseManager : MonoBehaviour
{
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private GoldManager goldManager;
    [SerializeField] private AugmentRoller roller;
    [SerializeField] private AugmentInventory inventory;

    private AugmentData[] candidates;

    public bool IsSelecting { get; private set; }
    public int RemainingSelections { get; private set; }
    public int TotalSelections { get; private set; }

    private int lastSelectionFrame = -1;

    public event Action SelectionChanged;

    public bool TryPurchaseSingle()
    {
        return TryPurchase(40, 1);
    }

    public bool TryPurchaseSix()
    {
        return TryPurchase(220, 6);
    }

    public bool TryPurchase(int price, int selectionCount) //증강 구매 시도
    {

        if (Time.timeScale == 0f)//일시 정지시
            return false;

        if (turnManager == null || goldManager == null || roller == null || inventory == null)
        {
            Debug.LogError("구매 관리자 연결을 확인해주세요.", this);
            return false;
        }

        if(IsSelecting || turnManager.State != TurnManager.TurnState.Aiming)
        {
            return false;
        }

        if(goldManager.Gold < price)
        {
            Debug.Log("구매 골드가 부족합니다.", this);
            return false;
        }

        AugmentData[] rolled = roller.RollCandidates();// 1,2,3번 증강을 추첨

        if (!AreCandidatesValid(rolled))
            return false;

        if (!turnManager.BeginAugmentSelection())
            return false;

        if (!goldManager.TrySpendGold(price))
        {
            turnManager.CompleteAugmentSelection();//증강 선택 상태로 입장
            return false;
        }

        candidates = rolled;
        TotalSelections = selectionCount;
        RemainingSelections = selectionCount;
        IsSelecting = true;
        

        SelectionChanged?.Invoke(); //선택 시작 시 UI에 알림

        for (int i = 0; i < candidates.Length; i++)
        {
            Debug.Log(
                $"{i + 1}번 후보: {candidates[i].DisplayName}",
                this
            );
        }

        return true;
    }

    private bool AreCandidatesValid(AugmentData[] rolled)//후보 데이터 검사 함수
    {
        if (rolled == null || rolled.Length != 3)
            return false;

        foreach (AugmentData data in rolled)
        {
            if (data == null)
                return false;
        }

        return true;
    }

    public void SelectCandidate(int index)
    {
        if (!IsSelecting || candidates == null)
            return;

        if (index < 0 || index >= candidates.Length)
            return;

        if (lastSelectionFrame == Time.frameCount)
            return;

        lastSelectionFrame = Time.frameCount;

        AugmentData selected = candidates[index];//3개의 후보중 선택된 증강 

        candidates = null;

        inventory.ApplySelection(selected);//선택한 증강을 인벤토리에 추가
        RemainingSelections--;//남은 횟수 차감

        if (RemainingSelections > 0)
        {
            AugmentData[] rolled = roller.RollCandidates();

            if (!AreCandidatesValid(rolled))
            {
                Debug.LogError(
                    "다음 후보 생성에 실패했습니다. 데이터 연결을 확인해주세요.",
                    this
                );
                return;
            }

            candidates = rolled;

            SelectionChanged?.Invoke();

            Debug.Log($"남은 선택: {RemainingSelections}회", this);
            return;
        }
        // 같은 구매에서 두 번 선택하지 못하도록 먼저 닫는다.
        IsSelecting = false;
        turnManager.CompleteAugmentSelection(); //선택 완료했으니 조준 단계로 복귀

        SelectionChanged?.Invoke();//선택 종료 시 UI에 알림

        Debug.Log("구매 완료: 조준 단계로 복귀", this);
    }

    public AugmentData GetCandidate(int index)
    {
        if(candidates == null || index < 0 || index >= candidates.Length)
        {
            return null;
        }

        return candidates[index];
    }
    [ContextMenu("Test Purchase Single")]
    private void TestPurchaseSingle()
    {
        if (Application.isPlaying)
            TryPurchaseSingle();
    }

    [ContextMenu("Test Select First")]
    private void TestSelectFirst()
    {
        if (Application.isPlaying)
            SelectCandidate(0);
    }

    [ContextMenu("Test Select Second")]
    private void TestSelectSecond()
    {
        if (Application.isPlaying)
            SelectCandidate(1);
    }

    [ContextMenu("Test Select Third")]
    private void TestSelectThird()
    {
        if (Application.isPlaying)
            SelectCandidate(2);
    }

}
