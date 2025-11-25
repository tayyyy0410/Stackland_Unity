using UnityEngine;


public class CardPack : MonoBehaviour
{
    [Header("Pack Config")]
    [Tooltip("这个卡包对应的 PackData")]
    public PackData packData;
    
    public GameObject cardPrefab;

    [Tooltip("卡牌偏移")]
    public float spawnRadius = 0.5f;

    [Header("Click Settings")]
    public float clickThreshold = 10f;  // 鼠标移动小于这个像素就当点击

    // 一次卡包一共能开几张
    private int remainingOpens = 0;
    private bool initialized = false;

 
    private Vector3 mouseDownScreenPos;

 

    // 初始化当前这包一共能开多少张（
    private void EnsureInitialized()
    {
        if (initialized) return;

        if (packData == null)
        {
            Debug.LogWarning("CardPack 没有设置 packData，无法初始化剩余次数。");
            remainingOpens = 0;
            initialized = true;
            return;
        }
        
        remainingOpens = Random.Range(packData.minCards, packData.maxCards + 1);
        if (remainingOpens < 1) remainingOpens = 1;

        initialized = true;
    }

    private void OnMouseDown()
    {
        mouseDownScreenPos = Input.mousePosition;
    }

    private void OnMouseUp()
    {
        Vector3 mouseUpScreenPos = Input.mousePosition;
        float distance = Vector3.Distance(mouseDownScreenPos, mouseUpScreenPos);
        
        if (distance <= clickThreshold)
        {
            HandleClick();
        }
    }


    private void HandleClick()
    {
        if (packData == null)
        {
            Debug.LogWarning("CardPack 没有 packData，无法打开卡包。");
            return;
        }

        if (cardPrefab == null)
        {
            Debug.LogWarning("CardPack 没有设置 cardPrefab，无法生成卡牌。");
            return;
        }

        EnsureInitialized();

        if (remainingOpens <= 0)
        {
            return;
        }

        // 打开一次，生成一张卡
        SpawnOneCard();
        remainingOpens--;

        // 销毁卡包
        if (remainingOpens <= 0)
        {
            Destroy(gameObject);
        }
    }

    // 生成一张卡
    private void SpawnOneCard()
    {
        CardData dropData = GetRandomCardFromPack();
        if (dropData == null) return;

        // 偏移位置
        Vector2 offset = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPos = transform.position + new Vector3(offset.x, offset.y, 0f);

        GameObject newCardObj = Instantiate(cardPrefab, spawnPos, Quaternion.identity);
        Card newCard = newCardObj.GetComponent<Card>();
        if (newCard != null)
        {
            newCard.data = dropData;
            newCard.stackRoot = newCard.transform;
            newCard.ApplyData();
            newCard.LayoutStack();
        }
    }

    /// 按权重随机抽一张卡
    private CardData GetRandomCardFromPack()
    {
        if (packData == null || packData.entries == null || packData.entries.Count == 0)
            return null;

        int totalWeight = 0;
        foreach (var entry in packData.entries)
        {
            if (entry.cardData == null || entry.weight <= 0) continue;
            totalWeight += entry.weight;
        }

        if (totalWeight <= 0) return null;

        int rand = Random.Range(0, totalWeight);
        int cumulative = 0;

        foreach (var entry in packData.entries)
        {
            if (entry.cardData == null || entry.weight <= 0) continue;

            cumulative += entry.weight;
            if (rand < cumulative)
            {
                return entry.cardData;
            }
        }

        return null;
    }
}
