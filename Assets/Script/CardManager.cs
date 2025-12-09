using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// ���泡���е� Coin, Food, Villager
/// ���� Villager �� װ����״̬
/// </summary>

public class CardManager : MonoBehaviour
{
    public static CardManager Instance { get; private set; }

    public List<Card> AllCards { get; private set; } = new List<Card>();
    public List<Card> FoodCards { get; private set; } = new List<Card>();
    public List<Card> VillagerCards { get; private set; } = new List<Card>();
    
    
    public HashSet<CardData> discoveredIdeas = new HashSet<CardData>();

    


    // UIʵʱ����ͳ��
    [Header("Runtime Stats (For Debug)")]
    [Tooltip("ʵʱ���µĿ������� (debug only)")]
    [SerializeField] private int coinCount;
    [SerializeField] private int totalSaturation;
    [SerializeField] private int totalHunger;
    [SerializeField] private int maxCardCapacity;
    private int fixedMaxCapcity = 20;

    // UI: ���� Class ���õ�����
    public int MaxCardCapacity => maxCardCapacity;  // UI: ������������
    public int CoinCount => coinCount;  // UI������coin����
    public int TotalSaturation => totalSaturation;  // UI�����б���ֵ
    public int TotalHunger => totalHunger;  // UI����Ҫ�ı���ֵ

    public int NonCoinCount => AllCards.Count - CoinCount;  // UI�����г���coin�Ŀ�������

    public int CardToSellCount => NonCoinCount - MaxCardCapacity;   // UI�����������Ŀ�������

    
    
    
    //idea card////
    public bool HasDiscoveredIdea(CardData data)
    {
        return data != null && discoveredIdeas.Contains(data);
    }

    public void RegisterIdeaIfNeeded(Card card)
    {
        if (card == null || card.data == null) return;
        if (card.data.cardClass != CardClass.Idea) return;

        if (!discoveredIdeas.Contains(card.data))
        {
            discoveredIdeas.Add(card.data);
            Debug.Log($"[Idea] 解锁新 Idea：{card.data.displayName}");
        }
    }

