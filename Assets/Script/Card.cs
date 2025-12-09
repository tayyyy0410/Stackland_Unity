using NUnit.Framework.Interfaces;
using Unity.VisualScripting;
using UnityEngine;
using System.Collections.Generic;

// 卡牌当前状态，用于管理装备
public enum CardRuntimeState
{
    OnBoard,        // 在场/stack里，被记入total count
    InEquipmentUI,  // 在装备栏，不算进total count
}

//这个代码是接入CardData.cs 用来改变卡的数据和外观；目前的stack逻辑也写在这里
public class Card : MonoBehaviour
{
    [Header("Config")]
    public CardData data;        // 这张场上instance引用哪张 CardData
    private SpriteRenderer sr;

    [Header("Stacking")]
    public Transform stackRoot;  // 一个stack的root
    public float yOffset = -0.5f; // 往下偏移

    [Header("Harvest Runtime")]
    [HideInInspector] public int harvestUsesLeft = -1;

    [Header("Feeding Runtime")]
    public int currentSaturation = -1;  // food 剩余的饱腹值，卡牌ui显示这个
    public int currentHunger = 0;       // villager 的饥饿值
    public bool HasMovedDuringFeed { get; set; } = false;    // food 是否在 feeding 过程中被抓取过

    [Header("UI Display")]
    private InfoBarIndep infoBar;
    private CardStatsUI statsUI;
    public bool isTopVisual = true;


    // Battle 相关
    [Header("Battle Runtime")]
    [Tooltip("当前 HP")]
    public int currentHP;

    [Tooltip("是否已经初始化过 HP")]
    public bool hasInitHP = false;

    [HideInInspector] public BattleManager.BattleInstance currentBattle;
    public bool IsInBattle => currentBattle != null;


    // Equipment 相关
    [Header("Runtime State")]
    [Tooltip("当前卡牌是否处于装备栏")]
    [SerializeField] private CardRuntimeState runtimeState = CardRuntimeState.OnBoard;
    public CardRuntimeState RuntimeState => runtimeState;
    public bool IsOnBoard => runtimeState == CardRuntimeState.OnBoard;
    public Vector3 defaultScale;


    // CardManager 相关
    public bool HasRegisteredToManager { get; set; } = false;





    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        defaultScale = transform.localScale;

        if (data != null)
        {
            ApplyData();
        }
        else
        {
            Debug.LogWarning($"{name} 没有设置 CardData！");
        }

        if (stackRoot == null)
        {
            stackRoot = transform;
        }

        GameObject infoBarObj = GameObject.FindWithTag("UI-Infobar");
        if (infoBarObj != null)
        {
            infoBar = infoBarObj.GetComponent<InfoBarIndep>();
        }

