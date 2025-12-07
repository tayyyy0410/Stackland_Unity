using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// 储存场景中的 Coin, Food, Villager
/// 储存 Villager 的 装备栏状态
/// </summary>

public class CardManager : MonoBehaviour
{
    public static CardManager Instance { get; private set; }

    public List<Card> AllCards { get; private set; } = new List<Card>();
    public List<Card> FoodCards { get; private set; } = new List<Card>();
    public List<Card> VillagerCards { get; private set; } = new List<Card>();

    // UI实时计数统计
    [Header("Runtime Stats (Debug)")]
    [Tooltip("实时更新的卡牌数据 (debug only)")]
    [SerializeField] private int coinCount;
    [SerializeField] private int totalSaturation;
    [SerializeField] private int totalHunger;
    [SerializeField] private int maxCardCapacity;
    private int fixedMaxCapcity = 20;

    // UI: 其他 Class 调用的数据
    public int MaxCardCapacity => maxCardCapacity;  // UI: 卡牌容量上限
    public int CoinCount => coinCount;  // UI：现有coin数量
    public int TotalSaturation => totalSaturation;  // UI：现有饱腹值
    public int TotalHunger => totalHunger;  // UI：需要的饱腹值

    public int NonCoinCount => AllCards.Count - CoinCount;  // UI：现有除了coin的卡牌数量

    public int CardToSellCount => NonCoinCount - MaxCardCapacity;   // UI：还需售卖的卡牌数量

    

    // 管理 villager 的装备状态
    public Dictionary<Card, VillagerEquipState> villagerEquipStates = new Dictionary<Card, VillagerEquipState>();

    public VillagerEquipState GetEquipState(Card villager)
    {
        if (villager == null) return null;
        if (villagerEquipStates.TryGetValue(villager, out var state))
        {
            return state;
        }
        return null;
    }

    public void CleanupEquipStateIfEmpty(Card villager)
    {
        if (villager == null) return;
        if (villagerEquipStates.TryGetValue(villager,out var state))
        {
            if (!state.HasAnyEquip)
            {
                villagerEquipStates.Remove(villager);
            }
        }
    }

    private bool IsEquipment(Card c)
    {
        return c != null &&
               c.data != null &&
               c.data.cardClass == CardClass.Equipment;
    }


