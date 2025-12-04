using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 绠＄悊澶╂暟锛屾瘡澶╃殑鍊掕鏃舵潯锛屾瘡澶╃粨鏉熶箣鍚庣殑feed缁撶畻
/// CardData 閲岀殑 saturation 鍜� hunger 鍒嗗埆瀵瑰簲椋熺墿鐨勯粯璁らケ鑵瑰�硷紝鍜屾潙姘戠殑姣忓ぉ闇�瑕佺殑楗辫吂鍊硷紙楗ラタ鍊硷級
/// Card 鏂板 currentSaturation 鍜� currentHunger 涓哄綋鍓嶉ケ鑵瑰�煎拰楗ラタ鍊硷紝鐢ㄤ簬缁撶畻
/// 閰嶅悎 PanelManager 椋熺敤锛堬級锛孶I閮藉綊PanelManager绠★紝杩欓噷鍙涓�涓瘡澶╃殑璇绘潯鍊掕鏃�
/// </summary>

public class DayManager : MonoBehaviour
{
    public static DayManager Instance { get; private set; }

    public enum DayState
    {
        Running,        // 姝ｅ父鐜�
        WaitingFeed,        // 涓�澶╃粨鏉燂紝绛夊緟鐜╁鎸� Feed锛堝叏灞�鍐荤粨锛�
        FeedingAnimation,       // 杩涢鍔ㄧ敾鎾斁涓紙鎽勫儚鏈轰緷娆¤仛鐒︽潙姘� + 椋熺墿鍗￠鏉ラ鍘伙級
        FeedingResultAllFull,       // 鍔ㄧ敾缁撴潫锛屾湰杞墍鏈変汉閮藉悆楗辩殑缁撶畻 UI
        FeedingResultHungry,        // 鍔ㄧ敾缁撴潫锛屾湁浜烘病鍚冮ケ鐨勭粨绠� UI锛堟樉绀衡�滃晩鍝︹�濓級
        StarvingAnimation,      // 鐐瑰嚮鈥滃晩鍝︹�濆悗锛屾病鍚冮ケ鐨勪汉涓�涓釜鍙樺案浣撶殑鍔ㄧ敾
        WaitingNextDay,     // 杩樻湁娲讳汉锛岀瓑寰呯偣鍑烩�滃紑濮嬩笅涓�澶┾��
        WaitingEndGame,     // 姝讳骸鍔ㄧ敾鎾畬锛屾鍏変簡锛岀瓑寰呯偣鍑烩�滅粨鏉熸父鎴忊��
        GameOver        // 鐪熸鐨凣ameOver缁撶畻椤甸潰
    }

    [Header("Moon Settings")]
    [Tooltip("姣忎釜 Moon 鐨勯暱搴︼紙绉掞級")]
    public float moonLength = 120f;

    [Tooltip("寮�濮嬫椂鏄鍑犱釜 Moon锛堜竴鑸槸 1锛�")]
    public int startMoon = 1;

    [Header("Day Progress UI")]     // day progress鐨勮鏉�
    [Tooltip("鏄剧ず Moon 杩涘害鐨� Image锛孴ype 瑕佹敼鎴� Filled, Horizontal")]
    public Image moonProgressFill;

    [Tooltip("鏄剧ず褰撳墠 Moon 鏂囨湰锛屾瘮濡� `Moon 1`")]
    public TMP_Text moonText;

    [Header("Villager & Food")]
    [Tooltip("Villager 楗挎涔嬪悗鍙樻垚鐨勫案浣撳崱锛堝彲浠ヤ负绌猴紝涓虹┖鍒欑洿鎺� Destroy锛�")]
    public CardData corpseCardData;

