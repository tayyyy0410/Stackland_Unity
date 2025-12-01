using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(Collider2D))]
public class CardSellArea : MonoBehaviour
{
    [Header("Sell Config")]
    public GameObject coinPrefab;

    [Tooltip("找零的 coin 在卖卡区域附近随机一点偏移")]
    public float coinSpawnRadius = 0.5f;
    
    
    [Tooltip("金币生成位置相对卖卡区域的偏移）")]
    public Vector2 coinSpawnOffset = new Vector2(0f, -1f);

    private Collider2D col;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        if (col == null)
        {
           Debug.Log("none" );
        }
    }

    /// 用某一叠卡来尝试卖出换钱
    public void TrySellFromStack(Card anyCardInStack)
    {
        if (coinPrefab == null)
        {
            Debug.LogWarning("[SellArea] 没有设置 coinPrefab，无法卖卡换钱。");
            return;
        }

        if (anyCardInStack == null || anyCardInStack.data == null)
            return;

        // 检查 stack 的 root 位置是否在卖卡区域内
        Transform root = anyCardInStack.stackRoot != null ? anyCardInStack.stackRoot : anyCardInStack.transform;
        Vector2 pos = root.position;
        if (!col.OverlapPoint(pos))
        {
            
            return;
        }

        // 收集这一叠里所有可以卖的卡
        List<Card> cardsToSell = new List<Card>();
        int totalCoins = 0;

        // root 自己
        Card rootCard = root.GetComponent<Card>();
        if (rootCard != null && rootCard.data != null)
        {
            if (rootCard.data.cardClass != CardClass.Coin && rootCard.data.value > 0)
            {
                cardsToSell.Add(rootCard);
                totalCoins += rootCard.data.value;
            }
        }

        // 子物体
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Card c = child.GetComponent<Card>();
            if (c != null && c.data != null)
            {
                if (c.data.cardClass != CardClass.Coin && c.data.value > 0)
                {
                    cardsToSell.Add(c);
                    totalCoins += c.data.value;
                }
            }
        }

        Debug.Log($"[SellArea] 这叠卡可以卖出 {totalCoins} 个 coin，对应卡数量 = {cardsToSell.Count}");

        // 如果没有任何可以卖的卡直接返回
        if (totalCoins <= 0 || cardsToSell.Count == 0)
            return;

        // 销毁被卖掉的卡
        foreach (var card in cardsToSell)
        {
            if (card != null)
                Destroy(card.gameObject);
        }

        // 生成等额的 coin
        GiveCoins(totalCoins);
    }
    
    /// 生成指定数量的 coinPrefab
    private void GiveCoins(int count)
    {
        if (count <= 0) return;

        if (coinPrefab == null)
        {
            Debug.LogWarning($"[SellArea] 需要生成 {count} 个 coin，但没有设置 coinPrefab。");
            return;
        }

        // 生成第一个 coin，当作这一叠的 root
        Vector3 basePos = (Vector3)(coinSpawnOffset)+transform.position;
        GameObject rootObj = Instantiate(coinPrefab, basePos, Quaternion.identity);
        Card rootCard = rootObj.GetComponent<Card>();

        if (rootCard == null)
        {
            Debug.LogWarning("[SellArea] coinPrefab 上没有 Card 组件，无法做成一叠，只能生成一个。");
            return;
        }

        // 确保它自己是 stackRoot
        rootCard.stackRoot = rootCard.transform;

        // 生成剩下的 coin全部挂在 root 下面
        for (int i = 1; i < count; i++)
        {
            GameObject coinObj = Instantiate(coinPrefab, basePos, Quaternion.identity);
            Card c = coinObj.GetComponent<Card>();
            if (c != null)
            {
                // 设置它们的 stackRoot 都是 root
                c.stackRoot = rootCard.transform;
                coinObj.transform.SetParent(rootCard.transform);
            }
        }
        
        rootCard.LayoutStack();

        Debug.Log($"[SellArea] 卖卡完成：生成了一叠 {count} 个 coin。");
    }

    
}
