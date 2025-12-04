using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 负责在 DayManager 的 FeedingAnimation / StarvingAnimation 状态下播放镜头 + 卡牌动画，
/// 并实时修改 Card.currentHunger / currentSaturation
/// 播放完后会调用 DayManager.OnFeedingAnimationFinished / OnStarvingAnimationFinished
/// 使用 unscaled time，这样即使 timeScale = 0 动画也会正常播放
/// </summary>

public class FeedAnimationController : MonoBehaviour
{
    [Header("Camera")]
    public Camera targetCamera;                 
    public float zoomInSize = 3f;       // 镜头聚焦村民时的 orthographic size
    public float zoomOutSize = 6f;      // 动画结束后恢复的 orthographic size
    public float cameraMoveDuration = 0.5f;     // 镜头移动 + 缩放时长
    public Vector3 cameraOffset = new Vector3(0f, 0f, -10f);

    [Header("Feeding Animation")]
    public float delayBeforeFeeding = 0.3f;     // 进入 FeedingAnimation 后稍等
    public float delayBetweenVillagers = 0.4f;      // 每个 villager 之间间隔
    public float foodMoveDuration = 0.35f;      // 食物飞到 villager 身上的时间
    public float foodHoldDuration = 0.3f;       // 食物停在 villager 身上的时间

    [Header("Starving Animation")]
    public float delayBeforeStarving = 0.3f;        // 进入 StarvingAnimation 后稍等
    public float starvingPerVillagerDelay = 0.6f;       // 每个要死掉的村民的展示时间

    private bool isPlaying = false;
    private Vector3 originalCameraPos;
    private float originalCameraSize;

