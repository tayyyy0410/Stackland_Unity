using UnityEngine;

public class Heartmove : MonoBehaviour
{
    public float speed = 200f;
    public float lifetime = 1f;

    private RectTransform rect;
    void Awake()
    {
        rect = GetComponent<RectTransform>();
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        rect.anchoredPosition += Vector2.up * speed * Time.deltaTime;
    }
}
