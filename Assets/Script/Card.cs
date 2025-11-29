using UnityEngine;

//这个代码是接入CardData.cs 用来改变卡的数据和外观；目前的stack逻辑也写在这里

public class Card : MonoBehaviour
{
    [Header("Config")]
    public CardData data;        // 这张场上instance引用哪张 CardData
    private SpriteRenderer sr;

    [Header("Stacking")] 
    public Transform stackRoot; //一个stack的root
    public float yOffset = -0.5f; // 往下偏移
    
    [Header("Harvest Runtime")]
    [HideInInspector] public int harvestUsesLeft = -1;

    [Header("Feeding Runtime")]
    public int currentSaturation = -1;  //food剩余的饱腹值，卡牌ui显示这个
    public int currentHunger = 0;   //villager的饥饿值

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        if (data != null)
        {
            ApplyData();
        }
        else
        {
            Debug.LogWarning($"{name} 没有设置 CardData！");
        }
        
        if (stackRoot == null)
        {
            stackRoot = transform;
        }
    }
    
    public void EnsureHarvestInit()
    {
        if (data == null) return;
        if (!data.isHarvestable) return;

        if (harvestUsesLeft < 0)
        {
            harvestUsesLeft = Mathf.Max(1, data.maxHarvestUses);
        }
    }

    public void FoodInit()
    {
        if (data.cardClass == CardClass.Food && data.hasSaturation && data.saturation > 0)
        {
            currentSaturation = data.saturation;
        }
        else currentSaturation = -1;    //不是食物没有饱腹值
    }

    public void HungerInit()
    {
        if (data.cardClass == CardClass.Villager)
        {
            currentHunger = data.hunger;
        }
        else currentHunger = -1;    //不是villager没有饥饿值
    }


    public void ApplyData()
    {
        // 替换Sprite
        if (data.backgroundSprite != null)
        {
            sr.sprite = data.backgroundSprite;
        }
        
        harvestUsesLeft = -1;
        EnsureHarvestInit();
        FoodInit();
        HungerInit();
    }
    

    /// 把自己这一叠叠到 target 的那一叠上
    public void JoinStackOf(Card target)
    {
        if (target == null) return;
        
        // stack的root
        Transform sourceRoot = stackRoot != null ? stackRoot : transform;
        // 目标stack的root
        Transform targetRoot = target.stackRoot != null ? target.stackRoot : target.transform;

        // 自己已经在对方这个stack里了不用处理
        if (sourceRoot == targetRoot) return;


        System.Collections.Generic.List<Transform> cardsToMove = new System.Collections.Generic.List<Transform>();
        cardsToMove.Add(sourceRoot);
        for (int i = 0; i < sourceRoot.childCount; i++)
        {
            cardsToMove.Add(sourceRoot.GetChild(i));
        }

        // 把整叠所有卡都挂到 targetRoot 下，形成一个大stack
        foreach (Transform t in cardsToMove)
        {
            t.SetParent(targetRoot);               
            Card c = t.GetComponent<Card>();
            if (c != null)
            {
                c.stackRoot = targetRoot;          // 更新每张卡的 stackRoot 
            }
        }

        // 合并之后对新的 target stack 排
        Card targetRootCard = targetRoot.GetComponent<Card>();
        if (targetRootCard != null)
        {
            targetRootCard.LayoutStack();
        }
    }



    /// stack的layout
    public void LayoutStack()
    {
        if (stackRoot == null) return;

        yOffset = -0.5f; 

        int i = 0;
        foreach (Transform child in stackRoot)
        {
            i++;

            child.localPosition = new Vector3(0f, i * yOffset, 0f);
        }
    }
}
