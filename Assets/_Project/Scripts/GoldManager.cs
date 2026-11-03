using UnityEngine;

public class GoldManager : MonoBehaviour
{

    [SerializeField] private AugmentInventory augmentInventory;//이자 증가 증강을 몇번 획득했는지 조회하기 위해 가져옴

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
        if (augmentInventory == null)
        {
            PendingInterest = 0;
            Debug.LogError("AugmentInventory를 연결해주세요.", this);
            return;
        }

        if(Gold < 10)
        {
            PendingInterest = 0;
        }
        else
        {
            int baseInterst = Mathf.Min(Gold / 10, 5); //기본 이자

            int bonusInterest = augmentInventory.GetOwnedCount(AugmentId.InterestUp);//획득한 이자 증가 증강당 추가 이자

            PendingInterest = baseInterst + bonusInterest;// 총 이자
        }
   
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

    //블록 파괴 보상 구현
    public void AddBlockReward(int maxHealth, bool isGold)
    {
        if (maxHealth <= 0)
            return;

        if(augmentInventory == null)
        {
            Debug.LogError("AugmentInventory를 연결해주세요.", this);
            return;
        }

        int baseReward = Mathf.CeilToInt(maxHealth / 2f);//B = 올림(블록 최대 체력 ÷ 2)

        int count = augmentInventory.GetOwnedCount(AugmentId.DestructionGoldUp);

        int percent = 100 + 10 * count;

        int reward = Mathf.CeilToInt(baseReward * (percent / 100f)); //G = 올림[B × (100 + 10 × n) ÷ 100]

        if (isGold)
            reward *= 2;//골드 속성의 블록은 2배

        AddGold(reward);
    }

    [ContextMenu("Test Add 100 Gold")]
    private void TestAdd100Gold()
    {
        if (Application.isPlaying)
            AddGold(100);
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
