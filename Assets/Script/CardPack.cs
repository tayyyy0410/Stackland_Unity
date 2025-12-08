using UnityEngine;

public class CardPack : MonoBehaviour
{
    [Header("Pack Config")]
    [Tooltip("这个卡包对应的 PackData")]
    public PackData packData;
    
    public GameObject cardPrefab;

    [Header("开包槽位")]
    public Vector2[] slotOffsets = new Vector2[]
    {
        new Vector2(-1.2f,  0.6f),
        new Vector2( 0f,     0.6f),
        new Vector2( 1.2f,   0.6f),
        new Vector2(-1.2f, -0.1f),
        new Vector2( 0f,    -0.1f),
        new Vector2( 1.2f,  -0.1f),
        new Vector2( 0f,    -0.8f)
    };


    [Header(" Drag Settings")]
    [Tooltip("鼠标移动小于这个像素就当点击")]
    public float clickThreshold = 10f;

    [Tooltip("开始判定为拖动的屏幕距离")]
    public float dragStartThreshold = 5f;

    [Tooltip("拖动时的世界坐标敏感度，可以根据需要调整")]
    public float dragSensitivity = 1f;

    // 一次卡包一共能开几张
    public int remainingOpens = 0;
    private int totalOpens = 0;
    private bool initialized = false;

    // 用来记录每一张卡应该用哪个槽位
    private int[] slotOrder;

    // 点击/拖动判定
    private Vector3 mouseDownScreenPos;
    private bool isDragging = false;
    private Vector3 dragStartWorldPos;
    private Vector3 dragStartPackPos;

    private void EnsureInitialized()
    {
        if (initialized) return;

        if (packData == null)
        {
            Debug.LogWarning("CardPack 没有设置 packData，无法初始化剩余次数。");
            remainingOpens = 0;
            totalOpens = 0;
            initialized = true;
            return;
        }
        
        remainingOpens = Random.Range(packData.minCards, packData.maxCards + 1);
        if (remainingOpens < 1) remainingOpens = 1;

        totalOpens = remainingOpens;

        InitSlotOrder();

        initialized = true;
    }

    //决定每次开包的槽位
    private void InitSlotOrder()
    {
        if (slotOffsets == null || slotOffsets.Length == 0)
        {
            slotOrder = null;
            return;
        }

        int maxSlots = Mathf.Min(slotOffsets.Length, totalOpens);


        if (totalOpens <= maxSlots)
        {
            int[] indices = new int[maxSlots];
            for (int i = 0; i < maxSlots; i++)
                indices[i] = i;

            for (int i = 0; i < maxSlots; i++)
            {
                int r = Random.Range(i, maxSlots);
                int tmp = indices[i];
                indices[i] = indices[r];
                indices[r] = tmp;
            }

            slotOrder = new int[totalOpens];
            for (int i = 0; i < totalOpens; i++)
            {
                slotOrder[i] = indices[i];
            }
        }
        else
        {
            slotOrder = new int[totalOpens];

            int[] firstFour = new int[maxSlots];
            for (int i = 0; i < maxSlots; i++)
                firstFour[i] = i;

            for (int i = 0; i < maxSlots; i++)
            {
                int r = Random.Range(i, maxSlots);
                int tmp = firstFour[i];
                firstFour[i] = firstFour[r];
                firstFour[r] = tmp;
            }

            for (int i = 0; i < maxSlots && i < totalOpens; i++)
            {
                slotOrder[i] = firstFour[i];
            }

            for (int i = maxSlots; i < totalOpens; i++)
            {
                slotOrder[i] = i % maxSlots;
            }
        }
    }

    private void OnMouseDown()
    {
        EnsureInitialized();

        mouseDownScreenPos = Input.mousePosition;
        isDragging = false;

        // 记录世界坐标，用于拖动时计算偏移
        Vector3 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        world.z = 0f;

        dragStartWorldPos = world;
        dragStartPackPos = transform.position;
    }

    private void OnMouseDrag()
    {
        
        if (!isDragging)
        {
            float dist = Vector3.Distance(Input.mousePosition, mouseDownScreenPos);
            if (dist >= dragStartThreshold)
            {
                isDragging = true;
            }
            else
            {
                return;
            }
        }

        // 已经在拖动状态更新位置
        Vector3 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        world.z = 0f;

        Vector3 deltaWorld = world - dragStartWorldPos;
        Vector3 newPos = dragStartPackPos + deltaWorld * dragSensitivity;

        newPos.z = transform.position.z;
        transform.position = newPos;
    }

    private void OnMouseUp()
    {
        
        float screenDist = Vector3.Distance(Input.mousePosition, mouseDownScreenPos);

        if (!isDragging && screenDist <= clickThreshold)
        {
            HandleClick();
        }

        isDragging = false;
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

        if (remainingOpens <= 0)
        {
            return;
        }

        //播放开包音效
        if (AudioManager.I != null && AudioManager.I.packOpenSfx != null)
        {
            AudioManager.I.PlaySFX(AudioManager.I.packOpenSfx);
        }

        // 打开一次，生成一张卡
        SpawnOneCard();
        remainingOpens--;

        // 打完就销毁卡包
        if (remainingOpens <= 0)
        {
            Destroy(gameObject);
        }
    }

    // 生成一张卡使用预定的槽位
    private void SpawnOneCard()
    {
        // 已经开了几张 = 总数 - 剩余
        int openedCount = totalOpens - remainingOpens;

        CardData dropData = null;

        // ★ 如果这个 pack 配置了固定结果，就按 fixedResults 的顺序给
        if (packData != null && packData.useFixedResults &&
            packData.fixedResults != null && packData.fixedResults.Count > 0)
        {
            // 超出长度就 clamp 一下，避免越界（正常情况下 openedCount < fixedResults.Count）
            int idx = Mathf.Clamp(openedCount, 0, packData.fixedResults.Count - 1);
            dropData = packData.fixedResults[idx];
        }
        else
        {
            // 否则按原来的权重随机
            dropData = GetRandomCardFromPack();
        }

        if (dropData == null) return;

        Vector3 spawnPos = transform.position;

        if (slotOrder != null && slotOffsets != null && slotOffsets.Length > 0)
        {
            int idx = Mathf.Clamp(openedCount, 0, slotOrder.Length - 1);
            int slotIndex = slotOrder[idx];
            slotIndex = Mathf.Clamp(slotIndex, 0, slotOffsets.Length - 1);

            Vector2 offset2D = slotOffsets[slotIndex];
            spawnPos += new Vector3(offset2D.x, offset2D.y, 0f);
        }
        else
        {
            Vector2 offset = Random.insideUnitCircle * 0.5f;
            spawnPos += new Vector3(offset.x, offset.y, 0f);
        }

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

            // ★ 如果是 Idea 且已经解锁过，就不再计入权重
            if (entry.cardData.cardClass == CardClass.Idea &&
                CardManager.Instance != null &&
                CardManager.Instance.HasDiscoveredIdea(entry.cardData))
            {
                continue;
            }

            totalWeight += entry.weight;
        }

        if (totalWeight <= 0) return null;

        int rand = Random.Range(0, totalWeight);
        int cumulative = 0;

        foreach (var entry in packData.entries)
        {
            if (entry.cardData == null || entry.weight <= 0) continue;

            // 跳过已经解锁的 Idea
            if (entry.cardData.cardClass == CardClass.Idea &&
                CardManager.Instance != null &&
                CardManager.Instance.HasDiscoveredIdea(entry.cardData))
            {
                continue;
            }

            cumulative += entry.weight;
            if (rand < cumulative)
            {
                return entry.cardData;
            }
        }

        return null;
    }

}
