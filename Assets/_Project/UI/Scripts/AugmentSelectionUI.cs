using TMPro;
using UnityEngine;

public class AugmentSelectionUI : MonoBehaviour
{
    [SerializeField] private AugmentPurchaseManager purchaseManager;
    [SerializeField] private AugmentInventory inventory;

    [SerializeField] private GameObject selectionPanel;
    [SerializeField] private TMP_Text[] cardLabels;
    [SerializeField] private TMP_Text remainingSelectionText;

    private string GetRarityName(AugmentRarity rarity)
    {
        switch (rarity)
        {
            case AugmentRarity.Common:
                return "일반";

            case AugmentRarity.Rare:
                return "희귀";

            case AugmentRarity.Epic:
                return "영웅";

            case AugmentRarity.Legendary:
                return "전설";

            default:
                return "";
        }
    }

    private string GetOwnedInfo(AugmentData data)
    {
        int count = inventory.GetOwnedCount(data.Id);

        switch (data.Category)
        {
            case AugmentCategory.Ball:
                return $"현재 보유: {count}개 → 선택 후 {count + 1}개";

            case AugmentCategory.Ability:
                if (count == 0)
                {
                    return "미보유 → 선택 시 획득 / 피해 100";
                }

                int damage = 100 * count;
                int nextDamage = 100 * (count + 1);
                int upgradeCount = count - 1;

                return
                    $"현재 피해: {damage} / 강화 {upgradeCount}회\n" +
                    $"선택 후 피해: {nextDamage}";

            case AugmentCategory.Special:
                return $"누적 획득: {count}회 → 선택 후 {count + 1}회";

            default:
                return "";
        }
    }

    private void OnEnable()
    {
        purchaseManager.SelectionChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        purchaseManager.SelectionChanged -= Refresh;
    }

    private void Refresh()
    {
        bool selecting = purchaseManager.IsSelecting;

        selectionPanel.SetActive(selecting);

        if (!selecting)
            return;

        if (remainingSelectionText != null)
        {
            bool isMultiplePurchase = purchaseManager.TotalSelections > 1;

            remainingSelectionText.gameObject.SetActive(isMultiplePurchase);

            remainingSelectionText.text =
                $"남은 선택: {purchaseManager.RemainingSelections}회";
        }

        for (int i = 0; i < cardLabels.Length; i++)
        {
            AugmentData data = purchaseManager.GetCandidate(i);

            if (data == null)
            {
                cardLabels[i].text = "";
                continue;
            }

            string rarityName = GetRarityName(data.Rarity);
            string ownedInfo = GetOwnedInfo(data);

            cardLabels[i].text =
                $"{data.DisplayName} [{rarityName}]\n" +
                $"{data.Description}\n" +
                ownedInfo;
        }
    }

    public void SelectCard(int index)
    {
        purchaseManager.SelectCandidate(index);
    }
}
