using System.Collections.Generic;
using UnityEngine;

public class AugmentRoller : MonoBehaviour
{
    [SerializeField] private AugmentData[] allAugments;
    [SerializeField] private AugmentInventory augmentInventory;

    public AugmentData[] RollCandidates()
    {
        if (augmentInventory == null)
        {
            Debug.LogError("AugmentInventory를 연결해주세요.", this);
            return null;
        }

        int rarityUpCount = augmentInventory.GetOwnedCount(AugmentId.RarityUp);

        Debug.Log($"등급 업 {rarityUpCount}회 적용하여 추첨", this);

        AugmentData[] candidates = new AugmentData[3];

        candidates[0] = RollOne(AugmentCategory.Ball, rarityUpCount);//1번 슬롯 추첨(공 100%)
        candidates[1] = RollOne(AugmentCategory.Ball, rarityUpCount);//2번 슬롯 추첨(공 100%)

        int roll = Random.Range(0, 100);

        AugmentCategory category;

        if (roll < 40)
            category = AugmentCategory.Ball;
        else if (roll < 70)
            category = AugmentCategory.Ability;
        else
            category = AugmentCategory.Special;

        candidates[2] = RollOne(category, rarityUpCount);//3번 슬롯 추첨(공 40% / 특수 능력 30% / 특수 증강 30%)

        return candidates;
    }

    //지정한 종류에서 카드 한장을 추첨하는 함수
    private AugmentData RollOne(AugmentCategory category, int rarityUpCount)
    {

        /*등급별 가중치 준비
       → 해당 종류의 데이터를 등급별로 분류
       → 존재하는 등급의 가중치 합산
       → 등급 추첨
       → 그 등급 안에서 카드 한 장 추첨*/

        int count = Mathf.Max(0, rarityUpCount);

        //Common, Rare, Epic, Legendary 순서
        int[] weights =
        {
            700,
            230 + 20 * count,
            60 + 10 * count,
            10 + 2 * count
        };

        List<AugmentData>[] pools = new List<AugmentData>[4];

        for(int i = 0; i<pools.Length; i++)
        {
            pools[i] = new List<AugmentData>();
        }

        foreach(AugmentData data in allAugments)
        {
            if (data == null || data.Category != category)
                continue;

            int rarityIndex = (int)data.Rarity;
            pools[rarityIndex].Add(data);
        }

        int totalWeight = 0;

        for(int i=0;i<pools.Length; i++)
        {
            if (pools[i].Count > 0)
            {
                totalWeight += weights[i];
            }
        }

        if(totalWeight == 0)
        {
            Debug.LogError($"{category} 종류의 증강 데이터가 없습니다.", this);

            return null;
        }

        int roll = Random.Range(0, totalWeight);

        for(int i = 0;i<pools.Length; i++)
        {
            if (pools[i].Count == 0)
                continue;

            if(roll < weights[i])
            {
                int index = Random.Range(0, pools[i].Count);

                return pools[i][index];
            }

            roll -= weights[i];
        }

        return null;

    }

    [ContextMenu("Test Roll Candidates")]
    private void TestRollCandidates()
    {
        if (!Application.isPlaying)
            return;

        AugmentData[] candidates = RollCandidates();

        if (candidates == null)
            return;

        for (int i = 0; i < candidates.Length; i++)
        {
            AugmentData data = candidates[i];

            if (data == null)
                continue;

            Debug.Log(
                $"{i + 1}번: {data.DisplayName} / " +
                $"{data.Category} / {data.Rarity}",
                this
            );
        }
    }
}
