using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Threading;

/// <summary>
/// 管理天数，每天的倒计时条，每天结束之后的feed结算
/// CardData 里的 saturation 和 hunger 分别对应食物的默认饱腹值，和村民的每天需要的饱腹值（饥饿值）
/// Card 新增 currentSaturation 和 currentHunger 为当前饱腹值和饥饿值，用于结算
/// 配合 PanelManager 食用（），UI都归PanelManager管，这里只管一个每天的读条倒计时
/// </summary>

public class DayManager : MonoBehaviour
{
    public static DayManager Instance { get; private set; }

    public enum DayState
    {
        Running,        // 正常玩
        WaitingFeed,        // 一天结束，等待玩家按 Feed（全局冻结）
        FeedingAnimation,       // 进食动画播放中（摄像机依次聚焦村民 + 食物卡飞来飞去）
        FeedingResultAllFull,       // 动画结束，本轮所有人都吃饱的结算 UI
        FeedingResultHungry,        // 动画结束，有人没吃饱的结算 UI（显示“啊哦”）
        StarvingAnimation,      // 点击“啊哦”后，没吃饱的人一个个变尸体的动画
        WaitingSell,        // 等待点击 “出售卡牌”
        Selling,        // 售卖多出的卡牌
        WaitingNextDay,     // 还有活人，等待点击 “开始下一天”
        WaitingEndGame,     // 死亡动画播完，死光了，等待点击“结束游戏”
        GameOver        // 真正的GameOver结算页面
    }

    [Header("Moon Settings")]
    [Tooltip("每个 Moon 的长度（秒）")]
    public float moonLength = 120f;

    [Tooltip("开始时是第几个 Moon（一般是 1）")]
    public int startMoon = 1;

    [Header("Day Progress UI")]     // day progress的读条
    [Tooltip("显示 Moon 进度的 Image，Type 要改成 Filled, Horizontal")]
    public Image moonProgressFill;

    [Tooltip("running状态下显示卡牌数据统计")]     // ItemBar
    public TMP_Text foodCountText;
    public TMP_Text coinCountText;
    public TMP_Text cardCountText;
    public float flashInterval;     // 字体闪烁间隔
    public Image foodCountIcon;
    public Image coinCountIcon;
    public Image cardCountIcon;


    [Tooltip("显示当前 Moon 文本，比如 `Moon 1`")]
    public TMP_Text moonText;
    public TMP_Text moonTextInRun;
    public TMP_Text moonTextInFeeding;
    public TMP_Text moonTextInFed;
    public TMP_Text moonTextInHungary;
    public TMP_Text moonTextInSell;
    public TMP_Text moonTextInNext;
    public TMP_Text moonTextInOver;
    public TMP_Text moonTextInEnd;

    [Tooltip("显示当前多出的卡牌数量")]
    public TMP_Text cardSellText;
    public TMP_Text cardSellButtonText;
    public TMP_Text cardSellingText;

    [Tooltip("“所有人都吃饱了”页面展示时长")]
    public float allFedResultDuration;

    [Header("Villager & Food")]
    [Tooltip("Villager 饿死之后变成的尸体卡（可以为空，为空则直接 Destroy）")]
    public CardData corpseCardData;
    public Card cardPrefab;
    [Tooltip("尸体卡掉落位移")]
    public Vector3 corpseOffset;

    [Header("Time Scale")]
    public float gameSpeed = 1f;
    public bool dayPaused = false;
    public GameObject pauseIcon;
    public GameObject monoSpeedIcon;
    public GameObject doubleSpeedIcon;
    public GameObject pauseBack;
    
    
    [Header("Starter Pack Time Lock")]
    public bool lockTimeUntilStarterPacksOpened = true;

    // 还有多少个标记为 starter 的卡包没开完
    private int starterPacksBlockingTime = 0;

    public DayState CurrentState { get; private set; } = DayState.Running;
    public System.Action<DayState> OnStateChanged;

    private int currentMoon;
    private float timer;    // 每天的时间
    private bool useRed = false;

