using System.Collections.Generic;
using UnityEngine;

public class AugmentInventory : MonoBehaviour
{
    private readonly List<AugmentId> balls = new List<AugmentId>();

    private readonly Dictionary<AugmentId, int> acquisitionCounts = new Dictionary<AugmentId, int>();

    public int BallCount => balls.Count;
    public int BallAttackPower => 10 + 2 * GetOwnedCount(AugmentId.AttackUp);//공 공격력 = 10 + 2 × 공격력 업 획득 횟수

    //게임 시작시 초기화 후 기본 공 한 개로 시작
    public void BeginGame()
    {
        balls.Clear();
        acquisitionCounts.Clear();

        balls.Add(AugmentId.NormalBall);
    }

    public void ApplySelection(AugmentData data)
    {
        if (data == null)
            return;

        if(data.Category == AugmentCategory.Ball)
        {
            // 새 공은 발사 순서의 마지막에 추가
            balls.Add(data.Id);
        }
        else
        {
            int count = GetOwnedCount(data.Id);
            acquisitionCounts[data.Id] = count + 1;
        }

        Debug.Log(
            $"{data.DisplayName} 선택 / " +
            $"보유 수: {GetOwnedCount(data.Id)}, " +
            $"전체 공 수: {BallCount}, " +
            $"공 공격력: {BallAttackPower}",
            this
            );
    }

    public int GetOwnedCount(AugmentId id)
    {
        int ballCount = 0;

        foreach (AugmentId ball in balls)
        {
            if (ball == id)
                ballCount++;
        }

        if (ballCount > 0)
            return ballCount;

        if (acquisitionCounts.TryGetValue(id, out int count))
            return count;

        return 0;
    }

    [SerializeField] private AugmentData testSelection;

    [ContextMenu("Test Apply Selection")]
    private void TestApplySelection()
    {
        if (Application.isPlaying)
            ApplySelection(testSelection);
    }
}
