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
    
}

//卡牌的可配置数据
[CreateAssetMenu(fileName = "CardData", menuName = "Scriptable Objects/CardData")]
public class CardData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("唯一ID，用来在代码里查找")]
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
    
    [Tooltip("是否有饱腹值")]
    public bool hasSaturation;
    
    [Tooltip("有饱腹的话是多少")]
    public int saturation;

    [Header("Class")]
    [Tooltip("卡牌所属的类别，见代码上面的enum")]
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
