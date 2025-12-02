using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// 根据DayManager的DayState切换UI Panel/Button
/// 不参与判定state，切换规则在DayManager调用
/// </summary>
public class PanelManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject runningPanel;     // 月亮年进行中
    public GameObject waitingFeedPanel;     // 一天结束，提示“喂养村民”button
    public GameObject feedingAnimationPanel;        // 进食动画
    public GameObject feedingResultAllFullPanel;        // 所有人都吃饱了结算画面
    public GameObject feedingResultHungryPanel;     // 有人没吃饱结算画面
    public GameObject starvingAnimationPanel;       // 有人被饿死的动画
    public GameObject waitingNextDayPanel;      // 有人还活着，等待下一天的button
    public GameObject waitingEndGamePanel;      // 死光了，等待“结束游戏”button
    public GameObject gameOverPanel;        // GameOver结算面板

    [Header("Buttons")]
    public Button feedButton;   // waitingFeedPanel的喂养村民
    public Button AllFedContinueButton;     // FeedingResultAllFullPanel 的 所有人都吃饱了
    public Button hungryContinueButton;     // FeedingResultHungryPanel 的 “啊哦”
    public Button startNextDayButton;   // WaitingNextDayPanel 的 “开始下一天”
    public Button endGameButton;        // WaitingEndGamePanel 的 “结束游戏”

    private void Start()
    {
        // 绑定 Button onClick 和 DayManager 中用来 change state 的 method

        if (feedButton != null)
        {
            feedButton.onClick.AddListener(() =>
            {
                if (DayManager.Instance != null) { DayManager.Instance.RequestFeed(); }
            });
        }

        if (AllFedContinueButton != null)
        {
            AllFedContinueButton.onClick.AddListener(() =>
            {
                if (DayManager.Instance != null) { DayManager.Instance.ConfirmAllFedResult(); }
            });
        }

        if (hungryContinueButton != null)
        {
            hungryContinueButton.onClick.AddListener(() =>
            {
                if (DayManager.Instance != null) { DayManager.Instance.ConfirmHungryResult(); }
            });
        }

        if (startNextDayButton != null)
        {
            startNextDayButton.onClick.AddListener(() =>
            {
                if (DayManager.Instance != null) { DayManager.Instance.RequestNextDay(); }
            });
        }

        if (endGameButton != null)
        {
            endGameButton.onClick.AddListener(() =>
            {
                if (DayManager.Instance != null) { DayManager.Instance.RequestEndGame(); }
            });
        }



        // DayManager 的 CurrentState 改变时更新对应 Panel
        if (DayManager.Instance != null)
        {
            DayManager.Instance.OnStateChanged += HandleDayStateChanged;
            HandleDayStateChanged(DayManager.Instance.CurrentState);
        }
        else
        {
            HandleDayStateChanged(DayManager.DayState.Running);
        }

    }


    private void OnDestroy()
    {
        if (DayManager.Instance != null)
        {
            DayManager.Instance.OnStateChanged -= HandleDayStateChanged;
        }
    }

    // 根据 DayManager 的 State 显示对应 Panel
    private void HandleDayStateChanged(DayManager.DayState state)
    {
        // 先全部关掉
        SetPanelActive(runningPanel, false);
        SetPanelActive(waitingFeedPanel, false);
        SetPanelActive(feedingAnimationPanel, false);
        SetPanelActive(feedingResultAllFullPanel, false);
        SetPanelActive(feedingResultHungryPanel, false);
        SetPanelActive(starvingAnimationPanel, false);
        SetPanelActive(waitingNextDayPanel, false);
        SetPanelActive(waitingEndGamePanel, false);
        SetPanelActive(gameOverPanel, false);

        // 再打开一个 Panel
        switch (state)
        {
            case DayManager.DayState.Running:
                SetPanelActive(runningPanel, true);
                break;

            case DayManager.DayState.WaitingFeed:
                SetPanelActive(waitingFeedPanel, true);
                break;

            case DayManager.DayState.FeedingAnimation:
                SetPanelActive(feedingAnimationPanel, true);
                break;

            case DayManager.DayState.FeedingResultAllFull:
                SetPanelActive(feedingResultAllFullPanel, true);
                break;

            case DayManager.DayState.FeedingResultHungry:
                SetPanelActive(feedingResultHungryPanel, true);
                break;

            case DayManager.DayState.StarvingAnimation:
                SetPanelActive(starvingAnimationPanel, true);
                break;

            case DayManager.DayState.WaitingNextDay:
                SetPanelActive(waitingNextDayPanel, true);
                break;

            case DayManager.DayState.WaitingEndGame:
                SetPanelActive(waitingEndGamePanel, true);
                break;

            case DayManager.DayState.GameOver:
                SetPanelActive(gameOverPanel, true);
                break;
        }
    }

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }

}
