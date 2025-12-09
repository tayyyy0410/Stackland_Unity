using UnityEngine;

public class AudioManager : MonoBehaviour
{
    //管声音的
    public static AudioManager I;

    [Header("Sources")]
    public AudioSource bgmSource;
    public AudioSource sfxSource;
    [Header("Common SFX")]
    public AudioClip stackSfx;      // 叠加
    public AudioClip spawnSfx;      // 产出
    public AudioClip packOpenSfx;   // 开包
    public AudioClip bellSfx;       // 摇铃
    public AudioClip eatSfx;        // 进食
    public AudioClip attackSfx;     // 攻击


    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    public void PlayBGM(AudioClip clip, bool loop = true)
    {
        if (clip == null) return;
        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.Play();
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip);
    }
}