    [Header("Time Scale")]
    public float gameSpeed = 1f;
    public bool dayPaused = false;
    // 杩欓噷闇�瑕佷竴涓猽i锛屽氨鏄鏉′笂闈㈡樉绀虹殑鏍囪瘑
    // 姝ｅ父閫熷害 (gameSpeed == 1) 鐨勬椂鍊欐槸涓�涓皬涓夎锛屽揩杩� (gameSpeed == 2) 鏄竴涓揩杩涙爣璇�
    // 鏆傚仠 (dayPaused锛夋湁涓�涓伆鑹茶挋鐗�
    // !浣嗘槸鏆傚仠鐨勬椂鍊欎笉瑕佸垏鎹� Panel 鎴栬�� DayState锛屼唬鐮佽繕鏄啓鍦� DayState.Running 涓嬬殑

    public DayState CurrentState { get; private set; } = DayState.Running;
    public System.Action<DayState> OnStateChanged;

    private int currentMoon;
    private float timer;    // 姣忓ぉ鐨勬椂闂�

    public int CurrentMoon => currentMoon;
    public float NormalizedTime => Mathf.Clamp01(timer / Mathf.Max(0.01f, moonLength));

    // 缁撶畻 food 鍜� villager
    private readonly List<Card> lastVillagers = new List<Card>();
    private readonly List<Card> lastHungryVillagers = new List<Card>();
    private bool lastAllFed = false;

