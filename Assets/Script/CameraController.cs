using UnityEngine;


[RequireComponent(typeof(Camera))]
public class CameraControllers : MonoBehaviour
{
    [Header(" Center")]
    [Tooltip("镜头默认对准的中心")]
    public Transform focus;

    [Header("Drag")]
    [Tooltip("数值越大移动越快")]
    public float dragSensitivity = 0.01f;

    [Header("Zoom")]
    [Tooltip("越小越）")]
    public float minOrthoSize = 3f;

    [Tooltip("如果没有任何 Bounds")]
    public float maxOrthoSize = 12f;

    [Tooltip("滚轮缩放速度")]
    public float zoomSpeed = 5f;

    private Camera cam;

  
    private Vector3 basePos;   // 只负责 x,y 的中心；z 用相机自己的 z
    private Vector3 offset;    // 相对于 basePos 的偏移

    // 拖拽相关
    private bool isDraggingCamera = false;
    private Vector3 dragStartMousePos;
    private Vector3 dragStartOffset;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        UpdateBasePos();
    }

    private void Update()
    {
        UpdateBasePos();
        HandleZoom();
        HandleDrag();
        ApplyPosition();
    }

    /// 更新镜头中心位置
    private void UpdateBasePos()
    {
        if (focus != null)
        {
            basePos = new Vector3(focus.position.x, focus.position.y, transform.position.z);
        }
        else
        {
            basePos = new Vector3(0f, 0f, transform.position.z);
        }
    }

    /// 按当前 Bounds 计算允许的最大 orthographicSize
    private float GetMaxZoomByBounds()
    {
        Bounds b;
        bool hasBounds = false;

        if (CameraBounds.I != null)
        {
            b = CameraBounds.I.WorldBounds;
            hasBounds = true;
        }
        else if (BoardBounds.I != null)
        {
            b = BoardBounds.I.WorldBounds;
            hasBounds = true;
        }
        else
        {
            hasBounds = false;
            b = new Bounds(Vector3.zero, Vector3.zero);
        }

        if (!hasBounds)
        {
           
            return maxOrthoSize;
        }

        // b.extents 是半宽半高
        float halfWidthWorld = b.extents.x;
        float halfHeightWorld = b.extents.y;

      
        float maxSizeByWidth = halfWidthWorld / cam.aspect;
        float maxSizeByHeight = halfHeightWorld;

        float maxAllowed = Mathf.Min(maxSizeByWidth, maxSizeByHeight);

        // 避免极端情况太小
        if (maxAllowed < minOrthoSize)
        {
            maxAllowed = minOrthoSize;
        }

        return maxAllowed;
    }

    /// 处理滚轮缩放
    private void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;   
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            float size = cam.orthographicSize;
            size -= scroll * zoomSpeed * Time.deltaTime; 

            // 根据当前 Bounds 计算动态最大值
            float dynamicMax = GetMaxZoomByBounds();

            size = Mathf.Clamp(size, minOrthoSize, dynamicMax);
            cam.orthographicSize = size;
        }
    }

    /// 检查当前鼠标是否点在一张 Card 上
    private bool IsPointerOverCard()
    {
        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 p = mouseWorld;

        RaycastHit2D hit = Physics2D.Raycast(p, Vector2.zero, 0f);
        if (hit.collider == null) return false;

        // 下面有普通卡牌
        if (hit.collider.GetComponent<Card>() != null)
            return true;

        // 下面有卡包
        if (hit.collider.GetComponent<CardPack>() != null)
            return true;

        return false;
    }


    private void HandleDrag()
    {
        // 准备拖相机
        if (Input.GetMouseButtonDown(0))
        {
            // 如果点在卡牌上就不启动相机拖拽
            if (IsPointerOverCard())
            {
                isDraggingCamera = false;
            }
            else
            {
                isDraggingCamera = true;
                dragStartMousePos = Input.mousePosition;
                dragStartOffset = offset;
            }
        }
        
        if (Input.GetMouseButton(0) && isDraggingCamera)
        {
            Vector3 curMousePos = Input.mousePosition;
            Vector3 delta = curMousePos - dragStartMousePos;

            
            offset = dragStartOffset + new Vector3(-delta.x, -delta.y, 0f) * dragSensitivity;
        }

        // 松开左键停止拖拽
        if (Input.GetMouseButtonUp(0))
        {
            isDraggingCamera = false;
        }
    }

    /// 把 basePos + offset 应用到相机位置, Clamp 在 Bounds 内
    private void ApplyPosition()
    {
        Vector3 targetPos = basePos + new Vector3(offset.x, offset.y, 0f);
        targetPos.z = basePos.z;

  
        Bounds bounds;
        bool hasBounds = false;

        if (CameraBounds.I != null)
        {
            bounds = CameraBounds.I.WorldBounds;
            hasBounds = true;
        }
        else if (BoardBounds.I != null)
        {
            bounds = BoardBounds.I.WorldBounds;
            hasBounds = true;
        }
        else
        {
            hasBounds = false;
            bounds = new Bounds(Vector3.zero, Vector3.zero);
        }

        if (hasBounds)
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;

            float minX = bounds.min.x + halfW;
            float maxX = bounds.max.x - halfW;
            float minY = bounds.min.y + halfH;
            float maxY = bounds.max.y - halfH;

            // 防止边界比相机视野还小
            if (minX > maxX)
            {
                targetPos.x = bounds.center.x;
            }
            else
            {
                targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
            }

            if (minY > maxY)
            {
                targetPos.y = bounds.center.y;
            }
            else
            {
                targetPos.y = Mathf.Clamp(targetPos.y, minY, maxY);
            }
        }

        transform.position = targetPos;
    }
}
