using UnityEngine;
using UnityEngine.UI;

public class EquipBar : MonoBehaviour
{
    [Header("Icons")]
    [SerializeField] public SpriteRenderer headIcon;
    [SerializeField] public SpriteRenderer handIcon;
    [SerializeField] public SpriteRenderer bodyIcon;

    private SpriteRenderer mainSR;
    private int lastSO;
    
    private Card ownerVillager;

    public void Init(Card villager)
    {
        ownerVillager = villager;
    }

    private void Awake()
    {
        if (mainSR == null)
        {
            mainSR = GetComponent<SpriteRenderer>();
        }
    }

    private void Start()
    {
        lastSO = mainSR.sortingOrder;
    }

    void LateUpdate()
    {
        // 如果本体的 sorting 变了，就更新小图标
        if (mainSR.sortingOrder != lastSO)
        {
            SyncIconsSorting();
        }
    }

    private void OnMouseDown()
    {
        if (ownerVillager == null) return;

        if (!ownerVillager.isTopVisual)
        {
            Debug.Log($"[EquipUI] {ownerVillager.name} 不是顶牌，EquipBar点击被忽略");
            return;
        }

        // 比如暂停、结算阶段不允许开关
        if (DayManager.Instance == null) return;
        if (DayManager.Instance.CurrentState != DayManager.DayState.Running &&
            DayManager.Instance.CurrentState != DayManager.DayState.Selling)
        {
            return;
        }

        if (EquipmentUIController.Instance != null)
        {
            EquipmentUIController.Instance.ToggleBigPanelFor(ownerVillager);
        }
        else
        {
            Debug.LogWarning("[EquipUI] 没有 EquipmentUIController.Instance");
        }
    }

    private void SyncIconsSorting()
    {
        lastSO = mainSR.sortingOrder;
        headIcon.sortingOrder = lastSO + 1;
        handIcon.sortingOrder = lastSO + 1;
        bodyIcon.sortingOrder = lastSO + 1;
    }

    public void RefreshFromOwner()
    {
        if (ownerVillager == null || EquipManager.Instance == null) return;

        var state = EquipManager.Instance.GetEquipState(ownerVillager);
        bool hasHand = state != null && state.hand != null;
        bool hasHead = state != null && state.head != null;
        bool hasBody = state != null && state.body != null;

        if (handIcon != null)
            handIcon.color = hasHand ? Color.black : Color.white;
        if (headIcon != null)
            headIcon.color = hasHead ? Color.black : Color.white;
        if (bodyIcon != null)
            bodyIcon.color = hasBody ? Color.black : Color.white;
    }
}
