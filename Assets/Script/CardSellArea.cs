using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(Collider2D))]
public class CardSellArea : MonoBehaviour
{
    [Header("Sell Config")]
    public GameObject coinPrefab;

    [Tooltip("金币生成位置锚点（可选，不设则用 SellArea 位置 + 偏移）")]
    public Transform coinSpawnPoint;
    
    
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

            // 1) coin 不卖（原逻辑）
            if (c.data.cardClass == CardClass.Coin)
                continue;

            // 2) ✅ 这些类型不能卖：Villager / Enemy / Animals
            if (c.data.cardClass == CardClass.Villager ||
                c.data.cardClass == CardClass.Enemy ||
                c.data.cardClass == CardClass.Animals)
            {
                // 直接跳过，不加入 toDestroy，也不记钱
                continue;
            }

            // 3) 其它所有卡都可以卖掉（包括 value == 0）
            toDestroy.Add(c);

            // 只有 value > 0 的卡才产生金币
            if (c.data.value > 0)
            {
                totalCoins += c.data.value;
            }

            // 通知任务（只对真正被卖掉的卡触发）
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.NotifyCardSold(c.data);
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

        // ----------------------------
        // 计算生成位置
        // ----------------------------
        Vector3 basePos;

        // 使用锚点位置
        if (coinSpawnPoint != null)
        {
            basePos = coinSpawnPoint.position;
        }
        else
        {
            // 用 SellArea 位置 
            basePos = transform.position + (Vector3)coinSpawnOffset;
        }

        // ----------------------------
        // 生成首个 coin
        // ----------------------------
        GameObject rootObj = Instantiate(coinPrefab, basePos, Quaternion.identity);
        Card rootCard = rootObj.GetComponent<Card>();

        if (rootCard == null)
        {
            Debug.LogWarning("[SellArea] coinPrefab 上没有 Card 组件，只能生成 1 个 coin。");
            return;
        }

        // ROOT 自己做 stackRoot
        rootCard.stackRoot = rootCard.transform;

        // ----------------------------
        // 生成剩余 coin挂在 root 下面
        // ----------------------------
        for (int i = 1; i < count; i++)
        {
            GameObject coinObj = Instantiate(coinPrefab, basePos, Quaternion.identity);
            Card c = coinObj.GetComponent<Card>();
            if (c != null)
            {
                c.stackRoot = rootCard.transform;
                coinObj.transform.SetParent(rootCard.transform);
            }
        }

        
        rootCard.LayoutStack();
        
       

        Debug.Log($"[SellArea] GiveCoins：生成了一叠 {count} 个 coin。位置 = {basePos}");
    }
    
    // 
    

    
}
