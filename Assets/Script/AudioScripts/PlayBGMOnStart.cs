using UnityEngine;

public class PlayBGMOnStart : MonoBehaviour
{
    public AudioClip bgm;

    private void Start()
    {
        if (AudioManager.I != null)
        {
            AudioManager.I.PlayBGM(bgm);
        }
    }
}