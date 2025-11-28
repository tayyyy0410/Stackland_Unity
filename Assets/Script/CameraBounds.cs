using UnityEngine;


[RequireComponent(typeof(Collider2D))]
public class CameraBounds : MonoBehaviour
{
    public static CameraBounds I { get; private set; }

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

    /// 提供世界空间下的 Bounds
    public Bounds WorldBounds
    {
        get
        {
            if (col == null)
            {
         
                return new Bounds(Vector3.zero, new Vector3(9999f, 9999f, 0f));
            }

            return col.bounds;
        }
    }
}