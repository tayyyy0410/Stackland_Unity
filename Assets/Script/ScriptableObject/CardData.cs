using UnityEngine;



//卡牌的种类
public enum CardClass
{
    None,
    Food,
    Coin,
    Resource,
    Structure,
    Enemy,
    Villager,
    Idea,
    Equipment,
    Healing,
}

//卡牌的可配置数据
[CreateAssetMenu(fileName = "CardData", menuName = "Scriptable Objects/CardData")]
public class CardData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("唯一ID")]
    public string id;

    [Tooltip("物品的UI名字")]
    public string displayName;

    [TextArea]
    [Tooltip("卡牌的Description")]
    public string description;

    [Header("Stats")]
    [Tooltip("卡牌的价值")]
    public int value;

    [Tooltip("是否有生命值")]
    public bool hasHP;

    [Tooltip("有HP的话的最大生命值")]
    public int maxHP;

    [Tooltip("是否有伤害")]
    public bool hasAttack;

    [Tooltip("有伤害的话的伤害值")]
    public int attack;

    [Tooltip("是否有HitChance")]
    public bool hasHitChance;

    [Tooltip("有HitChance的话的HitChance值")]
    public float HitChance;

    [Tooltip("是否有饱腹值")]
    public bool hasSaturation;
    
    [Tooltip("有饱腹的话是多少")]
    public int saturation;

    [Tooltip("是否能被采集")]
    public bool canBeFarmed;

    [Tooltip("可以被采集多少次")]
    public int cardAmmount;

    [Tooltip("是否增加伤害")]
    public bool hasDamage;

    [Tooltip("增加的伤害")]
    public int damage;

    [Tooltip("是否增加HP")]
    public bool hasIncreaseHP;

    [Tooltip("增加的HP")]
    public int IncreasedHP;

    [Tooltip("是否增加HP")]
    public bool hasHealing;

    [Tooltip("增加的HP")]
    public int healing;

    [Header("Class")]
    [Tooltip("卡牌所属的类别，看代码上面的enum")]
    public CardClass cardClass;
    
    [Header("Visual")]
    [Tooltip("卡牌的visual")]
    public Sprite backgroundSprite;

    [Header("Stacking")]
    [Tooltip("这张牌最多能叠几张")]
    public int maxStackSize = 1;

    [Tooltip("是否允许和同class的牌叠在一起")]
    public bool canStackWithSameClass = true;
        
}
