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

    [Tooltip("卡包生成位置相对于这个区域的偏移")]
    //这个可以改个随机或者别的
    public Vector2 packSpawnOffset = new Vector2(0f, 1f);

    [Header("找零")]
    public GameObject changeCoinPrefab;

    [Tooltip("找零的 coin 在shop附近随机一点偏移")]
    //这里也可以随机个别的
    public float changeSpawnRadius = 0.5f;

    private Collider2D col;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        if (col == null)
        {
            Debug.LogError("PackShopArea 需要一个 Collider2D（建议 BoxCollider2D，isTrigger = true）");
        }
    }

    //用某一叠 coin 来尝试购买
    public void TryBuyFromStack(Card anyCardInStack)
    {
        if (packToSell == null || packCardPrefab == null)
        {
            Debug.LogWarning("[ShopArea] packToSell 或 packCardPrefab 没设置，无法购买卡包。");
            return;
        }

        if (anyCardInStack == null || anyCardInStack.data == null)
            return;
        
        if (anyCardInStack.data.cardClass != CardClass.Coin)
            return;

        // 先确认这个 stack 的位置是在 shop 区域里
        if (col != null)
        {
            Vector2 pos = anyCardInStack.stackRoot.position;
            if (!col.OverlapPoint(pos))
            {
                return;
            }
        }

        // 找到这一stack的root
        Transform root = anyCardInStack.stackRoot;
        if (root == null) root = anyCardInStack.transform;
        
        List<Card> coinsInStack = new List<Card>();

        // root 自己
        Card rootCard = root.GetComponent<Card>();
        if (rootCard != null && rootCard.data != null && rootCard.data.cardClass == CardClass.Coin)
        {
            coinsInStack.Add(rootCard);
        }

        // 子物体
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Card c = child.GetComponent<Card>();
            if (c != null && c.data != null && c.data.cardClass == CardClass.Coin)
            {
                coinsInStack.Add(c);
            }
        }

        int coinCount = coinsInStack.Count;
        Debug.Log($"[ShopArea] 当前 stack 中 coin 张数 = {coinCount} / 需要价格 = {packToSell.price}");

        if (coinCount < packToSell.price)
            return; // 钱不够

        int change = coinCount - packToSell.price;

        // 生成卡包
        Vector3 packPos = transform.position + (Vector3)packSpawnOffset;
        GameObject packObj = Instantiate(packCardPrefab, packPos, Quaternion.identity);

        CardPack cp = packObj.GetComponent<CardPack>();
        if (cp != null)
        {
            cp.packData = packToSell;
        }

        // 销毁这叠里的 coin
        foreach (var coin in coinsInStack)
        {
            if (coin != null)
                Destroy(coin.gameObject);
        }

        // 生成找零
        if (change > 0)
        {
            GiveChange(change);
        }
    }

    /// 找零
    private void GiveChange(int change)
    {
        if (change <= 0) return;
        if (changeCoinPrefab == null)
        {
            Debug.LogWarning($"[ShopArea] 需要找零 {change}，但没有设置 changeCoinPrefab。");
            return;
        }

        for (int i = 0; i < change; i++)
        {
            Vector2 offset = Random.insideUnitCircle * changeSpawnRadius;
            Vector3 spawnPos = transform.position + new Vector3(offset.x, offset.y, 0f);

            GameObject coinObj = Instantiate(changeCoinPrefab, spawnPos, Quaternion.identity);
            Debug.Log("[ShopArea] 生成了一枚找零 coin：" + coinObj.name);
        }

        Debug.Log($"[ShopArea] 找零完成：生成 {change} 个找零coin。");
    }
    
}