    // ���� villager ��װ��״̬
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
    /// ���ɿ���ʱע��
    /// </summary>
    public void RegisterCard(Card card)
    {
        if (card == null) return;
        if (!card.IsOnBoard) return;    // ��ͳ��װ����Ŀ�

        var data = card.data;
        if (data == null)
        {
            Debug.LogWarning($"[Register] {card.name} û�� data");
            return;
        }

        if (card.data.cardClass == CardClass.Prefab) return;
        
        RegisterIdeaIfNeeded(card); //记住上场的idea

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
    /// ���ٿ���ʱɾ������
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

        // ���� villager ʱensureһ��װ����Ҳ�����
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

        // �����ۼ����� food �� currentSaturation
        foreach (var food in FoodCards)
        {
            if (food == null) continue;
            if (food.currentSaturation > 0)
            {
                totalSaturation += food.currentSaturation;
            }
        }

        // �����ۼ����� villager �� currentHunger
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
    // villager ���ϵ� equipment ��Ϣ
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

    // �� equipment �� owner
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
    /// ��קװ������ʱ���߼����
    /// return true: ������ɣ��������� stack �߼�
    /// return false: ������װ����������ԭ���� stack �߼�
    /// </summary>
    public bool TryHandleEquipmentDrop(Card sourceRootCard, Card targetCard)
    {
        if (sourceRootCard == null || sourceRootCard.data == null)
        {
            Debug.Log("[TryHandleEquipmentDrop]����ǰ��ֹ��sourceRootCard == null || sourceRootCard.data == null");
            return false;
        }
        if (!IsEquipment(sourceRootCard))
        {
            Debug.Log("[TryHandleEquipmentDrop]����ǰ��ֹ�� !IsEquipment(sourceRootCard)");
            return false;
        }
        if (targetCard == null || targetCard.data == null)
        {
            Debug.Log("[TryHandleEquipmentDrop]����ǰ��ֹ�� targetCard == null || targetCard.data == null");
            return false;
        }
        if (targetCard == sourceRootCard)
        {
            Debug.Log("[TryHandleEquipmentDrop]����ǰ��ֹ�� targetCard == sourceRootCard");
            return false;
        }

        Debug.Log($"[EquipDrop] targetCard = {targetCard.name}");

        // ��� target stack �� root �� ���ϲ����
        // ��Ҫ���������������ڵ� equipment�����ܴ���װ�����ڣ�

        // targetCard �����װ�����еĿ������ҵ� owner �� true target
        Card effectiveTarget = targetCard;

        if (targetCard.RuntimeState == CardRuntimeState.InEquipmentUI &&
            CardManager.Instance != null)
        {
            Card owner = CardManager.Instance.FindOwnerVillagerOfEquip(targetCard);
            if (owner != null)
            {
                effectiveTarget = owner;
                Debug.Log($"[EquipDrop] ����װ������ {targetCard.name}��ӳ�䵽 owner {owner.name}");
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

        Debug.Log($"[EquipDrop] �ѵ�Ŀ��topCard��{topCard.name}");

        // ������û���ٿ�root�Լ��ǲ��ǿ�
        // root�Լ��������� equipment
        /*if (topCard == null)
        {
            // BUG�����⵽װ������Ŀ�
            topCard = targetRoot.GetComponent<Card>();
        }

        if (topCard == null)
        {
            Debug.Log("[EquipDrop] topCard == null");
            return false;
        }*/

        Debug.Log($"[EquipDrop] �ѵ�Ŀ��topCard��{topCard.name}");

        // 1) ������ villager������װ������������
        if (topCard.data.cardClass == CardClass.Villager)
        {
            bool equipped = TryEquipStackToVillager(sourceRootCard, topCard);
            if (equipped)
            {
                Debug.Log($"[EquipDrop] {sourceRootCard.name} ���ڵ����� װ���� ����{topCard.name} ����");
            }
            return equipped;
        }

        // 2) װ����װ������stack������ͨstack�߼�
        // ��Ŀ��װ��������װ������
        // UI��Ҫȷ��װ���������⿨�Ƶļ�ⷶΧ����villager�ļ�ⷶΧ֮�ڣ�����޷�װ������ͼstackװ�����е�װ���������
        if (IsEquipment(topCard) && topCard.IsOnBoard)
        {

            // �����������ͨ stack ���� topCard ��һ����
            sourceRootCard.JoinStackOf(topCard);
            Debug.Log($"[EquipDrop] {sourceRootCard.name} ���ڵ����� �ѵ��� onboard װ����");

            return true;

        }

        // 3) �Ų���װ��stack��Ҳװ������villager���� -> ���ص� target �·�
        Transform sourceRootTf = sourceRootCard.stackRoot != null ? sourceRootCard.stackRoot : sourceRootCard.transform;

        Vector3 dropPos = targetRoot.position + new Vector3(0f, -1.5f, 0f);
        sourceRootTf.position = dropPos;

        // ����stack�� stackRoot
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
        Debug.Log($"EquipDrop] {sourceRootCard.name} ����װ��/�ѵ��� ���� {targetCard.name} �·�");
        return false;
    }

    /// <summary>
    /// ���԰�һ����װ�����ҵ���������
    /// </summary>
    public bool TryEquipStackToVillager(Card anyCardInStack, Card villagerCard)
    {
        if (anyCardInStack == null || villagerCard == null) return false;
        if (anyCardInStack.data == null ||  villagerCard.data == null) return false;
        if (villagerCard.data.cardClass != CardClass.Villager) return false;
        
        // 1) �ҵ�������root
        Transform srcRoot = anyCardInStack.stackRoot != null
            ? anyCardInStack.stackRoot
            : anyCardInStack.transform;

        // 2) �ռ�����װ����
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
            Debug.Log("[TryEquipStack] �������û�� Card��");
            return false;
        }

        // 3) ���װ���ͷ�װ��
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
            // �����ϲ�����֣�sourceRootCard һ����װ��������Ϊ�����ף�
            Debug.Log("[TryEquipStack] �����û��װ���������ظ���ͨ stack �߼�����");
            return false;
        }

        // 4) ȷ�� villager �ж�Ӧ��װ��״̬
        if (!villagerEquipStates.TryGetValue(villagerCard, out var state))
        {
            state = new VillagerEquipState();
            villagerEquipStates[villagerCard] = state;
        }

        // 5) ����װ������
        foreach (var equipCard in equipCards)
        {
            if (equipCard == null) continue;
            EquipSingleCardToVillagerSlot(equipCard, villagerCard);
        }
        Debug.Log($"[TryEquipStack] װ��{equipCards.Count} �ţ�װ���� {villagerCard.name}");

        // 6) ��װ�����֣�������ڣ����������һ�������� villager �·�
        if (nonEquipCards.Count > 0)
        {
            Transform villagerRoot = villagerCard.stackRoot != null
                ? villagerCard.stackRoot
                : villagerCard.transform;

            Vector3 dropPos = villagerRoot.position + new Vector3(0f, -1.5f, 0f);

            // ѡ��װ����ĵ�һ����Ϊ�� root
            Card newRootCard = nonEquipCards[0];
            Transform newRootTf = newRootCard.transform;

            // �Ȱ��� root ����ԭ stack
            newRootTf.SetParent(null);
            newRootCard.ResetToDefaultSize();
            newRootTf.position = dropPos;
            newRootCard.stackRoot = newRootTf;

            // �����װ�����ҵ���� newRootTf ��
            for (int i = 1; i < nonEquipCards.Count; i++)
            {
                Card c = nonEquipCards[i];
                if (c == null) continue;
                c.transform.SetParent(null);
                c.ResetToDefaultSize();

                c.transform.SetParent(newRootTf);
                c.stackRoot = newRootTf;
            }

            // 4.3 ��һ���µ���λ��
            newRootCard.LayoutStack();

            Debug.Log($"[TryEquipStack] ��װ�� {nonEquipCards.Count} �ţ����� {villagerCard.name} �·�");
        }

        // TODO: ����֮����Ը���װ�����´����ս������
        return true;
    }

    /// <summary>
    /// ��һ��װ��װ�� villager ����ĳ����λ
    /// TODO: Ŀǰ�ȼ�ʵ���� hand ��λ�������ٸ��� CardData �ֲ�λ
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
            Debug.LogWarning($"[Equip] {equipCard.name} �� equipSlot δ���ã��������װ��");
            return;
        }