    public int CurrentMoon => currentMoon;
    public float NormalizedTime => Mathf.Clamp01(timer / Mathf.Max(0.01f, moonLength));

    // 结算 food 和 villager
    private readonly List<Card> lastVillagers = new List<Card>();
    private readonly List<Card> lastHungryVillagers = new List<Card>();
    private bool lastAllFed = false;
    public TMP_Text hungerNumText;

    public IReadOnlyList<Card> LastVillagers => lastVillagers;
    public IReadOnlyList<Card> LastHungryVillagers => lastHungryVillagers;  // UI: 调用多少个村民挨饿 LastHungryVillager.Count


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        InitDay();

        monoSpeedIcon.SetActive(true);

        pauseBack.SetActive(false);
        pauseIcon.SetActive(false);
        doubleSpeedIcon.SetActive(false);

        foodCountText.alignment = TextAlignmentOptions.Right;
        coinCountText.alignment = TextAlignmentOptions.Right;
        cardCountText.alignment = TextAlignmentOptions.Right;
    }


    private void InitDay()
    {
        currentMoon = Mathf.Max(1, startMoon);
        timer = 0f;

        gameSpeed = 1f;
        flashInterval = 0.5f;
        dayPaused = false;
        allFedResultDuration = 2f;
        corpseOffset = new Vector3(0f, 1f, 0f);

        SetState(DayState.Running);

        UpdateProgressUI();
    }


    private void Update()
    {
        if (CurrentState == DayState.Running)
        {
            UpdateRunning();
        }
        else if (CurrentState == DayState.Selling)
        {
            UpdateSelling();
        }
        else if (CurrentState == DayState.FeedingAnimation || 
                 CurrentState == DayState.FeedingResultAllFull || 
                 CurrentState == DayState.FeedingResultHungry || 
                 CurrentState == DayState.WaitingSell ||
                 CurrentState == DayState.WaitingNextDay ||
                 CurrentState == DayState.WaitingEndGame ||
                 CurrentState == DayState.GameOver)
        {
            UpdateBarDate();
        }
   
    }

    private void UpdateBarDate()
    {
        if (CurrentState == DayState.FeedingAnimation && moonTextInFeeding != null)
        {
            moonTextInFeeding.text = currentMoon.ToString();
        }
        else if (CurrentState == DayState.FeedingResultAllFull && moonTextInFed != null)
        {
            moonTextInFed.text = currentMoon.ToString();
        }
        else if (CurrentState == DayState.FeedingResultHungry && moonTextInHungary != null)
        {
            moonTextInHungary.text = currentMoon.ToString();
            UpdateHungerStatus();
        }
        else if (CurrentState == DayState.WaitingSell && moonTextInSell != null)
        {
            moonTextInSell.text = currentMoon.ToString();
            UpdateSellStatus();
        }
        else if (CurrentState == DayState.WaitingNextDay && moonTextInNext != null)
        {
            moonTextInNext.text = (currentMoon+1).ToString();
        }
        else if (CurrentState == DayState.WaitingEndGame && moonTextInOver != null)
        {
            moonTextInOver.text = currentMoon.ToString();
        }
        else if (CurrentState == DayState.GameOver && moonTextInEnd != null)
        {
            moonTextInEnd.text = $"You reached Moon {currentMoon}";

        }
    }
    
    
    public void RegisterStarterPack()
    {
        starterPacksBlockingTime++;
        // Debug.Log($"[DayManager] 注册 StarterPack，剩余 = {starterPacksBlockingTime}");
    }
    
    
    public void NotifyStarterPackFullyOpened()
    {
        starterPacksBlockingTime = Mathf.Max(0, starterPacksBlockingTime - 1);
        // Debug.Log($"[DayManager] StarterPack 开完，剩余 = {starterPacksBlockingTime}");
    }

    // 当前是否应该因为初始包而锁时间
    private bool IsLockedByStarterPacks()
    {
        return lockTimeUntilStarterPacksOpened && starterPacksBlockingTime > 0;
    }

    private void UpdateHungerStatus()
    {

          hungerNumText.text = $"There is not enough food..{lastHungryVillagers.Count} Human will starve of Hunger";
         

    }

    private void UpdateSellStatus()
    {
        cardSellText.text = $"You have {CardManager.Instance.CardToSellCount} Cards too many!";
        cardSellButtonText.text = $"Sell {CardManager.Instance.CardToSellCount} Cards";
    }

    private void UpdateRunning()
    {
        if (moonLength <= 0f) return;

        if (!dayPaused && !IsLockedByStarterPacks())
        {
            timer += Time.deltaTime * gameSpeed;
        }

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


        if (timer >= moonLength)    // 一天倒计时结束
        {
            timer = moonLength;
            UpdateProgressUI();
            EndCurrentMoon();
        }
        else
        {
            UpdateProgressUI();
        }
    }


    private void UpdateSelling()
    {
        if (CardManager.Instance == null) return;

        cardSellingText.text = $"You have {CardManager.Instance.CardToSellCount} Cards too many!";

        if (CardManager.Instance.NonCoinCount > CardManager.Instance.MaxCardCapacity) return;

        SetState(DayState.WaitingNextDay);
    }

    /// <summary>
    /// 更新 RUNNING STATE 下的 day progress 和 天数UI
    /// </summary>
    private void UpdateProgressUI()
    {
        // 因为多个text闪烁的颜色是同步的，所以不单独写method，而是储存一个颜色，符合条件时调用
        int phase = Mathf.FloorToInt(Time.time / flashInterval);
        useRed = (phase % 2 == 0);
        //Debug.Log($"[DayManager]useRed={useRed}");

        if (moonProgressFill != null)
        {
            moonProgressFill.fillAmount = NormalizedTime;
        }

        if (moonText != null)
        {
            moonText.text = $"Moon {currentMoon}";
            moonTextInRun.text = currentMoon.ToString();
        }

        if (CardManager.Instance == null) return;
        var cm = CardManager.Instance;

        if (foodCountText != null && foodCountIcon != null)
        {
            foodCountText.text = $"{cm.TotalSaturation}/{cm.TotalHunger}";

            if (cm.TotalSaturation < cm.TotalHunger)
            {
                foodCountText.color = useRed ? Color.red : Color.black;
                foodCountIcon.color = useRed ? Color.red : Color.black;
            }
            else
            {
                foodCountText.color = Color.black;
                foodCountIcon.color = Color.black;
            }
        }

        if (coinCountText != null)
        {
            coinCountText.text = $"{cm.CoinCount}";
        }

        if (cardCountText != null && cardCountIcon != null)
        {
            cardCountText.text = $"{cm.NonCoinCount}/{cm.MaxCardCapacity}";

            if (cm.NonCoinCount > cm.MaxCardCapacity)
            {
                cardCountText.color = useRed ? Color.red : Color.black;
                cardCountIcon.color = useRed ? Color.red : Color.black;
            }
            else
            {
                cardCountText.color = Color.black;
                cardCountIcon.color = Color.black;
            }
        }
    }


    /// <summary>
    /// 更新 DayState 并广播 newState，由 PanelManager 接收后调出对应Panel
    /// </summary>
    private void SetState(DayState newState)
    {
        if (CurrentState == newState) return;
        CurrentState = newState;
        OnStateChanged?.Invoke(CurrentState);
    }


    // =================== 流程控制 ======================

    /// <summary>
    /// 一天结束，进入WaitingFeed
    /// </summary>
    private void EndCurrentMoon()
    {
        if (CurrentState != DayState.Running) return;

        dayPaused = true;       // 一天结束之后卡牌coroutine被冻结
        SetState(DayState.WaitingFeed);

        //摇铃音效
        if (AudioManager.I != null && AudioManager.I.bellSfx != null)
        {
            AudioManager.I.PlaySFX(AudioManager.I.bellSfx);
        }
    }

    /// <summary>
    /// UI调用：在WaitingFeed state下，玩家点击喂养村民按钮
    /// </summary>
    public void RequestFeed()
    {
        if (CurrentState != DayState.WaitingFeed) return;
        InitializeFeedingRuntimeValues();
        SetState(DayState.FeedingAnimation);
        // 此时 FeedingSequenceController 控制食物飞来飞去的动画
        // 动画结束后调用 DayManager.Instance.OnFeedingAnimationFinished()
    }


    /// <summary>
    /// 本轮结算的初始值设定：
    /// 不做任何扣减，扣减完全交给动画阶段进行。
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
                // Villager：本轮要吃多少就看 CardData.hunger
                int baseHunger = Mathf.Max(0, c.data.hunger);
                c.currentHunger = baseHunger;
            }
            else if (c.data.cardClass == CardClass.Food &&
                     c.data.hasSaturation &&
                     c.data.saturation > 0)
            {
                // Food：如果当前没有合理的剩余值，就按模板 saturation 重新设定
                if (c.currentSaturation <= 0 || c.currentSaturation > c.data.saturation)
                {
                    c.currentSaturation = c.data.saturation;
                }
            }
        }
    }

    /// <summary>
    /// 由 FeedingSequenceController 结束喂食动画后调用
    /// 结算有没有人活着，进入对应 UI Panel
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

        if (lastAllFed)
        {
            SetState(DayState.FeedingResultAllFull);
            Invoke(nameof(ConfirmAllFedResult), allFedResultDuration);
        }
        else
        {
            SetState(DayState.FeedingResultHungry);
        }
    }

    /// <summary>
    /// UI：在 FeedingResultAllFullPanel 上按 “所有人都吃饱了”
    /// </summary>
    public void ConfirmAllFedResult()
    {
        if (CurrentState != DayState.FeedingResultAllFull) return;
        if (CardManager.Instance == null) return;

        if (CardManager.Instance.NonCoinCount > CardManager.Instance.MaxCardCapacity)
        {
            SetState(DayState.WaitingSell);
        }
        else
        {
            SetState(DayState.WaitingNextDay);
        }
    }

    /// <summary>
    /// UI：在 FeedingResultHungryPanel 上按 “啊哦”
    /// </summary>
    public void ConfirmHungryResult()
    {
        if (CurrentState != DayState.FeedingResultHungry) return;
        SetState(DayState.StarvingAnimation);
        // 此时 FeedAnimationController 可以根据 LastHungryVillagers 播“变尸体”动画。
        // 每个要死的人在动画合适的时机调用 DayManager.KillVillager(v)
        // 动画全部播完之后调用 DayManager.Instance.OnStarvingAnimationFinished()
    }

    /// <summary>
    /// 由 FeedAnimationController 在播完变尸体动画后调用
    /// 结算还有没有人活着。进入对应Panel
    /// </summary>
    public void OnStarvingAnimationFinished()
    {
        if (CurrentState != DayState.StarvingAnimation) return;
        if (CardManager.Instance == null) return;

        Debug.Log($"[CardManager] 结束播放死亡动画，Villager={CardManager.Instance.VillagerCards.Count}");

        if (CardManager.Instance.VillagerCards.Count > 0)
        {
            if (CardManager.Instance.NonCoinCount > CardManager.Instance.MaxCardCapacity)
            {
                SetState(DayState.WaitingSell);
            }
            else
            {
                SetState(DayState.WaitingNextDay);
            }
        }
        else
        {
            SetState(DayState.WaitingEndGame);
        }
    }

    /// <summary>
    /// 在 WaitingSellPanel 按 “出售卡牌”
    /// </summary>
    public void RequestSell()
    {
        if (CurrentState != DayState.WaitingSell) return;
        SetState(DayState.Selling);
    }

    /// <summary>
    /// 在WaitingNextDayPanel 按 “开始下一天“
    /// </summary>
    public void RequestNextDay()
    {
        if (CurrentState != DayState.WaitingNextDay) return;

        if (CardManager.Instance != null)
        {
            CardManager.Instance.RefreshVillagerHPBeforeNewMoon();
        }

        currentMoon++;
        timer = 0f;

        dayPaused = false;    // 恢复时间流逝，coroutine恢复
        gameSpeed = 1f;

        SetState(DayState.Running);
        UpdateProgressUI();
    }

    /// <summary>
    /// UI：在 WaitingEndGame 面板按 “结束游戏”
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
    /// 视觉上要把食物“吃掉”时，调用函数destroy
    /// </summary>
    public void ConsumeFoodCompletely(Card food)
    {
        if (food == null) return;
        Destroy(food.gameObject);
    }

    /// <summary>
    /// 视觉上villager死掉时调用函数
    /// 变成尸体或者destroy
    /// </summary>
    public void KillVillager(Card villager)
    {
        if (villager == null) return;
        villager.TakeRootOutOfStack();

        if (corpseCardData != null)
        {
            var spawnPos = villager.GetComponent<Transform>().position + corpseOffset;

            if (cardPrefab == null)
            {
                Debug.Log($"[DayManager] 未设置cardPrefab，无法生成corpse");
            }
            Card corpseCard = Instantiate(cardPrefab, spawnPos, Quaternion.identity);

            corpseCard.data = corpseCardData;
            corpseCard.ApplyData();

            Destroy(villager.gameObject);
        }
        else
        {
            Destroy(villager.gameObject);
        }
    }


    // ========================= Time Scale Control ========================
