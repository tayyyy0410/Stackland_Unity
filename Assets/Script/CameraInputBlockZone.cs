using UnityEngine;
using UnityEngine.EventSystems;

public class CameraInputBlockZone : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        CameraControllers.uiBlockCameraInput = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        CameraControllers.uiBlockCameraInput = false;
    }
}