        EquipmentUIController.Instance.OpenBigPanel(villagerCard);

        // �ֲ�λ
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

        // ������ͳ�����Ƴ������Ϊ InEquipmentUI
        equipCard.SetRuntimeState(CardRuntimeState.InEquipmentUI);

        // TODO: Ч����д�ҵ�villager������Сһ��, ��������ui
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

        // ����������û��װ��
        // �����ǲ��ǵ�һ��װ��ˢ�´�Сװ����
        //bool hasNow = VillagerHasAnyEquip(villagerCard);

        //  ���ǵ�һ��װ�������̴� ����� villager �Ĵ�װ������
        //if (!hadBefore && hasNow && EquipmentUIController.Instance != null)
        //{
        //}
        /*else if (hasNow && EquipmentUIController.Instance != null)
        {
            // ���ǵ�һ��װ��
            if (EquipmentUIController.Instance.IsBigPanelOpenFor(villagerCard))
            {
                EquipmentUIController.Instance.RebuildBigPanelContent(villagerCard);
            }
            else
            {
                EquipmentUIController.Instance.EnsureSmallBar(villagerCard);
            }
        }*/

        // ���� stackRoot
        // ����������������գ�����������ջ�drag����
        equipCard.stackRoot = equipCard.transform;
        equipCard.isTopVisual = false;

        // ��λ�о�װ�����Ѿ�װ���ӻس���
        // TODO: �ӻس��ϵ�λ�ú����ĳɷ�Χ�����
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

        Debug.Log($"[EquipSingle]{villagerCard.name} hand��λװ��{equipCard.name}(ԭװ��: {(oldEquip != null ? oldEquip.name : "��")})");
    }


    /// <summary>
    /// �ҵ����װ����ǰ�����ĸ� villager ���ϣ�û�оͷ��� null��
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
    /// ������װ������ԭ�������� villager ���Ͻ��ˢ��ԭ owner װ����UI
    /// ���ﲻ����װ������ oboard ״̬��������ע��
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

        // ֪ͨ UI ˢ����� villager ��װ����
        if (EquipmentUIController.Instance != null)
        {
            if (VillagerHasAnyEquip(owner))
            {
                if (EquipmentUIController.Instance.IsBigPanelOpenFor(owner))
                    EquipmentUIController.Instance.RebuildBigPanelContent(owner);
                else
                    EquipmentUIController.Instance.EnsureSmallBar(owner);
                Debug.Log($"[ClearEquipRelation] װ��{equipCard.name}��{owner.name}��󣬱���װ����");
            }

            else
            {
                EquipmentUIController.Instance.CloseBigPanel(owner);
                EquipmentUIController.Instance.CloseSmallBar(owner);
                Debug.Log($"[ClearEquipRelation] װ��{equipCard.name}��{owner.name}������װ����");
            }
        }

    }

    /// <summary>
    /// ����һ����������п����Ӹ��� villager ��װ��״̬�н��
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
    /// �ڿ�ʼ��קʱ���ã������װ���� villager ����ж�£���������һ����ͨ��
    /// ����ԭ owner װ��״̬��װ��UI + װ����UI/״̬/ע��
    /// </summary>
    public void UnequipFromVillagerImmediate(Card equipCard)
    {
        if (equipCard == null || equipCard.data == null) return;
        if (equipCard.data.cardClass != CardClass.Equipment) return;

        // ��װ����������
        ClearEquipRelation(equipCard);

        // ״̬/�Ӿ���ԭ
        equipCard.isTopVisual = true;
        equipCard.SetRuntimeState(CardRuntimeState.OnBoard);
        equipCard.ResetToDefaultSize();
        equipCard.gameObject.SetActive(true);
        equipCard.transform.SetParent(null);   // ���ٹ��� UI panel ����

        Debug.Log($"[Equip] UnequipFromVillagerImmediate: {equipCard.name} ��ж�£���ǰ��Ϊ��ͨ OnBoard ��");
    }




}
