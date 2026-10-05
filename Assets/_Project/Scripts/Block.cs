using UnityEngine;
using UnityEngine.VFX;
using TMPro;

//블록의 체력을 관리하고, 피해를 받았을 때 파괴 여부를 알려주는 역할
public class Block : MonoBehaviour
{
    [SerializeField] private int maxHealth = 10;
    [SerializeField] private TMP_Text healthText;

    //최대체력,현재체력,파괴여부
    public int MaxHealth => maxHealth;
    public int CurrentHealth{ get; private set; }
    public bool IsDestroyed { get; private set; }

    //단단함,골드 속성
    public bool IsHard { get; private set; }
    public bool IsGold { get; private set; }


    private void Awake()
    {
        Initialize(maxHealth);
    }

    //초기 상태 설정
    public void Initialize(int baseHealth, bool isHard=false, bool isGold=false)
    {
        IsHard = isHard;
        IsGold = isGold;

        int health = Mathf.Max(1, baseHealth);

        maxHealth = IsHard ? health * 3 : health;
        CurrentHealth = maxHealth;
        IsDestroyed = false;

        UpdateHealthText();
    }

    //피해 처리
    public bool TakeDamage(int damage)
    {
        if (IsDestroyed)
            return true;
        if (damage <= 0)
            return false;

        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);

        UpdateHealthText();

        if (CurrentHealth > 0)
            return false;

        IsDestroyed = true;

        foreach(Collider2D blockCollider in GetComponentsInChildren<Collider2D>())
        {
            blockCollider.enabled = false;
        }

        Destroy(gameObject);
        return true;
    }

    //블록의 체력표시 업데이트
    private void UpdateHealthText()
    {
        if (healthText == null)
            return;

        healthText.text = CurrentHealth.ToString();
    }

    //테스트
    [ContextMenu("Test Damage 10")]
    private void TestDamage()
    {
        if (!Application.isPlaying)
            return;

        bool destroyed = TakeDamage(10);

        Debug.Log(
            $"남은 체력: {CurrentHealth}, 파괴 여부: {destroyed}",
            this
        );
    }
}