//    private void HandleFastForawrd()
//    {
//        if (dayPaused)
//        {
//            gameSpeed = 1f;
//            dayPaused = false;
//        }
//        else
//        {
//            gameSpeed = gameSpeed == 1 ? 2f : 1f;
//        }
//        Debug.Log($"[DayManager]Game Speed: {gameSpeed}x");
//    }

//    private void HandlePause()
//    {
//        dayPaused = dayPaused ? false : true;
//        Debug.Log($"[DayManager]DayPaused: {dayPaused}");
//    }


private void HandleFastForawrd()
{
    if (dayPaused)
    {
        gameSpeed = 1f;
        monoSpeedIcon.SetActive(true);
        pauseIcon.SetActive(false);
        pauseBack.SetActive(false);
        dayPaused = false;
    }
    else
    {
        gameSpeed = gameSpeed == 1 ? 2f : 1f;

        if (gameSpeed == 1f)
        {
            monoSpeedIcon.SetActive(true);
            doubleSpeedIcon.SetActive(false);
        }
        else
        {
            monoSpeedIcon.SetActive(false);
            doubleSpeedIcon.SetActive(true);
        }
    }
}

private void HandlePause()
{
    
    bool wasPaused = dayPaused;
    dayPaused = dayPaused ? false : true;

    if (dayPaused)
    {
        
        if (!wasPaused && QuestManager.Instance != null)
        {
            QuestManager.Instance.NotifyPaused();
        }
        pauseIcon.SetActive(true);
        pauseBack.SetActive(true);
        monoSpeedIcon.SetActive(false);
        doubleSpeedIcon.SetActive(false);

    }
    else
    {
        pauseIcon.SetActive(false);
        pauseBack.SetActive(false);

        if (gameSpeed == 1f)
        {
            monoSpeedIcon.SetActive(true);
            doubleSpeedIcon.SetActive(false);
        }
        else
        {
            monoSpeedIcon.SetActive(false);
            doubleSpeedIcon.SetActive(true);
        }
    }
}

public void ButtonControlSpeed()
{
    if (dayPaused)
    {
        gameSpeed = 1f;
        monoSpeedIcon.SetActive(true);
        pauseIcon.SetActive(false);
        pauseBack.SetActive(false);
        dayPaused = false;
    }
    else if (gameSpeed == 1f)
    {
        gameSpeed = 2f;
        monoSpeedIcon.SetActive(false);
        doubleSpeedIcon.SetActive(true);
    }
    else if (gameSpeed == 2f)
    {
        HandlePause();
    }
}




}

