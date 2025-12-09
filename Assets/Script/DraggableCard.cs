using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DraggableCard : MonoBehaviour
{
    private static int globalSortingOrder = 0;

    private Camera cam;
    public bool isDragging = false;
    private Vector3 offset;

    //本次拖拽开始时，这张卡是否在装备状态
    private bool wasEquipAtDragStart;


    private SpriteRenderer sr;
    private int originalSortingOrder;

    private Card card;
    private Transform dragRoot;

    public float radius = 0.2f; // 检测堆叠的范围

    private void Awake()
    {
        cam = Camera.main;
        sr = GetComponent<SpriteRenderer>();
        card = GetComponent<Card>();
    }

    private void OnMouseDown()
    {
        if (!CanInteract()) return;
        if (card == null ) return;
        //wasEquipAtDragStart = (card.RuntimeState == CardRuntimeState.InEquipmentUI);

        // 如果点击了装备卡，立刻卸下，拿在手中
        if (card.RuntimeState == CardRuntimeState.InEquipmentUI &&
            EquipManager.Instance != null)
        {
            // 立即把装备卡的状态刷新
            EquipManager.Instance.UnequipFromVillagerImmediate(card);
        }

        // 拖动 villager 的时候会自动关掉所有大装备栏
        if (EquipmentUIController.Instance != null)
        {
            Transform rt = card.stackRoot != null ? card.stackRoot : card.transform;
            Card[] cardsInStack = rt.GetComponentsInChildren<Card>();

            foreach (var c in cardsInStack)
            {
                if (c == null || c.data == null) continue;
                if (c.data.cardClass != CardClass.Villager) continue;

                EquipmentUIController.Instance.CloseBigPanel(c);
            }
        }

        // 战斗：如果这是正在战斗中的村民 ，拖动时先中断战斗 
        if (cam == null) cam = Camera.main;
        if (card != null && card.data != null && card.data.cardClass == CardClass.Villager)
        {
            if (card.IsInBattle && BattleManager.Instance != null)
            {
                BattleManager.Instance.StopBattleFor(card);
            }
        }

        if (card == null)
        {
            dragRoot = transform;
        }
        else
        {
            bool isRoot = (card.stackRoot == card.transform);

            if (isRoot)
            {
                dragRoot = card.stackRoot;
            }
            else
            {
                // 如果中间这张下面还有牌，就和它下面的牌一起组成一个新stack
                Transform oldRoot = card.stackRoot;

                if (oldRoot != null)
                {
                    // 当前这张卡在旧stack中的索引
                    int index = transform.GetSiblingIndex();

                    System.Collections.Generic.List<Transform> belowCards =
                        new System.Collections.Generic.List<Transform>();
                    for (int i = index + 1; i < oldRoot.childCount; i++)
                    {
                        belowCards.Add(oldRoot.GetChild(i));
                    }

                    // 变成新的 stackRoot
                    transform.SetParent(null);
                    card.stackRoot = transform;

                    // 把下面那些牌也一起挂到这张牌下面，组成新的子stack
                    foreach (Transform t in belowCards)
                    {
                        t.SetParent(transform);
                        Card c = t.GetComponent<Card>();
                        if (c != null)
                        {
                            c.stackRoot = transform;
                        }
                    }

                    // 让旧的那一叠重新排一下
                    if (oldRoot != null)
                    {
                        Card rootCard = oldRoot.GetComponent<Card>();
                        if (rootCard != null)
                        {
                            rootCard.LayoutStack();
                        }
                    }

                    // 新的子stack 也排一下
                    card.LayoutStack();
                }

                dragRoot = card.stackRoot;
            }
        }

        isDragging = true;

        // 有 stackRoot 就动 stackRoot，没有就动自己
        Vector3 mouseWorldPos = cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = dragRoot.position.z;
        offset = dragRoot.position - mouseWorldPos;

        if (sr != null)
        {
            originalSortingOrder = sr.sortingOrder;

            bool isSingleCard = (card == null) ||
                                (card.stackRoot == transform && transform.childCount == 0);

            // 每次点击分配一个新的排序区间
            int baseOrder = (++globalSortingOrder) * 10;

            if (isSingleCard)
            {
                sr.sortingOrder = baseOrder;
            }
            else
            {
                int i = 0;
                foreach (var s in dragRoot.GetComponentsInChildren<SpriteRenderer>())
                {
                    s.sortingOrder = baseOrder + i;
                    i++;
                }
            }
        }

        //播放捡起音效
        if (AudioManager.I != null && card != null && card.data != null && card.data.pickSfx != null)
        {
            AudioManager.I.PlaySFX(card.data.pickSfx);
        }
    }

    private void OnMouseDrag()
    {
        if (!CanInteract()) return;
        if (!isDragging) return;

        Vector3 mouseWorldPos = cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = dragRoot.position.z;

        // 移动一整个 stack
        dragRoot.position = mouseWorldPos + offset;
    }

    private void OnMouseUp()
    {
        if (!CanInteract()) return;
        if (!isDragging) return;
        isDragging = false;

        if (dragRoot == null) return;

        Card rootCard = dragRoot.GetComponent<Card>();

        bool stacked = false;

        // 优先尝试开始战斗
        if (rootCard != null)
        {
            if (TryStartBattle(rootCard))
            {
                // 开战以后不进行堆叠/合成/买卖
                return;
            }
        }

        // 处理是否装备
        // 被处理的装备卡之后不走常规stack逻辑
        bool equipmentDropHandled = TryStackOnOtherCard();
        if (equipmentDropHandled) return;

        //  触发 recipe
        if (RecipeManager.Instance != null)
        {
            Transform rootTransform = dragRoot != null ? dragRoot : transform;
            Card rootCard2 = rootTransform.GetComponent<Card>();
            if (rootCard2 != null)
            {
                RecipeManager.Instance.TryCraftFromStack(rootCard2);
            }
        }

        // 检查是否在 Shop / Sell 区域 
        TryBuyPackIfOnShop();

        // 最后再找一次“真正的最终 stackRoot”，统一排一下
        Card finalRootCard = null;
        if (card != null)
        {
            Transform finalRoot = card.stackRoot != null ? card.stackRoot : card.transform;
            finalRootCard = finalRoot.GetComponent<Card>();
        }
        else if (dragRoot != null)
        {
            finalRootCard = dragRoot.GetComponent<Card>();
        }

        if (finalRootCard != null)
        {
            finalRootCard.LayoutStack();
        }

        PlayDropOrStackSfx(stacked);
    }

    
    /// 松手时检查：当前这叠在不在某个 Shop 或 Sell 区域上
    private void TryBuyPackIfOnShop()
    {
        if (card == null) return;

        // 当前 stack 的 root
        Transform root = card.stackRoot != null ? card.stackRoot : transform;
        Vector2 pos = root.position;

        Card rootCard = root.GetComponent<Card>();
        if (rootCard == null) return;

        //先查所有 PackShopArea
        PackShopArea[] shops = FindObjectsByType<PackShopArea>(FindObjectsSortMode.None);
        foreach (var shop in shops)
        {
            if (shop == null) continue;

            var shopCol = shop.GetComponent<Collider2D>();
            if (shopCol == null) continue;

            // 用自己的 collider 来判断 root 是否在里面
            if (shopCol.OverlapPoint(pos))
            {
                Debug.Log("[DraggableCard] 在 Shop 区域上松手，尝试买卡包");
                shop.TryBuyFromStack(rootCard);
                return;    // 找到 Shop 就直接结束，不再检查 Sell
            }
        }

        // 如果没有 Shop 命中，再查所有 Sell 区域
        CardSellArea[] sells = FindObjectsByType<CardSellArea>(FindObjectsSortMode.None);
        foreach (var sellArea in sells)
        {
            if (sellArea == null) continue;

            var sellCol = sellArea.GetComponent<Collider2D>();
            if (sellCol == null) continue;

            if (sellCol.OverlapPoint(pos))
            {
                Debug.Log("[DraggableCard] 在 Sell 区域上松手，尝试卖卡");
                sellArea.TrySellFromStack(rootCard);
                return;
            }
        }
    }

    /// 检测周围有没有其他牌，用来自动堆叠
    /*public bool TryStackOnOtherCard()
    {
        if (card == null) return false;
        if (dragRoot == null) return false;

        radius = 0.2f;
        var hits = Physics2D.OverlapCircleAll(dragRoot.position, radius);

        Card sourceRootCard = dragRoot.GetComponent<Card>();
        if (sourceRootCard == null) return false;

        bool stacked = false; //触发什么audio的判定条件

        foreach (var hit in hits)
        {
            // 跳过自己这整个 stack 里的牌
            if (hit.transform == dragRoot || hit.transform.IsChildOf(dragRoot))
                continue;

            var otherCard = hit.GetComponent<Card>();
            if (otherCard == null) continue;

            // TODO：之后这里可以加 class 规则 / maxStack 限制

            // 把这个子stack整叠的 root 叠到对方那一个stack上
            sourceRootCard.JoinStackOf(otherCard);
            stacked = true;
            break;
        }
        return stacked;
    }*/

    public bool TryStackOnOtherCard()
    {
        if (card == null) return false;
        if (dragRoot == null) return false;

        radius = 0.2f;
        var hits = Physics2D.OverlapCircleAll(dragRoot.position, radius);

        Card sourceRootCard = dragRoot.GetComponent<Card>();
        if (sourceRootCard == null) return false;

        // 如果 dragroot 是装备卡
        if (sourceRootCard.data != null &&
            sourceRootCard.data.cardClass == CardClass.Equipment &&
            EquipManager.Instance != null)
        {
            foreach (var hit in hits)
            {
                // 跳过自己这整个 stack 里的牌
                if (hit.transform == dragRoot || hit.transform.IsChildOf(dragRoot))
                    continue;

                var otherCard = hit.GetComponent<Card>();

                // 如果有target，需要交给EquipManager来处理装备堆叠
                bool equipped = EquipManager.Instance.TryHandleEquipmentDrop(sourceRootCard, otherCard);
                return equipped;
            }

            return false;
            // 周围没有target，相当于这次只是在map移动了装备的位置，后续仍不检测sell/recipe
        }

        foreach (var hit in hits)
        {
            // 跳过自己这整个 stack 里的牌
            if (hit.transform == dragRoot || hit.transform.IsChildOf(dragRoot))
                continue;

            var otherCard = hit.GetComponent<Card>();
            if (otherCard == null) continue;

            // 在装备栏中的卡牌不能作为target
            // 放下的卡牌应显示在装备栏sr之下
            if (!otherCard.IsOnBoard)
            {
                var otherSR = otherCard.GetComponent<SpriteRenderer>();
                if (otherSR != null)
                {
                    sr.sortingOrder = otherSR.sortingOrder - this.transform.childCount;
                }
                Debug.Log($"[TryStackOnOtherCard] 装备栏中的卡牌不能作为 target！");
                continue;
            }


            // TODO：之后这里可以加 class 规则 / maxStack 限制
            
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.NotifyUseCardOnCard(sourceRootCard, otherCard);
            }
            // 把这个子stack整叠的 root 叠到对方那一个stack上
            sourceRootCard.JoinStackOf(otherCard);
            break;
        }
        return false;
    }

    // 在除了 running 和 selling 阶段锁死卡牌拖拽
    private bool CanInteract()
    {
        if (DayManager.Instance == null)
        {
            return false;
        }
        else
        {
            return DayManager.Instance.CurrentState == DayManager.DayState.Running ||
                   DayManager.Instance.CurrentState == DayManager.DayState.Selling;
        }
    }

    // 战斗触发检测
    // 战斗触发检测
    private bool TryStartBattle(Card rootCard)
    {
        if (rootCard == null || rootCard.data == null) return false;
        if (BattleManager.Instance == null) return false;

        // 只有 村民 / 敌人 / 动物 参与战斗触发，其他卡直接略过
        if (rootCard.data.cardClass != CardClass.Villager &&
            rootCard.data.cardClass != CardClass.Enemy &&
            rootCard.data.cardClass != CardClass.Animals)
        {
            return false;
        }

        float r = 0.3f;
        Vector3 center = rootCard.stackRoot != null ? rootCard.stackRoot.position : rootCard.transform.position;

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, r);
        foreach (var hit in hits)
        {
            if (hit == null) continue;

            // 忽略自己和自己的 stack
            if (hit.transform == rootCard.transform ||
                (rootCard.stackRoot != null && hit.transform.IsChildOf(rootCard.stackRoot)))
                continue;

            Card otherCard = hit.GetComponent<Card>();
            if (otherCard == null || otherCard.data == null) continue;

            Card villager = null;
            Card enemy = null;

            // 情况 1：村民拖到 敌人 / 动物 上
            if (rootCard.data.cardClass == CardClass.Villager &&
                (otherCard.data.cardClass == CardClass.Enemy ||
                 otherCard.data.cardClass == CardClass.Animals))
            {
                villager = rootCard;
                enemy = otherCard;
            }
            // 情况 2：敌人 / 动物 拖到 村民 上
            else if ((rootCard.data.cardClass == CardClass.Enemy ||
                      rootCard.data.cardClass == CardClass.Animals) &&
                     otherCard.data.cardClass == CardClass.Villager)
            {
                villager = otherCard;
                enemy = rootCard;
            }
            else
            {
                // 其他组合不触发战斗
                continue;
            }

            // 统一走 BattleManager，里面会再做一次安全检查
            BattleManager.Instance.StartBattle(villager, enemy);
            return true;
        }

        return false;
    }


    //根据状态选择audio
    private void PlayDropOrStackSfx(bool stacked)
    {
        if (AudioManager.I == null || card == null || card.data == null)
            return;

        if (stacked && AudioManager.I.stackSfx != null)
        {
            // 叠到stack则用通用叠加声
            AudioManager.I.PlaySFX(AudioManager.I.stackSfx);
            
        }
        else if (card.data.dropSfx != null)
        {
            // 没叠上则用放下声
            AudioManager.I.PlaySFX(card.data.dropSfx);
            
        }
    }

}