    private void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null && targetCamera.orthographic)
        {
            originalCameraPos = targetCamera.transform.position;
            originalCameraSize = targetCamera.orthographicSize;
        }

        if (DayManager.Instance != null)
        {
            DayManager.Instance.OnStateChanged += HandleDayStateChanged;
            // 订阅 DayManager 的 State 变化
        }
    }

    private void OnDestroy()
    {
        if (DayManager.Instance != null)
        {
            DayManager.Instance.OnStateChanged -= HandleDayStateChanged;
        }
    }


    // 接收 DayManager 的 State 变化，如果是 AnimationState 就开始镜头动画
    private void HandleDayStateChanged(DayManager.DayState state)
    {
        if (state == DayManager.DayState.FeedingAnimation)
        {
            if (!isPlaying)
            {
                StartCoroutine(PlayFeedingSequence());
            }
        }
        else if (state == DayManager.DayState.StarvingAnimation)
        {
            if (!isPlaying)
            {
                StartCoroutine(PlayStarvingSequence());
            }
        }
    }


    // ================== Feeding Animation ==================

    /// <summary>
    /// 播放喂食动画：依次镜头对准每个 villager
    /// 吃饭同时update currentHunger / currentSaturation
    /// </summary>
    private IEnumerator PlayFeedingSequence()
    {
        if (DayManager.Instance == null || targetCamera == null)
        {
            yield break;
        }

        isPlaying = true;
        var dm = DayManager.Instance;

        // 从场景里重新扫一遍当前的 villager / food

        if (CardManager.Instance == null)
        {
            yield break;
        }
        var cm = CardManager.Instance;

        if (cm.VillagerCards.Count == 0)
        {
            // 没有村民，直接结束
            dm.OnFeedingAnimationFinished();
            isPlaying = false;
            yield break;
        }

        // 备份相机状态
        originalCameraPos = targetCamera.transform.position;
        if (targetCamera.orthographic)
        {
            originalCameraSize = targetCamera.orthographicSize;
        }

        // 等一点时间，让 UI 切换好
        yield return new WaitForSecondsRealtime(delayBeforeFeeding);

        // 对每个 villager 依次聚焦 + 食物飞过去
        foreach (Card villager in cm.VillagerCards)
        {
            if (cm.FoodCards.Count == 0) break;
            if (villager == null) continue;

            if (villager.currentHunger <= 0)
                continue;

            // 镜头对准这个 villager
            yield return MoveCameraToTarget(villager.transform.position);
            

            // 只要这个人还没吃饱且还有可用食物，就一口一口吃
            while (villager.currentHunger > 0)
            {
                // 找第一个还有饱腹值的食物
                Card food = null;
                for (int i = 0; i < cm.FoodCards.Count; i++)
                {
                    if (cm.FoodCards[i] != null && cm.FoodCards[i].currentSaturation > 0)
                    {
                        food = cm.FoodCards[i];
                        break;
                    }
                }

                if (food == null)
                {
                    // 没有可用食物了，这个 villager 后面也吃不到了
                    break;
                }

                // 这一口能吃多少饱腹值
                int eatAmount = Mathf.Min(villager.currentHunger, food.currentSaturation);
                food.TakeOutOfStack();

                // 播放一口吃饭的动画，并在动画中扣 currentSaturation 和 currentHunger
                yield return AnimateFoodBite(food, villager, eatAmount);

                // 检查是否还有任何食物
                if (cm.FoodCards.Count == 0 || cm.TotalSaturation == 0)
                {
                    break;
                }

                // 每两口之间稍微停一下
                yield return new WaitForSecondsRealtime(0.1f);
            }

            // 每个 villager 之间留一点时间
            yield return new WaitForSecondsRealtime(delayBetweenVillagers);
        }

        // 镜头拉回
        yield return MoveCameraTo(originalCameraPos,
            (originalCameraSize > 0 && targetCamera.orthographic) ? originalCameraSize : zoomOutSize);

        // 重新layout场景中所有卡牌
        foreach (Card c in cm.AllCards)
        {
            if (c.transform == c.stackRoot)
            {
                c.LayoutStack();

                if (!c.HasMovedDuringFeed)
                {
                    var dc = c.GetComponent<DraggableCard>();
                    dc.TryStackOnOtherCard();
                }
            }
        }

        // 此时 currentHunger / currentSaturation 已经更新完成，等待结算本轮
        // 通知 DayManager 动画完成，切换 State
        dm.OnFeedingAnimationFinished();

        isPlaying = false;
    }


    /// <summary>
    /// 一口吃饭：食物飞到 villager 身边，扣数值，hover停留一下，吃光就 Destroy，否则飞回原位
    /// </summary>
    private IEnumerator AnimateFoodBite(Card food, Card villager, int eatAmount)
    {
        if (food == null || villager == null) yield break;

        Transform foodTf = food.transform;
        Vector3 originPos = foodTf.position;
        Vector3 targetPos = villager.transform.position; //+ foodOffsetOnVillager;

        float t = 0f;

        // 确保食物始终在villager上层显示
        var villagerSR = villager.GetComponent<SpriteRenderer>();
        var foodSR = food.GetComponent<SpriteRenderer>();
        int villagerSO = villagerSR.sortingOrder;

        int temp = foodSR.sortingOrder;
        foodSR.sortingOrder = villagerSO + 1;

        food.HasMovedDuringFeed = true;

        // 飞过去
        while (t < foodMoveDuration)
        {
            t += Time.unscaledDeltaTime;
            float lerp = Mathf.Clamp01(t / foodMoveDuration);
            if (foodTf != null)
            {
                foodTf.position = Vector3.Lerp(originPos, targetPos, lerp);
            }
            yield return null;
        }

        if (foodTf != null)
        {
            foodTf.position = targetPos;
        }

        // 飞到villager脸上后改变 food的饱腹值 和 villager的饥饿值
        villager.currentHunger = Mathf.Max(0, villager.currentHunger - eatAmount);
        food.ChangeSaturation(eatAmount);

        // 停留一下
        yield return new WaitForSecondsRealtime(foodHoldDuration);

        bool isDepleted = food.currentSaturation <= 0;

        if (isDepleted)
        {
            // 食物吃光，销毁卡牌
            DayManager.Instance.ConsumeFoodCompletely(food);
        }
        else
        {
            // 食物还有饱腹值，飞回原位
            t = 0f;
            while (t < foodMoveDuration)
            {
                t += Time.unscaledDeltaTime;
                float lerp = Mathf.Clamp01(t / foodMoveDuration);
                if (foodTf != null)
                {
                    foodTf.position = Vector3.Lerp(targetPos, originPos, lerp);
                }
                yield return null;
            }

            if (foodTf != null)
            {
                foodTf.position = originPos;
            }

            foodSR.sortingOrder = temp;
        }
    }


    // ================== Starving Animation ==================

    private IEnumerator PlayStarvingSequence()
    {
        if (DayManager.Instance == null || targetCamera == null)
        {
            yield break;
        }

        isPlaying = true;
        var dm = DayManager.Instance;

        // 从 DayManager 拿到本轮要饿死的村民列表
        List<Card> hungryVillagers = new List<Card>(dm.LastHungryVillagers);

        // 备份相机
        originalCameraPos = targetCamera.transform.position;
        if (targetCamera.orthographic)
        {
            originalCameraSize = targetCamera.orthographicSize;
        }

        // 等一点时间让 UI 切换好
        yield return new WaitForSecondsRealtime(delayBeforeStarving);

        foreach (Card villager in hungryVillagers)
        {
            if (villager == null) continue;

            // 镜头对准这个 villager
            yield return MoveCameraToTarget(villager.transform.position);

            // 停顿一下
            yield return new WaitForSecondsRealtime(starvingPerVillagerDelay * 0.5f);

            // 调用 DayManager，把这个 villager 变尸体 / Destroy
            DayManager.Instance.KillVillager(villager);

            // 再等一会儿再去下一个 villager
            yield return new WaitForSecondsRealtime(starvingPerVillagerDelay * 0.5f);
        }

        // 镜头拉回
        yield return MoveCameraTo(originalCameraPos,
            (originalCameraSize > 0 && targetCamera.orthographic) ? originalCameraSize : zoomOutSize);

        // 通知 DayManager 动画完成，切换 State
        DayManager.Instance.OnStarvingAnimationFinished();

        isPlaying = false;
    }


    // ================== Camera Movement Helpers ==================

    private IEnumerator MoveCameraToTarget(Vector3 worldTarget)
    {
        Vector3 camTargetPos = new Vector3(worldTarget.x, worldTarget.y, 0f) + cameraOffset;
        float targetSize = (targetCamera.orthographic && zoomInSize > 0f) ? zoomInSize : 0f;
        return MoveCameraTo(camTargetPos, targetSize);
    }

    private IEnumerator MoveCameraTo(Vector3 camTargetPos, float targetSize)
    {
        if (targetCamera == null) yield break;

        Vector3 startPos = targetCamera.transform.position;
        float startSize = targetCamera.orthographic ? targetCamera.orthographicSize : 0f;
        float t = 0f;

        while (t < cameraMoveDuration)
        {
            t += Time.unscaledDeltaTime;
            float lerp = Mathf.Clamp01(t / cameraMoveDuration);

            targetCamera.transform.position = Vector3.Lerp(startPos, camTargetPos, lerp);

            if (targetCamera.orthographic && targetSize > 0f)
            {
                targetCamera.orthographicSize = Mathf.Lerp(startSize, targetSize, lerp);
            }

            yield return null;
        }

        targetCamera.transform.position = camTargetPos;
        if (targetCamera.orthographic && targetSize > 0f)
        {
            targetCamera.orthographicSize = targetSize;
        }
    }
}
