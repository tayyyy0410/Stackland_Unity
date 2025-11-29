using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 负责在 DayManager 的 FeedingAnimation / StarvingAnimation 状态下播放镜头 + 卡牌动画，
/// 并在“吃一口”的时刻实时修改 Card.currentHunger / currentSaturation
/// 播放完后会调用 DayManager.OnFeedingAnimationFinished / OnStarvingAnimationFinished
/// 使用 unscaled time，这样即使 timeScale = 0 动画也会正常播放。
/// </summary>
public class FeedAnimationController : MonoBehaviour
{
    [Header("Camera")]
    public Camera targetCamera;                 
    public float zoomInSize = 4f;              // 聚焦在村民时的 orthographic size
    public float zoomOutSize = 8f;             // 动画结束后恢复的 orthographic size
    public float cameraMoveDuration = 0.5f;    // 镜头移动 + 缩放时长
    public Vector3 cameraOffset = new Vector3(0f, 0f, -10f);

    [Header("Feeding Animation")]
    public float delayBeforeFeeding = 0.3f;        // 进入 FeedingAnimation 后稍等
    public float delayBetweenVillagers = 0.3f;     // 每个 villager 之间间隔
    public float foodMoveDuration = 0.35f;         // 食物飞到 villager 身上的时间
    public float foodHoldDuration = 0.2f;          // 食物停在 villager 身上的时间
    public Vector3 foodOffsetOnVillager = new Vector3(0.3f, 0.3f, 0f);

    [Header("Starving Animation")]
    public float delayBeforeStarving = 0.3f;
    public float starvingPerVillagerDelay = 0.6f;  // 每个没吃饱村民的展示时间

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
        }
    }

    private void OnDestroy()
    {
        if (DayManager.Instance != null)
        {
            DayManager.Instance.OnStateChanged -= HandleDayStateChanged;
        }
    }

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

    // ================== Feeding ==================

    /// <summary>
    /// 播放喂食动画：依次镜头对准每个 villager
    /// 只要还有 currentHunger > 0 且有食物，就一口一口吃
    /// 每一口吃饭的时刻直接扣 currentHunger / currentSaturation
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
        Card[] allCards = FindObjectsByType<Card>(FindObjectsSortMode.None);
        List<Card> villagers = new List<Card>();
        List<Card> foods = new List<Card>();

        foreach (var c in allCards)
        {
            if (c == null || c.data == null) continue;

            if (c.data.cardClass == CardClass.Villager)
            {
                villagers.Add(c);
            }
            else if (c.data.cardClass == CardClass.Food &&
                     c.data.hasSaturation &&
                     c.data.saturation > 0 &&
                     c.currentSaturation > 0)
            {
                foods.Add(c);
            }
        }

        if (villagers.Count == 0)
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

        // 对每个 villager 依次处理
        foreach (Card villager in villagers)
        {
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
                for (int i = 0; i < foods.Count; i++)
                {
                    if (foods[i] != null && foods[i].currentSaturation > 0)
                    {
                        food = foods[i];
                        break;
                    }
                }

                if (food == null)
                {
                    // 没有可用食物了，这个 villager 后面也吃不到了
                    break;
                }

                // 这一口能吃多少
                int eatAmount = Mathf.Min(villager.currentHunger, food.currentSaturation);

                // 播放一口吃饭的动画，并在动画中扣数
                yield return AnimateFoodBite(food, villager, eatAmount);

                // 如果这个食物被吃光了，从列表中移除
                if (food == null || food.currentSaturation <= 0 || food.gameObject == null)
                {
                    foods.Remove(food);
                }

                // 检查是否还有任何食物
                bool anyFoodLeft = false;
                for (int i = 0; i < foods.Count; i++)
                {
                    if (foods[i] != null && foods[i].currentSaturation > 0)
                    {
                        anyFoodLeft = true;
                        break;
                    }
                }
                if (!anyFoodLeft)
                {
                    break;
                }

                // 每两口之间稍微停一下
                yield return new WaitForSecondsRealtime(0.1f);
            }

            // 每个 villager 之间留一点时间
            yield return new WaitForSecondsRealtime(delayBetweenVillagers);
        }

        // 镜头拉回初始位置 / size
        yield return MoveCameraTo(originalCameraPos,
            (zoomOutSize > 0 && targetCamera.orthographic) ? zoomOutSize : originalCameraSize);

        // 此时 currentHunger / currentSaturation 已经是最终状态
        dm.OnFeedingAnimationFinished();

        isPlaying = false;
    }

    /// <summary>
    /// 一口吃饭：食物飞到 villager 身边 → 扣数 → 停留 → 吃光就 Destroy，不然飞回原位。
    /// </summary>
    private IEnumerator AnimateFoodBite(Card food, Card villager, int eatAmount)
    {
        if (food == null || villager == null) yield break;

        Transform foodTf = food.transform;
        Vector3 originPos = foodTf.position;
        Vector3 targetPos = villager.transform.position + foodOffsetOnVillager;

        float t = 0f;

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

        // 到达时扣数
        villager.currentHunger = Mathf.Max(0, villager.currentHunger - eatAmount);
        food.currentSaturation = Mathf.Max(0, food.currentSaturation - eatAmount);

        // 停留一下
        yield return new WaitForSecondsRealtime(foodHoldDuration);

        bool isDepleted = food.currentSaturation <= 0;

        if (isDepleted)
        {
            // 食物吃光，真正销毁
            DayManager.Instance.ConsumeFoodCompletely(food);
        }
        else
        {
            // 飞回原位
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
        }
    }

    // ================== Starving ==================

    private IEnumerator PlayStarvingSequence()
    {
        if (DayManager.Instance == null || targetCamera == null)
        {
            yield break;
        }

        isPlaying = true;
        var dm = DayManager.Instance;

        // 从 DayManager 拿到本轮未吃饱的村民列表
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

            // 停顿一下给玩家看
            yield return new WaitForSecondsRealtime(starvingPerVillagerDelay * 0.5f);

            // 调用 DayManager，把这个 villager 变尸体 / Destroy
            DayManager.Instance.KillVillager(villager);

            // 再等一会儿再去下一个
            yield return new WaitForSecondsRealtime(starvingPerVillagerDelay * 0.5f);
        }

        // 镜头拉回
        yield return MoveCameraTo(originalCameraPos,
            (zoomOutSize > 0 && targetCamera.orthographic) ? zoomOutSize : originalCameraSize);

        // 通知 DayManager：饿死动画播完
        DayManager.Instance.OnStarvingAnimationFinished();

        isPlaying = false;
    }

    // ================== Camera Helpers ==================

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
