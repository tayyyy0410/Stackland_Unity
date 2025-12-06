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

    // 大装备栏打开的时候刷新信息
    public void RebuildBigPanelContent(Card villager)
    {
        if (villager == null) return;
        if (CardManager.Instance == null) return;

        // 没有大装备栏就不管
        if (!bigPanels.TryGetValue(villager, out var panel) || panel == null)
        {
            return;
        }

        // 拿到这个 villager 的装备状态
        var state = CardManager.Instance.GetEquipState(villager);
        if (state == null)
        {
            Debug.Log("没有装备状态!");
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
            EnsureSmallBar(villager);
        }
        else
        {
            CloseSmallBar(villager);
        }
    }

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

    public void EnsureSmallBar(Card villager)
    {
        if (villager == null || smallEquipBarPrefab == null) return;

        // 已经有大装备栏：不要小条
        if (IsBigPanelOpenFor(villager))
        {
            CloseSmallBar(villager);
            return;
        }

        // 已有小条：不重复创建
        if (HasSmallBarFor(villager))
        {
            return;
        }

        GameObject go = Instantiate(smallEquipBarPrefab, villager.transform);
        go.transform.localPosition = new Vector3(0f, -1f, 0f);

        var bar = go.GetComponent<EquipBar>();
        if (bar != null)
        {
            bar.Init(villager);
        }

        smallBars[villager] = bar;

        var villagerSR = villager.GetComponent<SpriteRenderer>();
        var barSR = go.GetComponent<SpriteRenderer>();
        if (villagerSR != null && barSR != null)
        {
            barSR.sortingOrder = villagerSR.sortingOrder + 1;
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



}
