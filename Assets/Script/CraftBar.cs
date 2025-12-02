using UnityEngine;

public class CraftBar : MonoBehaviour
{
    public Transform target;      // 跟随的卡 stackRoot
    public Transform fill;        // 填充条（SpriteRenderer 宽度 = 1）
    public float offsetY = 1.2f;  // 离卡牌的高度
    public float maxWidth = 1.0f; // 填充条的最大宽度（世界单位）

    private float progress = 0f;

    public void Init(Transform followTarget)
    {
        target = followTarget;
        SetProgress(0f);
    }

    private void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        // 跟随卡牌
        transform.position = target.position + new Vector3(0, offsetY, 0);
    }

    /// 设置进度（0~1）
    public void SetProgress(float p)
    {
        progress = Mathf.Clamp01(p);
        UpdateFill();
    }

    private void UpdateFill()
    {
        if (fill == null) return;
        float curWidth = maxWidth * progress;

        fill.localScale = new Vector3(curWidth, fill.localScale.y, fill.localScale.z);
        float centerX = -maxWidth / 2f + curWidth / 2f;
        fill.localPosition = new Vector3(centerX, 0, 0);
    }
}