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
    Animals,
    Villager,
    Idea,
    Equipment,
    Healing,
    Prefab,
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

    [Tooltip("是否有饱腹值")]
    public bool hasSaturation;
    
    [Tooltip("饱腹值")]
    public int saturation;

    [Tooltip("是否有饥饿值")]
    public bool hasHunger;

    [Tooltip("饥饿值")]
    public int hunger;
    
   
    /// <summary>
    /// ////战斗相关
    /// </summary>
    [Tooltip("是否有生命值")]
    public bool hasHP;

    [Tooltip("最大生命值")]
    public int baseHP;

    [Tooltip("是否有伤害")]
    public bool hasAttack;

    [Tooltip("伤害值")]
    public int attack;

    [Tooltip("是否有HitChance")]
    public bool hasHitChance;

    [Tooltip("HitChance值")]
    public int hitChance;
    
    [Header("Battle Loot")]
    [Tooltip("是否有战斗死亡掉落")]
    public bool hasDeathLoot = false;
    
    
    [Tooltip("死亡时使用的掉落池（PackData）")]
    public PackData deathLootPack;

    [Tooltip("每次死亡至少掉几张卡")]
    public int minDeathLoot = 1;

    [Tooltip("每次死亡最多掉几张卡")]
    public int maxDeathLoot = 1;

    
    [Header("weapons")]
    [Tooltip("是否增加伤害")]
    public bool hasDamage;

    [Tooltip("增加的伤害")]
    public int damage;

    [Tooltip("是否增加HP")]
    public bool hasIncreaseHP;

    [Tooltip("增加的HP")]
    public int IncreasedHP;
    
    [Header("Stats UI")]
    public bool showHP;        // 这张卡要不要显示 HP 数值
    public bool showHunger;    // 要不要显示饱腹
    public bool showValue;     // 要不要显示金币数值

    public bool useDarkText = true; // true = 用深色字；false = 用白字


    [Header("Med kits")]
    [Tooltip("可以增加治疗吗")]
    public bool hasHealing;

    [Tooltip("治疗值")]
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
    
    

    [Header("Harvest")]
    [Tooltip("是否是可多次采集的结构")]
    public bool isHarvestable = false;

    [Tooltip("最多可以被采集多少次")]
    public int maxHarvestUses = 1;

    [Tooltip("采集时用的掉落池")]
    public PackData harvestLootPack;

    [Tooltip("留空就直接消失")]
    public CardData depletedCardData;
        
}
