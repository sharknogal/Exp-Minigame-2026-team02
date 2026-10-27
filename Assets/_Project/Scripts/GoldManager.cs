using UnityEngine;

public class GoldManager : MonoBehaviour
{
    public int Gold { get; private set; } //보유 골드
    public int Score { get; private set; } // 점수
    public int PendingInterest { get; private set; } // 이자

    public void BeginGame()
    {
        Gold = 0;
        Score = 0;
        PendingInterest = 0;
        AddGold(120);
    }

    public void AddGold(int amount)
    {
        if (amount <= 0)
            return;

        Gold += amount;
        Score += amount;

        Debug.Log(
            $"+{amount}골드 / 보유 골드: {Gold}, 점수: {Score}",
            this
        );
    }

    public void LockInterest()
    {
        PendingInterest = Mathf.Min(Gold / 10, 5);//이자=보유 골드 10당 1골드,최대 5골드

        Debug.Log(
        $"발사 시 골드: {Gold}, 다음 턴 이자: {PendingInterest}",
        this
    );
    }

    public void PayInterest()
    {
        int amount = PendingInterest;

        PendingInterest = 0;

        AddGold(amount);
    }

    public void CancelInterest()
    {
        PendingInterest = 0;
    }

    public bool TrySpendGold(int amount)
    {
        if (amount <= 0)
            return false;

        if (Gold < amount)
            return false;

        Gold -= amount;

        Debug.Log($"{amount}골드 사용 / 보유 골드: {Gold}, 점수: {Score}",
        this);

        return true;
    }

    [ContextMenu("Test Spend 40 Gold")]
    private void TestSpend40Gold()
    {
        if (!Application.isPlaying)
            return;

        if (!TrySpendGold(40))
            Debug.Log("골드를 사용할 수 없습니다.", this);
    }
}
