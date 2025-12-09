using UnityEngine;
using System.Collections;

public class CameraFocusOnStartPack : MonoBehaviour
{
    [Header("Start Pack Focus")]
    [Tooltip("起始卡包的 Transform")]
    public Transform startPack;

    [Tooltip("开局时相机对准卡包时的 orthographicSize")]
    public float focusZoom = 2.8f;

    private Camera cam;
    private CameraControllers camController;

    private float defaultZoom;
    private Vector3 defaultPos;

    private void Start()
    {
        cam = Camera.main;
        camController = FindFirstObjectByType<CameraControllers>();

        if (cam == null)
        {
            Debug.LogWarning("[CameraFocusOnStartPack] 找不到 Camera.main");
            return;
        }

        // 记录正常游戏视角
        defaultZoom = cam.orthographicSize;
        defaultPos = cam.transform.position;

        // 一开始就对准起始卡包，并 zoom in 到特写
        if (startPack != null)
        {
            Vector3 p = startPack.position;
            cam.transform.position = new Vector3(p.x, p.y, defaultPos.z);
            cam.orthographicSize = focusZoom;
        }

        // 在开完包之前，不允许玩家拖拽
        if (camController != null)
            camController.enabled = false;

        // 等玩家把包开完解锁相机
        StartCoroutine(WaitUntilPackOpened_ThenEnableCamera());
    }

    private IEnumerator WaitUntilPackOpened_ThenEnableCamera()
    {
        CardPack pack = startPack != null ? startPack.GetComponent<CardPack>() : null;

        if (pack != null)
        {
            // 只要包还在场上，就一直等
            while (pack != null)
            {
                yield return null;
            }
        }

    
        yield return new WaitForEndOfFrame();

        // 重新允许玩家拖拽
        if (camController != null)
            camController.enabled = true;

       
    }

}