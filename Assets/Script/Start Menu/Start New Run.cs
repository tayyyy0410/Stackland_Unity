using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Collections;

public class StartNewRun : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private string sceneName;     
    [SerializeField] private Transform airplane;   
    [SerializeField] private float speed = 5f;      
    [SerializeField] private float exitX = 20f;    
    [SerializeField] private float delayAfterExit = 1f;

    private bool isRunning = false;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isRunning) return;      
        isRunning = true;
        //飞机音效
        if (AudioManager.I != null && AudioManager.I.airplaneSfx != null)
        {
            AudioManager.I.PlaySFX(AudioManager.I.airplaneSfx);
        }

        StartCoroutine(FlyPlane());
    }

    private IEnumerator FlyPlane()
    {
        while (airplane.position.x < exitX)
        {
            airplane.position += Vector3.right * speed * Time.unscaledDeltaTime;
            yield return null;
        }

        yield return new WaitForSecondsRealtime(delayAfterExit);

        Debug.Log("Load scene: " + sceneName);
        SceneManager.LoadScene(sceneName);
    }
}