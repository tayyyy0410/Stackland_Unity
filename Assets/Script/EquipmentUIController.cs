using System.Collections.Generic;
using UnityEngine;

public class EquipmentUIController : MonoBehaviour
{
    public static EquipmentUIController Instance { get; private set; }

    [Header("Prefabs")]
    public GameObject smallEquipBarPrefab;   // 小装备条
    public GameObject bigEquipPanelPrefab;   // 大装备栏

    [Header("Equip Runtime Roots")]
    [Tooltip("用来暂存装备中的真实卡牌")]
    [SerializeField] private Transform equippedCardsRoot;

    private Dictionary<Card, EquipBar> smallBars =
        new Dictionary<Card, EquipBar>();

    private Dictionary<Card, GameObject> bigPanels =
        new Dictionary<Card, GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 如果在 Inspector 里没手动指定，就代码里建一个空物体
        if (equippedCardsRoot == null)
        {
            var go = new GameObject("EquippedCardsRoot");
            equippedCardsRoot = go.transform;
        }
    }

    private void Update()
    {
        // 没有任何大装备栏开着，不用处理
        if (bigPanels == null || bigPanels.Count == 0) return;

        // 只在按下左键那一瞬间检测
        if (!Input.GetMouseButtonDown(0)) return;

        var cam = Camera.main;
        if (cam == null) return;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 point2D = new Vector2(mouseWorld.x, mouseWorld.y);

        // 有没有碰到任何 collider
        var hits = Physics2D.OverlapPointAll(point2D);

        // 如果点到的是 Card 或者 大装备栏 的 collider，不关 panel
        foreach (var hit in hits)
        {
            if (hit == null) continue;

            // 点到任意卡牌
            if (hit.GetComponent<Card>() != null)
            {
                return;
            }

            // 点到大装备栏本身
            /*if (hit.GetComponent<BigEquipPanelMarker>() != null)
            {
                return;
            }*/

            // 有别的 UI collider可以再加其他判断
        }

        // 视为点击地图空白，关掉大装备栏

        var villagersWithOpenPanels = new List<Card>(bigPanels.Keys);
        foreach (var villager in villagersWithOpenPanels)
        {
            CloseBigPanel(villager);
        }
    }


    // =================================================================================
    public bool IsBigPanelOpenFor(Card villager)
    {
        return villager != null &&
               bigPanels.TryGetValue(villager, out var panel) &&
               panel != null;
    }

    public bool HasSmallBarFor(Card villager)
    {
        return villager != null &&
               smallBars.TryGetValue(villager, out var bar) &&
               bar != null;
    }

    // 小条被点击时调用：对这个 villager 切换“大装备栏”
    // 再根据大装备栏应有的状态刷新小装备条状态
    public void ToggleBigPanelFor(Card villager)
    {
        if (villager == null) return;

        if (IsBigPanelOpenFor(villager))
        {
            CloseBigPanel(villager);
        }
        else
        {
            OpenBigPanel(villager);
        }
    }

    // 打开大装备栏，收起小装备条
    public void OpenBigPanel(Card villager)
    {
        if (villager == null || bigEquipPanelPrefab == null) return;

        // 同一个 villager，已有大栏就先删掉，重新生成
        CloseBigPanel(villager);

        // 打开大栏时，这个 villager 的小条要消失
        CloseSmallBar(villager);

        GameObject panel = Instantiate(bigEquipPanelPrefab);
        bigPanels[villager] = panel;

        PositionPanelNearVillager(villager, panel);
        RebuildBigPanelContent(villager);
    }

    // 大装备栏打开的时候，layout装备栏卡牌
    public void RebuildBigPanelContent(Card villager)
    {
        if (villager == null) return;
        if (CardManager.Instance == null) return;

        // 没有大装备栏就不管
        if (!bigPanels.TryGetValue(villager, out var panel) || panel == null)
        {
            Debug.Log("[RebuildBigPanel] 没有大装备栏！");
            return;
        }

        // 拿到这个 villager 的装备状态
        var state = CardManager.Instance.GetEquipState(villager);
        if (state == null)
        {
            Debug.Log("[RebuildBigPanel] 没有装备状态!");
            return;
        }
        //Debug.Log($"[VillagerEquipState] hand: {state.hand.data.displayName}");

        PlaceEquipCardIfExists(state.head, panel, new Vector3(-0.7f, 0f, 0f));
        PlaceEquipCardIfExists(state.hand, panel, new Vector3(0f, 0f, 0f));
        PlaceEquipCardIfExists(state.body, panel, new Vector3(0.7f, 0f, 0f));
    }


    // 大装备栏的卡牌layout
    private void PlaceEquipCardIfExists(Card equipCard, GameObject panel, Vector3 localPos)
    {
        if (equipCard == null || panel == null) return;

        var t = equipCard.transform;

        t.SetParent(panel.transform, worldPositionStays: true);
        equipCard.gameObject.SetActive(true);

        t.localPosition = localPos;
        equipCard.transform.localScale = equipCard.defaultScale * 0.7f;

        // 让装备卡画在大装备栏上面
        var equipSR = equipCard.GetComponent<SpriteRenderer>();
        var panelSR = panel.GetComponent<SpriteRenderer>();

        if (equipSR != null && panelSR != null)
        {
            equipSR.sortingOrder = panelSR.sortingOrder + 1;
        }
    }

    
    /// <summary>
    /// 关闭大装备栏并根据装备栏信息刷新小装备条状态
    /// </summary>
    public void CloseBigPanel(Card villager)
    {
        if (villager == null) return;

        if (bigPanels.TryGetValue(villager, out var panel) && panel != null)
        {
            // 在 Destroy panel 前，先把 panel 下面的 Card 都转移到 equippedCardsRoot
            TransferCardsFromPanelToEquipRoot(panel.transform);
            Destroy(panel);
        }
        bigPanels.Remove(villager);

        // 关大栏之后，如果这个 villager 仍然有装备，就恢复小条
        if (CardManager.Instance != null &&
            CardManager.Instance.VillagerHasAnyEquip(villager))
        {
            OnTopDisplaySmallBar(villager);
        }
        else
        {
            CloseSmallBar(villager);
        }
    }

    // 大装备栏关闭时保存卡牌信息到empty
    private void TransferCardsFromPanelToEquipRoot(Transform panelTf)
    {
        if (equippedCardsRoot == null) return;

        while (panelTf.childCount > 0)
        {
            Transform child = panelTf.GetChild(0);
            var card = child.GetComponent<Card>();
            if (card != null)
            {
                child.SetParent(equippedCardsRoot, worldPositionStays: true);

                // 先藏起来
                child.gameObject.SetActive(false);
            }
            else
            {
                // 不是 Card 的别的东西，正常 Destroy
                child.SetParent(null);
                Destroy(child.gameObject);
            }
        }
    }

    /// <summary>
    /// 强制重新创建小装备条 gameObject，并填进 dictionary
    /// </summary>
    private EquipBar ForceCreateSmallBar(Card villager)
    {
        if (villager == null || smallEquipBarPrefab == null) return null;

        // 如果已经有一条旧的，先清理，防止 fake-null
        if (smallBars.TryGetValue(villager, out var oldBar) && oldBar != null)
        {
            Destroy(oldBar.gameObject);
        }

        GameObject go = Instantiate(smallEquipBarPrefab, villager.transform);
        go.transform.localPosition = new Vector3(0f, -1f, 0f);

        var bar = go.GetComponent<EquipBar>();
        if (bar != null)
        {
            bar.Init(villager);
        }

        var villagerSR = villager.GetComponent<SpriteRenderer>();
        var barSR = go.GetComponent<SpriteRenderer>();
        if (villagerSR != null && barSR != null)
        {
            barSR.sortingOrder = villagerSR.sortingOrder + 1;
        }

        smallBars[villager] = bar;
        return bar;
    }

    /// <summary>
    /// 读取大装备栏信息，刷新小装备条状态
    /// !!!不是无脑create，大装备栏状态是判定依据!!!
    /// </summary>
    public void EnsureSmallBar(Card villager)
    {
        if (villager == null || smallEquipBarPrefab == null) return;

        // 已经有大装备栏：不要小条
        if (IsBigPanelOpenFor(villager))
        {
            CloseSmallBar(villager);
            return;
        }

        // 没有小条就创建，有就重新可见
        if (!smallBars.TryGetValue(villager, out var bar) || 
            bar == null || 
            bar.gameObject == null)
        {
            bar = ForceCreateSmallBar(villager);
            if (bar == null) return;
        }

        bar.gameObject.SetActive(true);
    }

    /// <summary>
    /// 给layoutstack用的接口
    /// 目前只给layoutstack用，根据topcard判定，隐藏/显示 EquipBar
    /// ！！！没有判定，只根据input决定显不显示 EquipBar
    /// </summary>
    public void SetSmallBarVisible(Card villager, bool visible)
    {
        if (villager == null) return;

        if (visible)
        {
            // 不判定大装备栏
            if (!smallBars.TryGetValue(villager, out var bar) || bar == null || bar.gameObject == null)
            {
                bar = ForceCreateSmallBar(villager);
                if (bar == null) return;
            }

            // 强制挂回 owner 身上
            // 防止和 statsUI 打架
            Transform tf = bar.transform;
            if (tf.parent != villager.transform)
            {
                tf.SetParent(villager.transform, worldPositionStays: false);
                tf.localPosition = new Vector3(0f, -1f, 0f);   // 小条在 villager 底边
            }

            bar.gameObject.SetActive(true);

            var villagerSR = villager.GetComponent<SpriteRenderer>();
            var barSR = bar.GetComponent<SpriteRenderer>();
            if (villagerSR != null && barSR != null)
            {
                barSR.sortingOrder = villagerSR.sortingOrder + 1;
            }
        }
        else
        {
            // 不想显示的时候，不 Destroy，只隐藏
            // 只给layoutstack用，隐藏不是topcard的ui
            if (smallBars.TryGetValue(villager, out var bar) && bar != null && bar.gameObject != null)
            {
                bar.gameObject.SetActive(false);
            }
        }
    }

    public void CloseSmallBar(Card villager)
    {
        if (villager == null) return;

        if (smallBars.TryGetValue(villager, out var bar) && bar != null)
        {
            Destroy(bar.gameObject);
        }
        smallBars.Remove(villager);
    }

    private void PositionPanelNearVillager(Card villager, GameObject panel)
    {
        if (villager == null || panel == null) return;

        Vector3 vPos = villager.transform.position;
        panel.transform.position = vPos + new Vector3(0f, -0.5f, 0f);

        var villagerSR = villager.GetComponent<SpriteRenderer>();
        var panelSR = panel.GetComponent<SpriteRenderer>();
        if (villagerSR != null && panelSR != null)
        {
            panelSR.sortingOrder = villagerSR.sortingOrder + 1;
        }

    }


    /// <summary>
    /// 判断villager是否是top card，只有top card才显示EquipBar
    /// </summary>
    public void OnTopDisplaySmallBar(Card villager)
    {
        if (villager == null) return;

        // 只有在是顶牌的时候，才应该有小条
        bool shouldShowSmallBar = villager.isTopVisual;

        SetSmallBarVisible(villager, shouldShowSmallBar);
    }




}