    // ==========================================================================================
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

    }

    private void Start()
    {
        coinCount = 0;
        totalSaturation = 0;
        totalHunger = 0;
        maxCardCapacity = fixedMaxCapcity;

        Card[] cards = FindObjectsByType<Card>(FindObjectsSortMode.None);

        foreach (Card card in cards)
        {
            if (card)
                RegisterCard(card);
        }
    }


    // ===================================== Register Helpers =============================================
    /// <summary>
    /// 生成卡牌时注册
    /// </summary>
    public void RegisterCard(Card card)
    {
        if (card == null) return;
        if (!card.IsOnBoard) return;    // 不统计装备里的卡

        var data = card.data;
        if (data == null)
        {
            Debug.LogWarning($"[Register] {card.name} 没有 data");
            return;
        }

        if (card.data.cardClass == CardClass.Prefab) return;

        switch (data.cardClass)
        {
            case CardClass.Food:
                if (!FoodCards.Contains(card))
                {
                    FoodCards.Add(card);
                }
                break;

            case CardClass.Villager:
                if (!VillagerCards.Contains(card))
                {
                    VillagerCards.Add(card);
                }
                if (!villagerEquipStates.ContainsKey(card))
                {
                    villagerEquipStates[card] = new VillagerEquipState();
                }
                break;

            case CardClass.Coin:
                if (!card.HasRegisteredToManager)
                {
                    coinCount++;
                }
                break;

            case CardClass.Structure:
                if (!card.HasRegisteredToManager && card.data.hasCapacity)
                {
                    maxCardCapacity += card.data.capacity;
                }
                break;

            default: break;
        }

        if (!AllCards.Contains(card))
        {
            AllCards.Add(card);
            card.HasRegisteredToManager = true;
        }

        RecalculateTotals();
    }



    /// <summary>
    /// 销毁卡牌时删除数据
    /// </summary>
    public void UnregisterCard(Card card)
    {
        if (card == null) return;

        AllCards.Remove(card);
        FoodCards.Remove(card);
        VillagerCards.Remove(card);
        villagerEquipStates.Remove(card);

        var data = card.data;
        if (data != null && data.cardClass == CardClass.Coin)
        {
            coinCount = Mathf.Max(0, CoinCount - 1);
        }

        if (data != null && data.hasCapacity)
        {
            maxCardCapacity = Mathf.Max(0, MaxCardCapacity - data.capacity);
        }

        // 销毁 villager 时ensure一下装备栏也清掉了
        if (card.data != null && card.data.cardClass == CardClass.Villager)
        {
            if (EquipmentUIController.Instance != null)
            {
                EquipmentUIController.Instance.CloseSmallBar(card);
                EquipmentUIController.Instance.CloseBigPanel(card);
            }
        }

        RecalculateTotals();
    }

    public void ReduceSaturation(int delta)
    {
        totalSaturation -= delta;
    }

    public void RecalculateTotals()
    {
        totalSaturation = 0;
        totalHunger = 0;

        // 重新累加所有 food 的 currentSaturation
        foreach (var food in FoodCards)
        {
            if (food == null) continue;
            if (food.currentSaturation > 0)
            {
                totalSaturation += food.currentSaturation;
            }
        }

        // 重新累加所有 villager 的 currentHunger
        foreach (var v in VillagerCards)
        {
            if (v == null) continue;
            if (v.currentHunger > 0)
            {
                totalHunger += v.currentHunger;
            }
        }

        Debug.Log($"[CardManager]AllCards={AllCards.Count}, Villager={VillagerCards.Count}, Coin={CoinCount}, NonCoin={NonCoinCount}");
    }


    // ===================================== Equipment =============================================
    // villager 身上的 equipment 信息
    [System.Serializable]
    public class VillagerEquipState
    {
        public Card head;
        public Card hand;
        public Card body;

        public bool HasAnyEquip => head != null ||
                                   hand != null ||
                                   body != null;
    }

    public bool VillagerHasAnyEquip(Card villager)
    {
        if (villager == null) return false;
        if (!villagerEquipStates.TryGetValue(villager, out var state)) return false;
        return state != null && state.HasAnyEquip;
    }

    // 找 equipment 的 owner
    public Card FindOwnerVillagerOfEquip(Card equipCard)
    {
        if (equipCard == null) return null;

        foreach (var kvp in villagerEquipStates)
        {
            Card villager = kvp.Key;
            VillagerEquipState state = kvp.Value;
            if (state == null) continue;

            if (state.head == equipCard ||
                state.hand == equipCard ||
                state.body == equipCard)
            {
                return villager;
            }
        }

        return null;
    }


    /// <summary>
    /// 拖拽装备松手时的逻辑入口
    /// return true: 处理完成，跳过常规 stack 逻辑
    /// return false: 不属于装备场景，走原来的 stack 逻辑
    /// </summary>
    public bool TryHandleEquipmentDrop(Card sourceRootCard, Card targetCard)
    {
        if (sourceRootCard == null || sourceRootCard.data == null)
        {
            Debug.Log("[TryHandleEquipmentDrop]被提前中止：sourceRootCard == null || sourceRootCard.data == null");
            return false;
        }
        if (!IsEquipment(sourceRootCard))
        {
            Debug.Log("[TryHandleEquipmentDrop]被提前中止： !IsEquipment(sourceRootCard)");
            return false;
        }
        if (targetCard == null || targetCard.data == null)
        {
            Debug.Log("[TryHandleEquipmentDrop]被提前中止： targetCard == null || targetCard.data == null");
            return false;
        }
        if (targetCard == sourceRootCard)
        {
            Debug.Log("[TryHandleEquipmentDrop]被提前中止： targetCard == sourceRootCard");
            return false;
        }

        Debug.Log($"[EquipDrop] targetCard = {targetCard.name}");

        // 算出 target stack 的 root 和 最上层的牌
        // 且要跳过卡牌子物体内的 equipment（可能处于装备栏内）

        // targetCard 如果是装备栏中的卡，先找到 owner 改 true target
        Card effectiveTarget = targetCard;

        if (targetCard.RuntimeState == CardRuntimeState.InEquipmentUI &&
            CardManager.Instance != null)
        {
            Card owner = CardManager.Instance.FindOwnerVillagerOfEquip(targetCard);
            if (owner != null)
            {
                effectiveTarget = owner;
                Debug.Log($"[EquipDrop] 命中装备栏卡 {targetCard.name}，映射到 owner {owner.name}");
            }
        }

        targetCard = effectiveTarget;
        Transform targetRoot = targetCard.stackRoot != null ? targetCard.stackRoot : targetCard.transform;

        Card topCard = null;
        Card[] allCards = targetRoot.GetComponentsInChildren<Card>();

        foreach (var c in allCards)
        {
            if (c == null || c.data == null) continue;

            if (c.isTopVisual)
            {
                topCard = c;
                break;
            }
        }

        if (topCard == null)
        {
            Debug.Log("[EquipDrop] topCard == null");
            return false;
        }

        Debug.Log($"[EquipDrop] 堆叠目标topCard：{topCard.name}");

        // 子物体没有再看root自己是不是卡
        // root自己不用跳过 equipment
        /*if (topCard == null)
        {
            // BUG：会检测到装备栏里的卡
            topCard = targetRoot.GetComponent<Card>();
        }

        if (topCard == null)
        {
            Debug.Log("[EquipDrop] topCard == null");
            return false;
        }*/

        Debug.Log($"[EquipDrop] 堆叠目标topCard：{topCard.name}");

        // 1) 如果最顶是 villager，尝试装备到村民身上
        if (topCard.data.cardClass == CardClass.Villager)
        {
            bool equipped = TryEquipStackToVillager(sourceRootCard, topCard);
            if (equipped)
            {
                Debug.Log($"[EquipDrop] {sourceRootCard.name} 所在的整叠 装备到 村民{topCard.name} 身上");
            }
            return equipped;
        }

        // 2) 装备跟装备可以stack，走普通stack逻辑
        // 且目标装备不能在装备栏内
        // UI需要确保装备栏内任意卡牌的检测范围都在villager的检测范围之内（针对无法装备，试图stack装备栏中的装备卡情况）
        if (IsEquipment(topCard) && topCard.IsOnBoard)
        {

            // 把这叠当作普通 stack 叠在 topCard 那一叠上
            sourceRootCard.JoinStackOf(topCard);
            Debug.Log($"[EquipDrop] {sourceRootCard.name} 所在的整叠 堆叠到 onboard 装备上");

            return true;

        }

        // 3) 放不进装备stack，也装备不到villager身上 -> 返回到 target 下方
        Transform sourceRootTf = sourceRootCard.stackRoot != null ? sourceRootCard.stackRoot : sourceRootCard.transform;

        Vector3 dropPos = targetRoot.position + new Vector3(0f, -1.5f, 0f);
        sourceRootTf.position = dropPos;

        // 更新stack的 stackRoot
        sourceRootCard.stackRoot = sourceRootTf;
        foreach (Transform child in sourceRootTf)
        {
            Card c = child.GetComponent<Card>();
            if (c != null)
            {
                c.stackRoot = sourceRootTf;
            }
        }
        sourceRootCard.LayoutStack();

        ClearEquipRelation(sourceRootCard);
        Debug.Log($"EquipDrop] {sourceRootCard.name} 不能装备/堆叠， 掉在 {targetCard.name} 下方");
        return false;
    }

    /// <summary>
    /// 尝试把一整叠装备卡挂到村民身上
    /// </summary>
    public bool TryEquipStackToVillager(Card anyCardInStack, Card villagerCard)
    {
        if (anyCardInStack == null || villagerCard == null) return false;
        if (anyCardInStack.data == null ||  villagerCard.data == null) return false;
        if (villagerCard.data.cardClass != CardClass.Villager) return false;
        
        // 1) 找到整叠的root
        Transform srcRoot = anyCardInStack.stackRoot != null
            ? anyCardInStack.stackRoot
            : anyCardInStack.transform;

        // 2) 收集整叠装备卡
        var allCards = new List<Card>();
        Card rootCard = srcRoot.GetComponent<Card>();
        
        if (rootCard != null)
        {
            allCards.Add(rootCard);
        }
        foreach (Transform child in srcRoot)
        {
            Card c = child.GetComponent<Card>();
            if (c != null)
            {
                allCards.Add(c);
            }
        }

        if (allCards.Count == 0)
        {
            Debug.Log("[TryEquipStack] 这叠里面没有 Card！");
            return false;
        }

        // 3) 拆分装备和非装备
        var equipCards = new List<Card>();
        var nonEquipCards = new List<Card>();
        foreach (var c in allCards)
        {
            if (c == null || c.data == null) continue;

            if (IsEquipment(c))
            {
                equipCards.Add(c);
            }
            else
            {
                nonEquipCards.Add(c);
            }
        }
        if (equipCards.Count == 0)
        {
            // 理论上不会出现（sourceRootCard 一定是装备），但为了稳妥：
            Debug.Log("[TryEquipStack] 这叠里没有装备卡，交回给普通 stack 逻辑处理");
            return false;
        }

        // 4) 确保 villager 有对应的装备状态
        if (!villagerEquipStates.TryGetValue(villagerCard, out var state))
        {
            state = new VillagerEquipState();
            villagerEquipStates[villagerCard] = state;
        }

        // 5) 处理装备卡堆
        foreach (var equipCard in equipCards)
        {
            if (equipCard == null) continue;
            EquipSingleCardToVillagerSlot(equipCard, villagerCard);
        }
        Debug.Log($"[TryEquipStack] 装备{equipCards.Count} 张，装备到 {villagerCard.name}");

        // 6) 非装备部分：如果存在，把它们组成一叠，掉在 villager 下方
        if (nonEquipCards.Count > 0)
        {
            Transform villagerRoot = villagerCard.stackRoot != null
                ? villagerCard.stackRoot
                : villagerCard.transform;

            Vector3 dropPos = villagerRoot.position + new Vector3(0f, -1.5f, 0f);

            // 选非装备里的第一个作为新 root
            Card newRootCard = nonEquipCards[0];
            Transform newRootTf = newRootCard.transform;

            // 先把新 root 脱离原 stack
            newRootTf.SetParent(null);
            newRootCard.ResetToDefaultSize();
            newRootTf.position = dropPos;
            newRootCard.stackRoot = newRootTf;

            // 其余非装备卡挂到这个 newRootTf 下
            for (int i = 1; i < nonEquipCards.Count; i++)
            {
                Card c = nonEquipCards[i];
                if (c == null) continue;
                c.transform.SetParent(null);
                c.ResetToDefaultSize();

                c.transform.SetParent(newRootTf);
                c.stackRoot = newRootTf;
            }

            // 4.3 排一下新叠的位置
            newRootCard.LayoutStack();

            Debug.Log($"[TryEquipStack] 非装备 {nonEquipCards.Count} 张，掉在 {villagerCard.name} 下方");
        }

        // TODO: 这里之后可以根据装备更新村民的战斗属性
        return true;
    }

    /// <summary>
    /// 把一件装备装到 villager 身上某个槽位
    /// TODO: 目前先简单实现塞 hand 槽位，后续再根据 CardData 分槽位
    /// </summary>
    private void EquipSingleCardToVillagerSlot(Card equipCard, Card villagerCard)
    {
        if (equipCard == null || villagerCard == null) return;
        if (equipCard.data == null) return;
        if (equipCard.data.cardClass != CardClass.Equipment) return;

        ClearEquipRelation(equipCard);

        var state = GetEquipState(villagerCard);
        if (state == null) return;

        EquipSlotType slot = equipCard.data.equipSlot;
        if (slot == EquipSlotType.None)
        {
            Debug.LogWarning($"[Equip] {equipCard.name} 的 equipSlot 未设置，忽略这次装备");
            return;
        }

        EquipmentUIController.Instance.OpenBigPanel(villagerCard);

        // 分槽位
        Card oldEquip = null;
        switch (slot)
        {
            case EquipSlotType.Head:
                oldEquip = state.head;
                state.head = equipCard;
                break;
            case EquipSlotType.Hand:
                oldEquip = state.hand;
                state.hand = equipCard;
                break;
            case EquipSlotType.Body:
                oldEquip = state.body;
                state.body = equipCard;
                break;
        }

        // 从世界统计中移除，标记为 InEquipmentUI
        equipCard.SetRuntimeState(CardRuntimeState.InEquipmentUI);

        // TODO: 效果先写挂到villager底下缩小一点, 后续更改ui
        /*var villagerSR = villagerCard.GetComponent<SpriteRenderer>();
        var equipSR = equipCard.GetComponent<SpriteRenderer>();
        equipSR.sortingOrder = villagerSR.sortingOrder + 100;

        equipCard.transform.SetParent(villagerCard.transform);
        equipCard.transform.localScale = Vector3.one * 0.7f;

        switch (slot)
        {
            case EquipSlotType.Head:
                equipCard.transform.localPosition = new Vector3(-1f, -2f, 0f);
                break;
            case EquipSlotType.Hand:
                equipCard.transform.localPosition = new Vector3(0f, -2f, 0f);
                break;
            case EquipSlotType.Body:
                equipCard.transform.localPosition = new Vector3(1f, -2f, 0f);
                break;
        }*/

        // 更新现在有没有装备
        // 根据是不是第一次装备刷新大小装备栏
        //bool hasNow = VillagerHasAnyEquip(villagerCard);

        //  这是第一件装备，立刻打开 “这个 villager 的大装备栏”
        //if (!hadBefore && hasNow && EquipmentUIController.Instance != null)
        //{
        //}
        /*else if (hasNow && EquipmentUIController.Instance != null)
        {
            // 不是第一次装备
            if (EquipmentUIController.Instance.IsBigPanelOpenFor(villagerCard))
            {
                EquipmentUIController.Instance.RebuildBigPanelContent(villagerCard);
            }
            else
            {
                EquipmentUIController.Instance.EnsureSmallBar(villagerCard);
            }
        }*/

        // 更新 stackRoot
        // ！！！！！不能清空！！！！！清空会drag不了
        equipCard.stackRoot = equipCard.transform;
        equipCard.isTopVisual = false;

        // 槽位有旧装备，把旧装备扔回场上
        // TODO: 扔回场上的位置后续改成范围内随机
        if (oldEquip != null && oldEquip != equipCard)
        {
            oldEquip.SetRuntimeState(CardRuntimeState.OnBoard);
            oldEquip.transform.SetParent(null);

            Vector3 dropPos = villagerCard.transform.position + new Vector3(0f, -1.5f, 0f);
            oldEquip.transform.position = dropPos;
            oldEquip.transform.localScale = oldEquip.defaultScale;
            oldEquip.stackRoot = oldEquip.transform;
        }

        EquipmentUIController.Instance.RebuildBigPanelContent(villagerCard);

        Debug.Log($"[EquipSingle]{villagerCard.name} hand槽位装备{equipCard.name}(原装备: {(oldEquip != null ? oldEquip.name : "无")})");
    }


    /// <summary>
    /// 找到这件装备当前挂在哪个 villager 身上（没有就返回 null）
    /// </summary>
    private Card FindEquipOwner(Card equipCard)
    {
        if (equipCard == null) return null;

        foreach (var kvp in villagerEquipStates)
        {
            var villager = kvp.Key;
            var state = kvp.Value;
            if (state == null) continue;

            if (state.head == equipCard ||
                state.hand == equipCard ||
                state.body == equipCard)
            {
                return villager;
            }
        }
        return null;
    }

    /// <summary>
    /// 把这张装备从它原本所属的 villager 身上解绑，刷新原 owner 装备栏UI
    /// 这里不操作装备卡的 oboard 状态，不处理注册
    /// </summary>
    private void ClearEquipRelation(Card equipCard)
    {
        if (equipCard == null) return;

        Card owner = null;
        VillagerEquipState ownerState = null;

        foreach (var kvp in villagerEquipStates)
        {
            var v = kvp.Key;
            var state = kvp.Value;
            if (state == null) continue;

            if (state.head == equipCard ||
                state.hand == equipCard ||
                state.body == equipCard)
            {
                owner = v;
                ownerState = state;
                break;
            }
        }

        if (owner == null || ownerState == null) return;

        if (ownerState.head == equipCard) ownerState.head = null;
        if (ownerState.hand == equipCard) ownerState.hand = null;
        if (ownerState.body == equipCard) ownerState.body = null;

        CleanupEquipStateIfEmpty(owner);

        // 通知 UI 刷新这个 villager 的装备栏
        if (EquipmentUIController.Instance != null)
        {
            if (VillagerHasAnyEquip(owner))
            {
                if (EquipmentUIController.Instance.IsBigPanelOpenFor(owner))
                    EquipmentUIController.Instance.RebuildBigPanelContent(owner);
                else
                    EquipmentUIController.Instance.EnsureSmallBar(owner);
                Debug.Log($"[ClearEquipRelation] 装备{equipCard.name}从{owner.name}解绑，保留装备栏");
            }

            else
            {
                EquipmentUIController.Instance.CloseBigPanel(owner);
                EquipmentUIController.Instance.CloseSmallBar(owner);
                Debug.Log($"[ClearEquipRelation] 装备{equipCard.name}从{owner.name}解绑，清除装备栏");
            }
        }

    }

    /// <summary>
    /// 把这一整叠里的所有卡都从各自 villager 的装备状态中解绑
    /// </summary>
    private void ClearEquipRelationForStack(Card stackRootCard)
    {
        if (stackRootCard == null) return;
        var root = stackRootCard.stackRoot != null
                 ? stackRootCard.stackRoot
                 : stackRootCard.transform;

        var cards = root.GetComponentsInChildren<Card>();
        if (cards == null || cards.Length == 0) return;

        foreach (var c in cards)
        {
            ClearEquipRelation(c);
        }
    }


    /// <summary>
    /// 在开始拖拽时调用：把这件装备从 villager 身上卸下，变成手里的一张普通卡
    /// 处理原 owner 装备状态和装备UI + 装备卡UI/状态/注册
    /// </summary>
    public void UnequipFromVillagerImmediate(Card equipCard)
    {
        if (equipCard == null || equipCard.data == null) return;
        if (equipCard.data.cardClass != CardClass.Equipment) return;

        // 从装备数据里解绑
        ClearEquipRelation(equipCard);

        // 状态/视觉还原
        equipCard.isTopVisual = true;
        equipCard.SetRuntimeState(CardRuntimeState.OnBoard);
        equipCard.ResetToDefaultSize();
        equipCard.gameObject.SetActive(true);
        equipCard.transform.SetParent(null);   // 不再挂在 UI panel 下面

        Debug.Log($"[Equip] UnequipFromVillagerImmediate: {equipCard.name} 已卸下，当前视为普通 OnBoard 卡");
    }




}
