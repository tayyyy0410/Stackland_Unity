using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
        Running,        //正常玩
        WaitingFeed,        //一天结束，等待玩家按 Feed（全局冻结）
        FeedingAnimation,       //进食动画播放中（摄像机依次聚焦村民 + 食物卡飞来飞去）
        FeedingResultAllFull,       //动画结束，本轮所有人都吃饱的结算 UI
        FeedingResultHungry,        //动画结束，有人没吃饱的结算 UI（显示“啊哦”）
        StarvingAnimation,      //点击“啊哦”后，没吃饱的人一个个变尸体的动画
        WaitingNextDay,     //还有活人，等待点击“开始下一天”
        WaitingEndGame,     //死亡动画播完，死光了，等待点击“结束游戏”
        GameOver        //真正的GameOver结算页面
    }

    [Header("Moon Settings")]
    [Tooltip("每个 Moon 的长度（秒）")]
    public float moonLength = 120f;

    [Tooltip("开始时是第几个 Moon（一般是 1）")]
    public int startMoon = 1;

    [Header("Day Progress UI")]     //day progress的读条
    [Tooltip("显示 Moon 进度的 Image，Type 要改成 Filled, Horizontal")]
    public Image moonProgressFill;

    [Tooltip("显示当前 Moon 文本，比如 `Moon 1`")]
    public TMP_Text moonText;

    [Header("Villager & Food")]
    [Tooltip("Villager 饿死之后变成的尸体卡（可以为空，为空则直接 Destroy）")]
    public CardData corpseCardData;

    public DayState CurrentState { get; private set; } = DayState.Running;
    public System.Action<DayState> OnStateChanged;

    private int currentMoon;
    private float timer;    //每天的剩余时间

    public int CurrentMoon => currentMoon;
    public float NormalizedTime => Mathf.Clamp01(timer / Mathf.Max(0.01f, moonLength));

    //结算 food 和 villager
    private readonly List<Card> lastVillagers = new List<Card>();
    private readonly List<Card> lastHungryVillagers = new List<Card>();
    private readonly List<Card> lastFoodCards = new List<Card>();
    private bool lastAllFed = false;

    public IReadOnlyList<Card> LastVillagers => lastVillagers;
    public IReadOnlyList<Card> LastHungryVillagers => lastHungryVillagers;
    public IReadOnlyList<Card> LastFoodCards => lastFoodCards;


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

        Time.timeScale = 1f;    //时间流逝速度
        SetState(DayState.Running);

        UpdateUI();
    }


    private void Update()
    {
        if (CurrentState != DayState.Running) return;
        if (moonLength <= 0f) return;

        timer += Time.deltaTime;

        if (timer >= moonLength)    //一天倒计时结束
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

    private void UpdateUI()     //更新day progress和天数UI
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

        Time.timeScale = 0f;       //一天结束之后卡牌操作被冻结
        SetState(DayState.WaitingFeed);
    }

    /// <summary>
    /// UI调用：在WaitingFeed state下，玩家点击喂养村民按钮
    /// </summary>
    public void RequestFeed()
    {
        if (CurrentState != DayState.WaitingFeed) return;
        InitializeFeedingRuntimeValues();
        SetState(DayState.FeedingAnimation);
        //此时 FeedingSequenceController 控制食物飞来飞去的动画
        //动画结束后调用 DayManager.Instance.OnFeedingAnimationFinished()
    }


    /// <summary>
    /// 本轮结算的初始值设定：
    /// 不做任何扣减，扣减完全交给动画阶段进行。
    /// </summary>
    private void InitializeFeedingRuntimeValues()
    {
        Card[] allCards = FindObjectsByType<Card>(FindObjectsSortMode.None);

        foreach (var c in allCards)
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
    /// 由 FeedingSequenceController 调用
    /// </summary>
    public void OnFeedingAnimationFinished()
    {
        if (CurrentState != DayState.FeedingAnimation) return;

        lastVillagers.Clear();
        lastHungryVillagers.Clear();
        lastFoodCards.Clear();
        lastAllFed = false;

        Card[] allCards = FindObjectsByType<Card>(FindObjectsSortMode.None);

        foreach (var c in allCards)
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
            else if (c.data.cardClass == CardClass.Food &&
                c.data.hasSaturation &&
                c.data.saturation > 0 &&
                c.currentSaturation > 0)
            {
                lastFoodCards.Add(c);
            }
        }

        if (lastVillagers.Count == 0)
        {
            EnterGameOver();
            return;
        }

        lastAllFed = lastHungryVillagers.Count == 0;

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
    /// UI：在 FeedingResultAllFullPanel 上按“所有人都吃饱了”
    /// </summary>
    public void ConfirmAllFedResult()
    {
        if (CurrentState != DayState.FeedingResultAllFull) return;
        SetState(DayState.WaitingNextDay);
    }

    /// <summary>
    /// UI：在 FeedingResultHungryPanel 上按 “啊哦”。
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
    /// 由 FeedAnimationController 在播完变尸体动画后调用。
    /// </summary>
    public void OnStarvingAnimationFinished()
    {
        if (CurrentState != DayState.StarvingAnimation) return;

        bool anyLiving = false;
        foreach (var c in FindObjectsByType<Card>(FindObjectsSortMode.None))
        {
            if (c != null && c.data != null && c.data.cardClass == CardClass.Villager)
            {
                anyLiving = true;
                break;
            }
        }

        if (anyLiving)
        {
            SetState(DayState.WaitingNextDay);
        }
        else
        {
            SetState(DayState.WaitingEndGame);
        }
    }

    /// <summary>
    /// 在WaitingNextDayPanel 按“开始下一天
    /// </summary>
    public void RequestNextDay()
    {
        if (CurrentState != DayState.WaitingNextDay) return;

        currentMoon++;
        timer = 0f;

        Time.timeScale = 1f;    //恢复冻结状态
        SetState(DayState.Running);
        UpdateUI();
    }

    /// <summary>
    /// UI：在 WaitingEndGame 面板按“结束游戏”。
    /// </summary>
    public void RequestEndGame()
    {
        if (CurrentState != DayState.WaitingEndGame) return;
        EnterGameOver();
    }

    private void EnterGameOver()
    {
        Time.timeScale = 0f;
        SetState(DayState.GameOver);
    }



    //不用管这个了

    // ==================== Feeding Logic =======================
    /// <summary>
    /// 这里先不搞动画，纯逻辑结算
    /// 不 destroy 卡牌，留给其他 Controller 处理
    /// </summary>
    /*private void ComputeFeedingLogic()
    {
        lastVillagers.Clear();
        lastHungryVillagers.Clear();
        lastFoodCards.Clear();
        lastAllFed = false;

        Card[] allCards = FindObjectsByType<Card>(FindObjectsSortMode.None);

        List<Card> villagers = new List<Card>();
        List<Card> foods = new List<Card>();

        foreach (var c in allCards)
        {
            if (c == null || c.data == null) continue;

            if (c.data.cardClass == CardClass.Villager)
            {
                int baseHunger = Mathf.Max(0, c.data.hunger);
                c.currentHunger = baseHunger;
                villagers.Add(c);
            }
            else if (c.data.cardClass == CardClass.Food && c.data.hasSaturation && c.data.saturation > 0)
            {
                if (c.currentSaturation <= 0 || c.currentSaturation > c.data.saturation)
                {
                    c.currentSaturation = c.data.saturation;
                }

                if (c.currentSaturation > 0)
                {
                    foods.Add(c);
                }
            }
        }

        //备份
        lastVillagers.AddRange(villagers);
        lastFoodCards.AddRange(foods);

        if (villagers.Count == 0)
        {
            lastAllFed = false;
            lastHungryVillagers.Clear();
            return;
        }

        // 用队列来分配食物（不 Destroy，只改 currentSaturation）
        Queue<Card> foodQueue = new Queue<Card>(foods);

        foreach (var v in villagers)
        {
            while (v.currentHunger > 0 && foodQueue.Count > 0)
            {
                Card food = foodQueue.Peek();

                if (food == null || food.data == null)
                {
                    foodQueue.Dequeue();
                    continue;
                }

                if (food.currentSaturation <= 0)
                {
                    foodQueue.Dequeue();
                    continue;
                }

                int eat = Mathf.Min(v.currentHunger, food.currentSaturation);
                v.currentHunger -= eat;
                food.currentSaturation -= eat;

                // 食物被吃光，但此处不 Destroy，只留给动画阶段处理
                if (food.currentSaturation <= 0)
                {
                    foodQueue.Dequeue();
                }
            }

            if (foodQueue.Count == 0)
            {
                // 没食物了，后面的都吃不到
                break;
            }
        }

        // 统计谁没吃饱
        foreach (var v in villagers)
        {
            if (v.currentHunger > 0)
                lastHungryVillagers.Add(v);
        }

        lastAllFed = lastHungryVillagers.Count == 0;
    }
    */



    // ============================ animation helpers ========================
    /// <summary>
    /// 视觉上要把食物“吃掉”时，调用函数destroy
    /// </summary>
    public void ConsumeFoodCompletely(Card food)
    {
        if (food == null) return;
        Destroy(food.gameObject);
    }

    /// <summary>
    /// 视觉上villager饿死时，调用函数
    /// 变成尸体或者destroy
    /// </summary>
    public void KillVillager(Card villager)
    {
        if (villager == null) return;

        if (corpseCardData != null)
        {
            villager.data = corpseCardData;
            villager.currentHunger = 0;
            villager.currentSaturation = 0;

            // 让 Card 用新的 data 重刷外观
            villager.ApplyData();
        }
        else
        {
            Destroy(villager.gameObject);
        }
    }

}