using UnityEngine;

public class EquipBar : MonoBehaviour
{
    private Card ownerVillager;

    public void Init(Card villager)
    {
        ownerVillager = villager;
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
}
