using UnityEngine;

public enum AugmentCategory
{
    Ball,
    Ability,
    Special
}

public enum AugmentRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}


public enum AugmentId
{
    //특수 공 8종
    NormalBall,
    ExplosionBall,
    ElectricBall,
    HorizontalLaserBall,
    VerticalLaserBall,
    CrossLaserBall,
    FireBall,
    ChargeBall,
    //특수 능력 4종
    FrontAttack,
    RearAttack,
    OddColumnAttack,
    EvenColumnAttack,
    //특수 증강 5종
    InterestUp,
    RarityUp,
    AttackUp,
    ExtraLife,
    DestructionGoldUp
}
//Unity의 Create 메뉴에서 AugmentData 정보를 저장하는 
[CreateAssetMenu(
    fileName = "Augment",
    menuName = "Brick Breaker/Augment Data"
    )]
//이름,설명,아이콘을 Inspector에서 설정할수 있게 ScriptableObject를 상속
public class AugmentData : ScriptableObject
{
    [SerializeField] private AugmentId id;
    [SerializeField] private AugmentCategory category;
    [SerializeField] private AugmentRarity rarity;

    [SerializeField] private string displayName;

    [TextArea(2, 5)]
    [SerializeField] private string description;

    [SerializeField] private Sprite icon;

    public AugmentId Id => id;
    public AugmentCategory Category => category;
    public AugmentRarity Rarity => rarity;

    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
}