    public IReadOnlyList<Card> LastVillagers => lastVillagers;
    public IReadOnlyList<Card> LastHungryVillagers => lastHungryVillagers;  // UI: 璋冪敤澶氬皯涓潙姘戞尐楗� LastHungryVillager.Count


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitDay();
    }


    private void InitDay()
    {
        currentMoon = Mathf.Max(1, startMoon);
        timer = 0f;

        gameSpeed = 1f;
        dayPaused = false;
        SetState(DayState.Running);

        UpdateUI();
    }


    private void Update()
    {
        if (CurrentState != DayState.Running) return;
        if (moonLength <= 0f) return;

        if (!dayPaused) { timer += Time.deltaTime * gameSpeed; }

        if (CardManager.Instance == null) return;
        if (CardManager.Instance.VillagerCards.Count <= 0)
        {
            Invoke(nameof(EnterGameOver), 1f);
        }


        if (Input.GetKeyDown(KeyCode.Tab))
        {
            HandleFastForawrd();
        }
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            HandlePause();
        }


        if (timer >= moonLength)    // 涓�澶╁�掕鏃剁粨鏉�
        {
            timer = moonLength;
            UpdateUI();
            EndCurrentMoon();
        }
        else
        {
            UpdateUI();
        }
    }

    /// <summary>
    /// 鏇存柊day progress鍜屽ぉ鏁癠I
    /// </summary>
    private void UpdateUI()
    {
        if (moonProgressFill != null)
        {
            moonProgressFill.fillAmount = NormalizedTime;
        }
        if (moonText != null)
        {
            moonText.text = $"Moon {currentMoon}";
        }
    }

    /// <summary>
    /// 鏇存柊DayState骞跺箍鎾璶ewState锛岀敱PanelManager鎺ユ敹鍚庤皟鍑哄搴擯anel
    /// </summary>
    private void SetState(DayState newState)
    {
        if (CurrentState == newState) return;
        CurrentState = newState;
        OnStateChanged?.Invoke(CurrentState);
    }


    // =================== 娴佺▼鎺у埗 ======================

    /// <summary>
    /// 涓�澶╃粨鏉燂紝杩涘叆WaitingFeed
    /// </summary>
    private void EndCurrentMoon()
    {
        if (CurrentState != DayState.Running) return;

        dayPaused = true;       // 涓�澶╃粨鏉熶箣鍚庡崱鐗宑oroutine琚喕缁�
        SetState(DayState.WaitingFeed);
    }

    /// <summary>
    /// UI璋冪敤锛氬湪WaitingFeed state涓嬶紝鐜╁鐐瑰嚮鍠傚吇鏉戞皯鎸夐挳
    /// </summary>
    public void RequestFeed()
    {
        if (CurrentState != DayState.WaitingFeed) return;
        InitializeFeedingRuntimeValues();
        SetState(DayState.FeedingAnimation);
        // 姝ゆ椂 FeedingSequenceController 鎺у埗椋熺墿椋炴潵椋炲幓鐨勫姩鐢�
        // 鍔ㄧ敾缁撴潫鍚庤皟鐢� DayManager.Instance.OnFeedingAnimationFinished()
    }


    /// <summary>
    /// 鏈疆缁撶畻鐨勫垵濮嬪�艰瀹氾細
    /// 涓嶅仛浠讳綍鎵ｅ噺锛屾墸鍑忓畬鍏ㄤ氦缁欏姩鐢婚樁娈佃繘琛屻��
    /// </summary>
    private void InitializeFeedingRuntimeValues()
    {
        if (CardManager.Instance == null) return;
        var cm = CardManager.Instance;

        foreach (var c in cm.AllCards)
        {
            if (c == null || c.data == null) continue;

            if (c.data.cardClass == CardClass.Villager)
            {
                // Villager锛氭湰杞鍚冨灏戝氨鐪� CardData.hunger
                int baseHunger = Mathf.Max(0, c.data.hunger);
                c.currentHunger = baseHunger;
            }
            else if (c.data.cardClass == CardClass.Food &&
                     c.data.hasSaturation &&
                     c.data.saturation > 0)
            {
                // Food锛氬鏋滃綋鍓嶆病鏈夊悎鐞嗙殑鍓╀綑鍊硷紝灏辨寜妯℃澘 saturation 閲嶆柊璁惧畾
                if (c.currentSaturation <= 0 || c.currentSaturation > c.data.saturation)
                {
                    c.currentSaturation = c.data.saturation;
                }
            }
        }
    }

    /// <summary>
    /// 鐢� FeedingSequenceController 缁撴潫鍠傞鍔ㄧ敾鍚庤皟鐢�
    /// 缁撶畻鏈夋病鏈変汉娲荤潃锛岃繘鍏ュ搴� UI Panel
    /// </summary>
    public void OnFeedingAnimationFinished()
    {
        if (CurrentState != DayState.FeedingAnimation) return;
        if (CardManager.Instance == null) return;
        var cm = CardManager.Instance;

        lastVillagers.Clear();
        lastHungryVillagers.Clear();
        lastAllFed = false;

        foreach (var c in cm.AllCards)
        {
            if (c == null || c.data == null) continue;

            if (c.data.cardClass == CardClass.Villager)
            {
                lastVillagers.Add(c);
                if (c.currentHunger > 0)
                {
                    lastHungryVillagers.Add(c);
                }
            }
        }

        if (lastVillagers.Count == 0)
        {
            EnterGameOver();
            return;
        }

        lastAllFed = lastHungryVillagers.Count == 0;

        foreach (var v in lastVillagers)
        {
            v.currentHunger = v.data.hunger;
        }

        cm.RecalculateTotals();
        Debug.Log($"[Daymanager]HungryVillagers={lastHungryVillagers.Count}");

        if (lastAllFed)
        {
            SetState(DayState.FeedingResultAllFull);
        }
        else
        {
            SetState(DayState.FeedingResultHungry);
        }
    }

    /// <summary>
    /// UI锛氬湪 FeedingResultAllFullPanel 涓婃寜 鈥滄墍鏈変汉閮藉悆楗变簡鈥�
    /// </summary>
    public void ConfirmAllFedResult()
    {
        if (CurrentState != DayState.FeedingResultAllFull) return;
        SetState(DayState.WaitingNextDay);
    }

    /// <summary>
    /// UI锛氬湪 FeedingResultHungryPanel 涓婃寜 鈥滃晩鍝︹��
    /// </summary>
    public void ConfirmHungryResult()
    {
        if (CurrentState != DayState.FeedingResultHungry) return;
        SetState(DayState.StarvingAnimation);
        // 姝ゆ椂 FeedAnimationController 鍙互鏍规嵁 LastHungryVillagers 鎾�滃彉灏镐綋鈥濆姩鐢汇��
        // 姣忎釜瑕佹鐨勪汉鍦ㄥ姩鐢诲悎閫傜殑鏃舵満璋冪敤 DayManager.KillVillager(v)
        // 鍔ㄧ敾鍏ㄩ儴鎾畬涔嬪悗璋冪敤 DayManager.Instance.OnStarvingAnimationFinished()
    }

    /// <summary>
    /// 鐢� FeedAnimationController 鍦ㄦ挱瀹屽彉灏镐綋鍔ㄧ敾鍚庤皟鐢�
    /// 缁撶畻杩樻湁娌℃湁浜烘椿鐫�銆傝繘鍏ュ搴擯anel
    /// </summary>
    public void OnStarvingAnimationFinished()
    {
        if (CurrentState != DayState.StarvingAnimation) return;
        if (CardManager.Instance == null) return;
        var cm = CardManager.Instance;
        Debug.Log($"[CardManager]Villager={cm.VillagerCards.Count}");

        if (cm.VillagerCards.Count > 0)
        {
            SetState(DayState.WaitingNextDay);
        }
        else
        {
            SetState(DayState.WaitingEndGame);
        }
    }

    /// <summary>
    /// 鍦╓aitingNextDayPanel 鎸� 鈥滃紑濮嬩笅涓�澶┾��
    /// </summary>
    public void RequestNextDay()
    {
        if (CurrentState != DayState.WaitingNextDay) return;

        currentMoon++;
        timer = 0f;

        dayPaused = false;    // 鎭㈠鏃堕棿娴侀�濓紝coroutine鎭㈠
        gameSpeed = 1f;

        SetState(DayState.Running);
        UpdateUI();
    }

    /// <summary>
    /// UI锛氬湪 WaitingEndGame 闈㈡澘鎸� 鈥滅粨鏉熸父鎴忊��
    /// </summary>
    public void RequestEndGame()
    {
        if (CurrentState != DayState.WaitingEndGame) return;
        EnterGameOver();
    }

    private void EnterGameOver()
    {
        dayPaused = true;
        SetState(DayState.GameOver);
    }


    // ============================ Animation Helpers ========================
    /// <summary>
    /// 瑙嗚涓婅鎶婇鐗┾�滃悆鎺夆�濇椂锛岃皟鐢ㄥ嚱鏁癲estroy
    /// </summary>
    public void ConsumeFoodCompletely(Card food)
    {
        if (food == null) return;
        Destroy(food.gameObject);
    }

    /// <summary>
    /// 瑙嗚涓妚illager姝绘帀鏃惰皟鐢ㄥ嚱鏁�
    /// 鍙樻垚灏镐綋鎴栬�卍estroy
    /// </summary>
    public void KillVillager(Card villager)
    {
        if (villager == null) return;
        villager.TakeOutOfStack();

        if (corpseCardData != null)
        {
            villager.data = corpseCardData;
            villager.currentHunger = 0;
            villager.currentSaturation = 0;

            // 璁� Card 鐢ㄦ柊鐨� data 閲嶅埛澶栬
            villager.ApplyData();
        }
        else
        {
            Destroy(villager.gameObject);
        }
    }


    // ========================= Time Scale Control ========================
    private void HandleFastForawrd()
    {
        if (dayPaused)
        {
            gameSpeed = 1f;
            dayPaused = false;
        }
        else
        {
            gameSpeed = gameSpeed == 1 ? 2f : 1f;
        }
        Debug.Log($"[DayManager]Game Speed: {gameSpeed}x");
    }

    private void HandlePause()
    {
        dayPaused = dayPaused ? false : true;
        Debug.Log($"[DayManager]DayPaused: {dayPaused}");
    }
}