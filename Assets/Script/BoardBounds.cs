using UnityEngine;

//gameboard 的边界
[RequireComponent(typeof(Collider2D))]
public class BoardBounds : MonoBehaviour
{
    public static BoardBounds I { get; private set; }

    private Collider2D col;

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        col = GetComponent<Collider2D>();
    }
    public Bounds WorldBounds => col.bounds;
    public Vector3 ClampPosition(Vector3 worldPos, float margin = 0f)
    {
        if (col == null) return worldPos;

        Bounds b = col.bounds;

        float minX = b.min.x + margin;
        float maxX = b.max.x - margin;
        float minY = b.min.y + margin;
        float maxY = b.max.y - margin;

        worldPos.x = Mathf.Clamp(worldPos.x, minX, maxX);
        worldPos.y = Mathf.Clamp(worldPos.y, minY, maxY);

        return worldPos;
    }
}