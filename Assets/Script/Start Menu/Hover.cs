using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Sprite Default;
    public Sprite Hovering;

    private Image image;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        image = GetComponent<Image>();
        image.sprite = Default;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        image.sprite = Hovering;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        image.sprite = Default;
    }
}
