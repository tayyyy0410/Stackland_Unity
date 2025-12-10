using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static EquipManager;

public class EquipManager : MonoBehaviour
{
    public static EquipManager Instance { get; private set; }

    [Header("Win Condition")]
    [Tooltip("判定胜利用的喷气背包 CardData")]
    public CardData jetpackData;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

    }
    // ============================================================================================


    [System.Serializable]
    public class VillagerEquipState
    {
        public Card head;
        public Card hand;
        public Card body;

        public bool HasAnyEquip => head != null ||
                                   hand != null ||
                                   body != null;

        public IEnumerable<Card> EnumerateEquips()
        {
            if (head != null) yield return head;
            if (hand != null) yield return hand;
            if (body != null) yield return body;
        }
    }


    public Dictionary<Card, VillagerEquipState> allEquipStates = new Dictionary<Card, VillagerEquipState> ();


    /// <summary>
    /// villager 有没有至少一件装备
    /// </summary>

    public bool VillagerHasAnyEquip(Card v)
    {
        if (v == null) return false;
        if (!allEquipStates.TryGetValue(v, out var state)) return false;
        bool hasEquip = state.head != null ||
                        state.hand != null ||
                        state.body != null;

        return state != null && hasEquip;
    }


    /// <summary>
    /// 从 dictionary 获取村民的装备信息
    /// </summary>

    public VillagerEquipState GetEquipState(Card v)
    {
        if (v == null) return null;
        if (allEquipStates.TryGetValue(v, out var state))
        {
            return state;
        }
        return null;
    }

    /// <summary>
    /// 判断村民装备是否为空，为空则从 dictionary 中删除
    /// </summary>
    public void CleanupEquipStateIfEmpty(Card villager)
    {
        if (villager == null) return;
        if (allEquipStates.TryGetValue(villager, out var state))
        {
            if (!state.HasAnyEquip)
            {
                allEquipStates.Remove(villager);
            }
        }
    }

    /// <summary>
    /// 装备的 card, cardData 是否存在且 CardClass 为装备
    /// </summary>
    private bool IsEquipment(Card c)
    {
        return c != null &&
               c.data != null &&
               c.data.cardClass == CardClass.Equipment;
    }


    /// <summary>
    /// 查找 equipCard 的 ownerCard
    /// </summary>
    public Card FindOwnerVillagerOfEquip(Card equipCard)
    {
        if (equipCard == null) return null;

        foreach (var kvp in allEquipStates)
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
    /// 把装备从村民 EquipState 中删除，并根据删除后的装备状态刷新装备栏UI
    /// 不处理卸下的 equipCard 状态，只处理 relation 和 owner装备栏
    /// </summary>
    private void ClearEquipRelation(Card equipCard)
    {
        if (equipCard == null) return;

        Card owner = FindOwnerVillagerOfEquip(equipCard);
        if (owner == null) return;

        VillagerEquipState ownerState = GetEquipState(owner);
        if (ownerState == null) return;

        if (ownerState.head == equipCard) ownerState.head = null;
        if (ownerState.hand == equipCard) ownerState.hand = null;
        if (ownerState.body == equipCard) ownerState.body = null;

        CleanupEquipStateIfEmpty(owner);

        // CleanEquipUI
        if (EquipmentUIController.Instance != null)
        {
            if (VillagerHasAnyEquip(owner))
            {
                if (EquipmentUIController.Instance.IsBigPanelOpenFor(owner))
                {
                    EquipmentUIController.Instance.RebuildBigPanelContent(owner);
                }
                else
                {
                    EquipmentUIController.Instance.EnsureSmallBar(owner);
                }

                EquipmentUIController.Instance.RefreshBarColor(owner);
            }

            else
            {
                EquipmentUIController.Instance.CloseBigPanel(owner);
                EquipmentUIController.Instance.CloseSmallBar(owner);
            }

            CalculateEquipStats(owner);
            Debug.Log($"[ClearEquipRelation] 装备 {equipCard.name} 从 {owner.name} 解绑");
        }

    }


    /// <summary>
    /// 卸下装备，完全处理和owner的关系，以及卡牌自身的显示和状态
    /// </summary>
    public void UnequipFromVillagerImmediate(Card equipCard)
    {
        if (equipCard == null || equipCard.data == null) return;
        if (equipCard.data.cardClass != CardClass.Equipment) return;

        // 卸下卡牌，更新UI
        ClearEquipRelation(equipCard);

        // 卸下的装备卡回到 onBoard，处理状态
        equipCard.isTopVisual = true;
        equipCard.SetRuntimeState(CardRuntimeState.OnBoard);
        equipCard.ResetToDefaultSize();
        equipCard.transform.localRotation = Quaternion.Euler(0, 0, 0);
        equipCard.gameObject.SetActive(true);
        equipCard.transform.SetParent(null);   // ���ٹ��� UI panel ����

        Debug.Log($"[UnequipImmediate] 装备卡 {equipCard.name} 从装备栏卸下，回到正常 onboard 状态");
    }





    /// <summary>
    /// 在 trystack 之前调用，尝试装备，装备成功则跳过常规stack后续逻辑
    /// </summary>
    public bool TryHandleEquipmentDrop(Card sourceRootCard, Card targetCard)
    {
        if (sourceRootCard == null || sourceRootCard.data == null)
        {
            Debug.Log("[TryHandleEquipmentDrop]试图装备被提前终止：sourceRootCard == null || sourceRootCard.data == null");
            return false;
        }
        if (!IsEquipment(sourceRootCard))
        {
            Debug.Log("[TryHandleEquipmentDrop]试图装备被提前终止：!IsEquipment(sourceRootCard)");
            return false;
        }
        if (targetCard == null || targetCard.data == null)
        {
            Debug.Log("[TryHandleEquipmentDrop]试图装备被提前终止：targetCard == null || targetCard.data == null");
            return false;
        }
        if (targetCard == sourceRootCard)
        {
            Debug.Log("[TryHandleEquipmentDrop]试图装备被提前终止：targetCard == sourceRootCard");
            return false;
        }

        Debug.Log($"[TryHandleEquipmentDrop] targetCard = {targetCard.name}");

        // 如果 targetCard 是装备栏中的卡，映射到 owner
        Card effectiveTarget = targetCard;

        if (targetCard.RuntimeState == CardRuntimeState.InEquipmentUI)
        {
            Card owner = FindOwnerVillagerOfEquip(targetCard);
            if (owner != null)
            {
                effectiveTarget = owner;
                Debug.Log($"[EquipDrop] 抓取到装备卡 {targetCard.name}，映射到到 owner {owner.name}");
            }
        }

        Debug.Log($"[EquipDrop] 装备目标：{targetCard.name}");

        // 1) targetCard 为村民，进行装备
        if (effectiveTarget.data.cardClass == CardClass.Villager)
        {
            bool equipped = TryEquipStackToVillager(sourceRootCard, effectiveTarget);
            if (equipped)
            {
                Debug.Log($"[EquipDrop] {sourceRootCard.name} 所在的整叠被装备到 {effectiveTarget.name} 身上");
                CalculateEquipStats(effectiveTarget);
            }
            return equipped;
        }

        // 2) 装备卡只能堆叠在装备卡上
        if (IsEquipment(effectiveTarget) && effectiveTarget.IsOnBoard)
        {

            // 走常规stack逻辑，但后续不触发售卖和合成检测
            sourceRootCard.JoinStackOf(effectiveTarget);
            Debug.Log($"[EquipDrop] {sourceRootCard.name} 被堆叠到装备卡上");

            return true;

        }

        // 3) target是其他种类的卡牌，装备卡被回退
        Transform sourceRootTf = sourceRootCard.stackRoot != null ? sourceRootCard.stackRoot : sourceRootCard.transform;

        Vector3 dropPos = effectiveTarget.GetComponent<Transform>().position + new Vector3(0f, -1.5f, 0f);
        sourceRootTf.position = dropPos;

        // 刷新stackroot
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
        Debug.Log($"[EquipDrop] {sourceRootCard.name} 无法装备被退回，掉落在 {targetCard.name} 下方");
        return false;
    }

    /// <summary>
    /// 尝试装备装备卡所在的整个stack到vilalger
    /// </summary>
    public bool TryEquipStackToVillager(Card anyCardInStack, Card villagerCard)
    {
        if (anyCardInStack == null || villagerCard == null) return false;
        if (anyCardInStack.data == null || villagerCard.data == null) return false;
        if (villagerCard.data.cardClass != CardClass.Villager) return false;

        // 1) 获取stackroot
        Transform srcRoot = anyCardInStack.stackRoot != null
            ? anyCardInStack.stackRoot
            : anyCardInStack.transform;

        // 2) 获取stack中的所有卡牌
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
            Debug.Log("[TryEquipStack] 当前stack中没有卡牌，无法装备");
            return false;
        }

        // 3) 区分equip和非equip
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
            Debug.Log("[TryEquipStack] 当前stack中没有装备卡，装备取消");
            return false;
        }

        // 4) 确保 villager 字典中的装备状态
        if (!allEquipStates.TryGetValue(villagerCard, out var state))
        {
            state = new VillagerEquipState();
            allEquipStates[villagerCard] = state;
        }

        // 5) 一张一张装备
        foreach (var equipCard in equipCards)
        {
            if (equipCard == null) continue;
            EquipSingleCardToVillagerSlot(equipCard, villagerCard);
        }
        Debug.Log($"[TryEquipStack] 装备卡 {equipCards.Count} 张，装备到 {villagerCard.name}");

        //播放声音
        if (AudioManager.I != null && AudioManager.I.stackSfx != null)
        {
            AudioManager.I.PlaySFX(AudioManager.I.stackSfx);
        }

        // 6) 退回非装备卡，掉落到 villager 下方
        if (nonEquipCards.Count > 0)
        {
            Transform villagerRoot = villagerCard.stackRoot != null
                ? villagerCard.stackRoot
                : villagerCard.transform;

            Vector3 dropPos = villagerRoot.position + new Vector3(0f, -1.5f, 0f);

            // 设置新 stackRoot
            Card newRootCard = nonEquipCards[0];
            Transform newRootTf = newRootCard.transform;

            // 处理 stackRoot 的位置和显示
            newRootTf.SetParent(null);
            newRootCard.ResetToDefaultSize();
            newRootTf.position = dropPos;
            newRootCard.stackRoot = newRootTf;

            for (int i = 1; i < nonEquipCards.Count; i++)
            {
                Card c = nonEquipCards[i];
                if (c == null) continue;
                c.transform.SetParent(null);
                c.ResetToDefaultSize();

                c.transform.SetParent(newRootTf);
                c.stackRoot = newRootTf;
            }

            newRootCard.LayoutStack();

            Debug.Log($"[TryEquipStack] 非装备卡 {nonEquipCards.Count} 张，掉落到 {villagerCard.name} 下方");
        }
        return true;
    }

    /// <summary>
    /// 把一张装备卡装备给 villager
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
            Debug.LogWarning($"[EquipSingle] {equipCard.name} 没有设置 equipSlot，无法装备");
            return;
        }

        EquipmentUIController.Instance.OpenBigPanel(villagerCard);

        // 按 slot 装备
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

        // 把装备卡的状态设为 InEquipmentUI
        equipCard.SetRuntimeState(CardRuntimeState.InEquipmentUI);

        // 更新 stackRoot
        // 不能清空！清空会导致drag不了
        equipCard.stackRoot = equipCard.transform;
        equipCard.isTopVisual = false;

        // 处理被替换的装备
        if (oldEquip != null && oldEquip != equipCard)
        {
            oldEquip.SetRuntimeState(CardRuntimeState.OnBoard);
            oldEquip.transform.SetParent(null);

            Vector3 dropPos = villagerCard.transform.position + new Vector3(0f, -1.5f, 0f);
            oldEquip.transform.position = dropPos;
            oldEquip.transform.localScale = oldEquip.defaultScale;
            oldEquip.transform.localRotation = Quaternion.Euler(0, 0, 0);
            oldEquip.stackRoot = oldEquip.transform;
        }

        EquipmentUIController.Instance.RebuildBigPanelContent(villagerCard);
        EquipmentUIController.Instance.RefreshBarColor(villagerCard);

        if (QuestManager.Instance != null && equipCard.data != null)
        {
            QuestManager.Instance.NotifyItemEquipped(equipCard.data);
        }


        Debug.Log($"[EquipSingle] 成功装备{equipCard.name} 到 {villagerCard.name}，原装备 {(oldEquip != null ? oldEquip.name : "无")}");

        if (AreAllVillagersEquippedWithJetpack())
        {
            Debug.Log("[Win] 所有村民都穿上了 Jetpack，游戏胜利！");
        }

    }


    /// <summary>
    /// 返回一张卡牌的装备加成[bonusAttack, bonusHP]
    /// 并重新计算村民的 currentHP 和 currentAttack
    /// </summary>
    public int[] CalculateEquipStats(Card villager)
    {
        int[] results = { 0, 0 };
        if (villager == null || villager.data == null) return results;

        CardData vd = villager.data;

        int bonusHP = 0;
        int bonusAttack = 0;

        if (villager.data.cardClass != CardClass.Villager)
        {
            Debug.Log($"[CalculateEquipStats] {villager.name}: 当前装备加成：bonusAttack={bonusAttack}, bonusHP={bonusHP}\n" +
                  $"当前状态: currentAttack={villager.currentAttack}, currentOwnHP={villager.currentOwnHP}, currentHP={villager.currentHP}");
            return results;
        }

        if (allEquipStates != null &&
            allEquipStates.TryGetValue(villager, out var state) &&
            state != null)
        {
            foreach (var equipCard in state.EnumerateEquips())
            {
                if (equipCard == null || equipCard.data == null) continue;

                CardData ed = equipCard.data;

                if (ed.hasDamage)
                {
                    bonusAttack += ed.damage;
                }

                if (ed.hasIncreaseHP)
                {
                    bonusHP += ed.IncreasedHP;
                }
            }
        }

        villager.currentHP = villager.currentOwnHP + bonusHP;
        villager.currentAttack = villager.data.attack + bonusAttack;

        results[0] = bonusAttack;
        results[1] = bonusHP;

        Debug.Log($"[CalculateEquipStats] {villager.name}: 当前装备加成：bonusAttack={bonusAttack}, bonusHP={bonusHP}\n" +
                  $"当前状态: currentAttack={villager.currentAttack}, currentOwnHP={villager.currentOwnHP}, currentHP={villager.currentHP}");

        return results;

    }


    // win condition
    public bool HasJetpackOnBody(Card villager)
    {
        if (villager == null || jetpackData == null) return false;

        // 从字典里拿这名村民的装备状态
        if (!allEquipStates.TryGetValue(villager, out var state))
            return false;

        if (state == null || state.body == null)
            return false;

        // body 上那张装备卡的 data 是否等于 jetpackData
        var bodyEquip = state.body;
        if (bodyEquip.data == null) return false;

        return bodyEquip.data == jetpackData;
    }


    public bool AreAllVillagersEquippedWithJetpack()
    {
        if (CardManager.Instance == null) return false;

        bool hasAtLeastOneVillager = false;

        foreach (var v in CardManager.Instance.VillagerCards)
        {
            if (v == null || v.data == null) continue;

            if (v.data.cardClass != CardClass.Villager)
                continue;

            hasAtLeastOneVillager = true;

            // 只要有一个村民 body 槽不是 jetpack，就还没达成胜利条件
            if (!HasJetpackOnBody(v))
            {
                return false;
            }
        }

        // 没有村民也不算“全员有喷气背包”
        if (!hasAtLeastOneVillager) return false;

        // 跑完没 false，说明所有村民都满足
        return true;
    }





}