        statsUI = GetComponent<CardStatsUI>();
    }

    private void OnEnable()
    {
        TryRegisterToManager();
    }

    private void OnDisable()
    {
        if (CardManager.Instance != null && HasRegisteredToManager)
        {
            CardManager.Instance.UnregisterCard(this);
        }
    }

    public void EnsureHarvestInit()
    {
        if (data == null) return;
        if (!data.isHarvestable) return;

        if (harvestUsesLeft < 0)
        {
            harvestUsesLeft = Mathf.Max(1, data.maxHarvestUses);
        }
    }

    public void FoodInit()
    {
        if (data.cardClass == CardClass.Food && data.hasSaturation && data.saturation > 0)
        {
            currentSaturation = data.saturation;
        }
        else
        {
            currentSaturation = -1;    // 不是食物没有饱腹值
        }
    }

    public void HungerInit()
    {
        if (data.cardClass == CardClass.Villager)
        {
            sr = GetComponent<SpriteRenderer>();
            sr.sortingOrder = -100;
            currentHunger = data.hunger;
        }
        else
        {
            currentHunger = -1;    // 不是 villager 没有饥饿值
        }
    }

    public void ApplyData()
    {
        // 替换 Sprite
        if (data != null && data.backgroundSprite != null)
        {
            sr.sprite = data.backgroundSprite;
        }

        harvestUsesLeft = -1;
        HasMovedDuringFeed = false;
        EnsureHarvestInit();
        FoodInit();
        HungerInit();

        if (statsUI != null)
        {
            statsUI.ForceRefreshOnDataChanged();
        }

        // 确保有 data 的牌会被 CardManager 统计
        TryRegisterToManager();
    }

    /// 把自己这一叠叠到 target 的那一叠上
    public void JoinStackOf(Card target)
    {
        if (target == null) return;

        Transform sourceRoot = stackRoot != null ? stackRoot : transform;
        Transform targetRoot = target.stackRoot != null ? target.stackRoot : target.transform;

        if (sourceRoot == targetRoot) return;

        // 收集这一叠里所有 Card（包括 root 自己）
        List<Card> cardsToMove = new List<Card>();

        Card rootCard = sourceRoot.GetComponent<Card>();
        if (rootCard != null)
        {
            cardsToMove.Add(rootCard);
        }

        for (int i = 0; i < sourceRoot.childCount; i++)
        {
            Transform child = sourceRoot.GetChild(i);
            Card childCard = child.GetComponent<Card>();
            if (childCard != null)
            {
                cardsToMove.Add(childCard);
            }
        }

        // 只移动 Card，对应的 StatsRoot 会跟着自己的 Card 一起走（因为是子物体）
        foreach (Card c in cardsToMove)
        {
            Transform t = c.transform;
            t.SetParent(targetRoot);
            c.stackRoot = targetRoot;
        }

        Card targetRootCard = targetRoot.GetComponent<Card>();
        if (targetRootCard != null)
        {
            targetRootCard.LayoutStack();
        }
    }


    public void LayoutStack()
    {
        if (stackRoot == null) return;

        // 包括 root 的整个 stack
        Card[] cardsInStack = stackRoot.GetComponentsInChildren<Card>();

        // 1. 先把这个 stack 里的所有 Card 的 isTopVisual 清零
        foreach (var c in cardsInStack)
        {
            if (c != null)
            {
                c.isTopVisual = false;
            }
        }

        int i = 0;
        Card lastCard = null;

        // 2. 只移动真正的 Card 子物体（跳过 StatsRoot 等 UI）
        foreach (Transform child in stackRoot)
        {
            Card childCard = child.GetComponent<Card>();
            if (childCard == null) continue;

            i++;
            child.localPosition = new Vector3(0f, i * yOffset, 0f);
            lastCard = childCard;

        }

        // 3. 标记最上面那张卡
        Card topCard = null;

        if (lastCard != null)
        {
            topCard = lastCard;
        }
        else
        {
            topCard = stackRoot.GetComponent<Card>();
        }

        if (topCard != null)
        {
            topCard.isTopVisual = true;
        }

        // ============ 4. 处理村民装备栏显示逻辑 ==========
        if (EquipmentUIController.Instance != null)
        {
            List<Card> villagersInStack = new List<Card>();

            foreach (var c in cardsInStack)
            {
                if (c == null || c.data == null) continue;

                if (c.data.cardClass == CardClass.Villager)
                {
                    villagersInStack.Add(c);
                }
            }

            foreach (Card v in villagersInStack)
            {
                bool villagerIsTop = (v == topCard);

                if (!villagerIsTop)
                {
                    // 不是顶牌要关掉大面板 + 隐藏小条
                    EquipmentUIController.Instance.CloseBigPanel(v);
                    EquipmentUIController.Instance.SetSmallBarVisible(v, false);
                }
                else if (EquipManager.Instance != null && EquipManager.Instance.VillagerHasAnyEquip(v))
                {
                    // 是顶牌且有装备：小条显示
                    EquipmentUIController.Instance.SetSmallBarVisible(v, true);
                }

            }
        }
    }


    // ================= LayoutStack 我改动比较大，怕出现ui问题把原版存在这了
    /// stack 的 layout
    /*public void LayoutStack()
    {
        if (stackRoot == null) return;

        // 1. 先把这个 stack 里的所有 Card 的 isTopVisual 清零
        foreach (Transform t in stackRoot)
        {
            Card c = t.GetComponent<Card>();
            if (c != null)
            {
                c.isTopVisual = false;

            }
        }

        int i = 0;
        Card lastCard = null;

        // 2. 只移动真正的 Card 子物体（跳过 StatsRoot 等 UI）
        foreach (Transform child in stackRoot)
        {
            Card childCard = child.GetComponent<Card>();
            if (childCard == null) continue;

            i++;
            child.localPosition = new Vector3(0f, i * yOffset, 0f);
            lastCard = childCard;

        }

        // 3. 标记最上面那张卡
        if (lastCard != null)
        {
            lastCard.isTopVisual = true;
        }
        else
        {
            Card rootCard = stackRoot.GetComponent<Card>();
            if (rootCard != null)
            {
                rootCard.isTopVisual = true;
            }
        }
    }*/

    private void OnMouseEnter()
    {
        if (infoBar != null && data != null)
        {
            infoBar.ShowInfoBar(data);
        }
    }

    private void OnMouseExit()
    {
        if (infoBar != null)
        {
            infoBar.HideInfoBar();
        }
    }

    // ====================== Feeding Helpers =====================
    public bool IsTopOfStack()
    {
        Transform parentRoot = stackRoot != null ? stackRoot : transform;
        if (parentRoot.childCount == 0)
        {
            return transform == parentRoot;
        }

        if (transform == parentRoot)
        {
            return false;
        }

        return transform.parent == parentRoot &&
               transform.GetSiblingIndex() == parentRoot.childCount - 1;
    }

    /// <summary>
    /// 从 stack 中抽出 stackRoot
    /// </summary>
    public void TakeRootOutOfStack()
    {
        Transform root = stackRoot != null ? stackRoot : transform;

        // 自己是 stackRoot
        // !!不考虑其他情况!! 调用这个函数的地方不该取出 stack 中间的卡牌
        if (transform == root && root.childCount > 0)
        {
            Transform newRoot = null;
            List<Transform> toMove = new List<Transform>();

            // 在 child 里找 Card 组件：第一个 Card 当 newRoot，其余是 toMove
            foreach (Transform child in root)
            {
                Card childCard = child.GetComponent<Card>();
                if (childCard == null) continue;  // 跳过 StatsRoot 等 UI

                if (newRoot == null)
                {
                    newRoot = child;
                }
                else
                {
                    toMove.Add(child);
                }
            }

            // 根本没有别的卡，那就不需要拆 stack
            if (newRoot == null)
            {
                return;
            }

            // 把其它卡挂到 newRoot 下面
            foreach (Transform child in toMove)
            {
                child.SetParent(newRoot);
            }

            // newRoot 自己变成一个新的 stackRoot
            newRoot.SetParent(null);
            Card newRootCard = newRoot.GetComponent<Card>();

            if (newRootCard != null)
            {
                newRootCard.stackRoot = newRoot;

                foreach (Transform t in newRoot)
                {
                    Card c = t.GetComponent<Card>();
                    if (c != null)
                    {
                        c.stackRoot = newRoot;
                    }
                }

                newRootCard.LayoutStack();
            }

            // 原来的 root 只剩自己（+自己的 UI），自己单独一张牌
            stackRoot = transform;
            transform.SetParent(null);
        }
    }

    // =========================== Card Manager Helpers ==========================
    public void ChangeSaturation(int eaten)
    {
        int old = currentSaturation;
        int now = old - eaten;
        currentSaturation = now;

        if (CardManager.Instance != null && data != null && data.cardClass == CardClass.Food)
        {
            CardManager.Instance.ReduceSaturation(eaten);
        }
    }

    private void TryRegisterToManager()
    {
        if (HasRegisteredToManager) return;
        if (CardManager.Instance == null) return;
        if (data == null) return;   // 没 data 先别注册

        // 不登记装备栏内的卡牌
        if (RuntimeState != CardRuntimeState.OnBoard) return;

        CardManager.Instance.RegisterCard(this);
    }

    /// <summary>
    /// 更改卡牌 onboard 状态
    /// 同时根据新状态 toggle register
    /// </summary>
    public void SetRuntimeState(CardRuntimeState newState)
    {
        if (runtimeState == newState) return;

        var oldState = runtimeState;
        runtimeState = newState;

        if (CardManager.Instance == null) return;

        // 从 OnBoard 被移到装备栏需要 unregister
        if (oldState == CardRuntimeState.OnBoard && newState != CardRuntimeState.OnBoard)
        {
            if (HasRegisteredToManager)
            {
                CardManager.Instance.UnregisterCard(this);
                HasRegisteredToManager = false;
            }
        }

        // 从装备栏移到 OnBoard 需要 register
        else if (oldState != CardRuntimeState.OnBoard && newState == CardRuntimeState.OnBoard)
        {
            TryRegisterToManager();
        }
    }

    public void ResetToDefaultSize()
    {
        transform.localScale = defaultScale;
    }

    public void SetEquipVisual(bool inEquip)
    {
        // 可以调比例
        transform.localScale = inEquip ? defaultScale * 0.7f : defaultScale;
    }

    // ========================== Battle ============================
    /// <summary>
    /// 确保 currentHP 按 data 初始化一次
    /// </summary>
    public void EnsureBattleInit()
    {
        if (hasInitHP) return;

        // 这里的 baseHP 改成你 CardData 里真实的字段名
        currentHP = data != null ? data.baseHP : 0;
        hasInitHP = true;
    }
}
