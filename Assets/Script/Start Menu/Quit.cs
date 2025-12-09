using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class Quit : MonoBehaviour, IPointerClickHandler
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Application.Quit();
    }
}
