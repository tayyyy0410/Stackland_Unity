using System.Collections.Generic;
using UnityEngine;

//卡牌商店的逻辑
public class PackShopArea : MonoBehaviour
{
    [Header("Shop Config")]
    [Tooltip("这个区域卖的是什么卡包")]
    public PackData packToSell;

    [Tooltip("对应卡包Prefab")]
    public GameObject packCardPrefab;
    
    [Tooltip("卡包生成位置的随机半径（在 packSpawnOffset 附近抖动）")]
    public float packSpawnRadius = 0.5f;


    [Tooltip("卡包生成位置相对于这个区域的偏移")]
    //这个可以改个随机或者别的
    public Vector2 packSpawnOffset = new Vector2(0f, 1f);

    [Header("找零")]
    public GameObject changeCoinPrefab;

    
    [Tooltip("找零 coin 生成位置相对于 Shop 的固定偏移")]
    public Vector2 changeSpawnOffset = new Vector2(1.5f, 0f);  

    private Collider2D col;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        if (col == null)
        {
            Debug.LogError("PackShopArea 需要一个 Collider2D（建议 BoxCollider2D，isTrigger = true）");
        }
    }

public void TryBuyFromStack(Card anyCardInStack)
{

    if (anyCardInStack == null)
    {
        Debug.Log("[ShopArea] anyCardInStack 为 null，无法买卡包");
        return;
    }

    if (anyCardInStack.stackRoot == null)
    {
        Debug.Log("[ShopArea] anyCardInStack.stackRoot 为 null，无法买卡包");
        return;
    }

    if (packToSell == null)
    {
        Debug.LogWarning("[ShopArea] packToSell 没有设置！请在 Inspector 里给这个 Shop 绑定一个 PackData。");
        return;
    }

    if (packCardPrefab == null)
    {
        Debug.LogWarning("[ShopArea] packCardPrefab 没有设置！请在 Inspector 里绑定一个 CardPack 的 prefab。");
        return;
    }

    if (anyCardInStack.data == null)
    {
        Debug.Log("[ShopArea] anyCardInStack.data 为 null");
        return;
    }

    Debug.Log($"[ShopArea] 传进来的卡是：{anyCardInStack.data.displayName}，cardClass = {anyCardInStack.data.cardClass}");
    Collider2D col = GetComponent<Collider2D>();
    if (col != null)
    {
        Vector2 pos = anyCardInStack.stackRoot.position;
        if (!col.OverlapPoint(pos))
        {
            Debug.Log("[ShopArea] stackRoot 没有在当前这个 Shop 的 Collider 里，返回");
            return;
        }
    }
    else
    {
        Debug.LogWarning("[ShopArea] 当前物体上没有 Collider2D，无法用 OverlapPoint 检测位置");
    }
    
    List<Card> coinCards = new List<Card>();
    Transform root = anyCardInStack.stackRoot;
    var allCards = root.GetComponentsInChildren<Card>();

    foreach (var c in allCards)
    {
        if (c == null || c.data == null) continue;
        if (c.data.cardClass == CardClass.Coin)
        {
            coinCards.Add(c);
        }
    }

    int coinCount = coinCards.Count;
    Debug.Log($"[ShopArea] 当前 stack 中 coin 张数 = {coinCount} / 需要价格 = {packToSell.price}");

    if (coinCount < packToSell.price)
    {
        Debug.Log("[ShopArea] 钱不够，不能买卡包。");
        return;
    }

    // 先算找零：这叠 coin 一共值多少 - 价格
    int change = coinCount - packToSell.price;

// 1）把这叠里的所有 coin 全部销毁（等于把钱全投进商店）
    for (int i = coinCards.Count - 1; i >= 0; i--)
    {
        Card coinCard = coinCards[i];
        if (coinCard == null) continue;

        Debug.Log("[ShopArea] 消耗一枚金币：" + coinCard.name);
        Destroy(coinCard.gameObject);
    }

// 2）如果需要找零，就在固定位置吐一叠 coin 出来
    if (change > 0)
    {
        Debug.Log($"[ShopArea] 找零 {change} 枚金币。");
        GiveChange(change);
    }


    //  生成卡包 
    //packSpawnOffset 附近一个圆形半径内随机
    Vector2 rand = Vector2.zero;
    if (packSpawnRadius > 0f)
    {
        rand = Random.insideUnitCircle * packSpawnRadius;
    }

    Vector3 packPos = transform.position 
                      + (Vector3)packSpawnOffset 
                      + new Vector3(rand.x, rand.y, 0f);

    
    GameObject packObj = Instantiate(packCardPrefab, packPos, Quaternion.identity);

    CardPack cp = packObj.GetComponent<CardPack>();
    if (cp != null)
    {
        cp.packData = packToSell;
    }
    else
    {
        Debug.LogWarning("[ShopArea] 生成的 packCardPrefab 上没有 CardPack 组件！");
    }
    
    
    //通知任务
    if (QuestManager.Instance != null)
    {
        QuestManager.Instance.NotifyPackBought(packToSell);
    }

    Debug.Log("[ShopArea] 成功购买卡包：" + packToSell.name);
}


    // 找零
    private void GiveChange(int change)
    {
        if (change <= 0) return;

        if (changeCoinPrefab == null)
        {
            Debug.LogWarning($"[ShopArea] 需要找零 {change}，但没有设置 changeCoinPrefab。");
            return;
        }

        //用固定偏移
        Vector3 basePos = transform.position + (Vector3)changeSpawnOffset;


        // 生成第一个 coin当作这一叠的 root
        GameObject rootObj = Instantiate(changeCoinPrefab, basePos, Quaternion.identity);
        Card rootCard = rootObj.GetComponent<Card>();

        if (rootCard == null)
        {
            Debug.LogWarning("[ShopArea] changeCoinPrefab 上没有 Card 组件，无法做成一叠，只能生成一个。");
            return;
        }

      
        rootCard.stackRoot = rootCard.transform;

        // 全部挂在 root 下面
        for (int i = 1; i < change; i++)
        {
            GameObject coinObj = Instantiate(changeCoinPrefab, basePos, Quaternion.identity);
            Card c = coinObj.GetComponent<Card>();
            if (c != null)
            {
                c.stackRoot = rootCard.transform;
                coinObj.transform.SetParent(rootCard.transform);
            }
        }
        
        rootCard.LayoutStack();

        Debug.Log($"[ShopArea] 找零完成：生成了一叠 {change} 个 coin。");
    }

    
}
