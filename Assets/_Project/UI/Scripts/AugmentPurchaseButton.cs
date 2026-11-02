using UnityEngine;

public class AugmentPurchaseButton : MonoBehaviour
{
    [SerializeField] private AugmentPurchaseManager purchaseManager;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private GoldManager goldManager;

    [SerializeField] private UnityEngine.UI.Button purchaseButton;
    [SerializeField] private bool sixPurchase;

    private void Update()
    {
        int price = sixPurchase ? 220 : 40;

        purchaseButton.interactable =
            turnManager.State == TurnManager.TurnState.Aiming &&//조준 상태이고
            !purchaseManager.IsSelecting &&//카드를 선택 중이지 않고
            goldManager.Gold >= price;//골드가 price이상이라면 클릭 가능
    }

    public void OnClickPurchase()
    {
        if (sixPurchase)
            purchaseManager.TryPurchaseSix();
        else
            purchaseManager.TryPurchaseSingle();
    }
}
