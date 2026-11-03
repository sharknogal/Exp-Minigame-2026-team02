using TMPro;
using UnityEngine;

public class GameStatusUI : MonoBehaviour
{
    [SerializeField] private GoldManager goldManager;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private AugmentInventory inventory;

    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text turnText;
    [SerializeField] private TMP_Text ballCountText;
    [SerializeField] private TMP_Text attackPowerText;

    private int displayedGold;
    private int displayedScore;
    private int displayedTurn;
    private int displayedBallCount;
    private int displayedAttackPower;

    private void OnEnable()
    {
        // 첫 갱신 때 -1로 초기화해 반드시 표시하도록 설정한다.
        displayedGold = -1;
        displayedScore = -1;
        displayedTurn = -1;
        displayedBallCount = -1;
        displayedAttackPower = -1;
    }

    private void LateUpdate()
    {
        if (displayedGold != goldManager.Gold)
        {
            displayedGold = goldManager.Gold;
            goldText.text = $"돈: {displayedGold}";
        }

        if (displayedScore != goldManager.Score)
        {
            displayedScore = goldManager.Score;
            scoreText.text = $"점수: {displayedScore}";
        }

        if (displayedTurn != turnManager.CurrentTurn)
        {
            displayedTurn = turnManager.CurrentTurn;
            turnText.text = $"라운드: {displayedTurn}";
        }

        if (displayedBallCount != inventory.BallCount)
        {
            displayedBallCount = inventory.BallCount;
            ballCountText.text = $"공: {displayedBallCount}개";
        }

        if (displayedAttackPower != inventory.BallAttackPower)
        {
            displayedAttackPower = inventory.BallAttackPower;
            attackPowerText.text = $"공격력: {displayedAttackPower}";
        }
    }

}
