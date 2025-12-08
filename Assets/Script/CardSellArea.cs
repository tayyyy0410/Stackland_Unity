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
        if (anyCardInStack == null) return;

        // 卖卡区域自己的碰撞体
        var col = GetComponent<Collider2D>();
        if (col == null) return;

        // 找到这一叠的 root
        Transform root = anyCardInStack.stackRoot != null
            ? anyCardInStack.stackRoot
            : anyCardInStack.transform;

        // 必须整叠放在卖卡区域上
        if (!col.OverlapPoint(root.position))
            return;

        Card rootCard = root.GetComponent<Card>();   // 可能为 null，仅用于后面给 coin 定 stackRoot

        // 找出这一叠里的所有卡
        Card[] cards = root.GetComponentsInChildren<Card>();

        List<Card> toDestroy = new List<Card>();
        int totalCoins = 0;

        foreach (var c in cards)
        {
            if (c == null || c.data == null) continue;

            // Coin 本身不在这里处理，直接跳过
            if (c.data.cardClass == CardClass.Coin)
                continue;

            // 所有非 Coin 卡都可以卖掉（包括 value == 0）
            toDestroy.Add(c);

            // 只有 value > 0 的卡才产生金币
            if (c.data.value > 0)
            {
                totalCoins += c.data.value;
            }
        }

        // 没有任何可以被卖掉的卡，直接返回
        if (toDestroy.Count == 0)
            return;

        // 先销毁被卖掉的卡
        foreach (var c in toDestroy)
        {
            if (c != null)
            {
                Destroy(c.gameObject);
            }
        }

        // 再根据总价值给金币（可能是 0）
        if (totalCoins > 0)
        {
            GiveCoins(totalCoins);
        }

        Debug.Log($"[SellArea] 卖卡完成：销毁了 {toDestroy.Count} 张卡，获得 {totalCoins} 个 coin。");
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
