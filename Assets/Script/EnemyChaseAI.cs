using UnityEngine;
using DG.Tweening;

/// <summary>
/// 敌人自动追击最近的村民：

[RequireComponent(typeof(Card))]
public class EnemyChaseAI : MonoBehaviour
{
    private Card card;
    private Transform root;              
    private Card targetVillager;         // 当前锁定的村民

    [Header("Movement (Juicy Hop)")]
    [Tooltip("一次“蹦”的移动时间（越小越快）")]
    public float hopMoveDuration = 0.12f;

    [Tooltip("每次蹦完在原地等待的时间")]
    public float hopPauseDuration = 0.15f;

    [Tooltip("单次蹦的距离因子：实际步长 = enemyMoveSpeed * hopMoveDuration * moveDistanceFactor")]
    public float moveDistanceFactor = 1.0f;

    [Header("Avoidance Settings")]
    [Tooltip("避障检测的半径")]
    public float avoidRadius = 0.25f;

    [Tooltip("在前方多远距离内检查障碍物")]
    public float avoidDistance = 0.4f;

    [Tooltip("避障时往侧面偏移的强度")]
    public float avoidStrength = 0.7f;

    private Tween moveTween;
    private bool isHopping = false;      // 正在“蹦 + 停顿”的整段过程

    private void Awake()
    {
        card = GetComponent<Card>();
    }

    private void Start()
    {
        UpdateRoot();
    }

    private void OnDestroy()
    {
        if (moveTween != null && moveTween.IsActive())
        {
            moveTween.Kill();
        }
    }

    private void Update()
    {
        if (card == null || card.data == null) return;
        if (DayManager.Instance == null) return;
        
       
        if (DayManager.Instance != null && DayManager.Instance.dayPaused)
        {
            return;
        }
        

        // 只在 Running 状态追击
        if (DayManager.Instance.CurrentState != DayManager.DayState.Running)
            return;

        // 只对 Enemy / Animals，且需要勾上 autoChaseVillagers
        if (card.data.cardClass != CardClass.Enemy &&
            card.data.cardClass != CardClass.Animals)
            return;

        if (!card.data.autoChaseVillagers)
            return;

        // 在战斗中就不要再动了
        if (card.currentBattle != null)
            return;

        UpdateRoot();

        // 当前在做这整段，就等它结束
        if (isHopping)
            return;

        // 没有或失去目标时，重新寻找最近的村民
        if (!IsValidTarget(targetVillager))
        {
            targetVillager = FindNearestVillager();
        }

        if (!IsValidTarget(targetVillager))
        {
            // 场上没有可追击的村民，不动
            return;
        }

        TryStepTowardsTarget(targetVillager);
    }

    private void UpdateRoot()
    {
        if (card.stackRoot != null)
            root = card.stackRoot;
        else
            root = transform;
    }

    private bool IsValidTarget(Card v)
    {
        if (v == null) return false;
        if (v.data == null) return false;
        if (v.data.cardClass != CardClass.Villager) return false;
        if (v.currentHP <= 0) return false;

        if (CardManager.Instance != null &&
            !CardManager.Instance.VillagerCards.Contains(v))
            return false;

        return true;
    }

    /// <summary>
    /// 从 CardManager 里找到最近的村民
    /// </summary>
    private Card FindNearestVillager()
    {
        if (CardManager.Instance == null) return null;

        Card nearest = null;
        float bestDistSqr = float.MaxValue;
        Vector3 pos = root != null ? root.position : transform.position;

        foreach (var v in CardManager.Instance.VillagerCards)
        {
            if (v == null || v.data == null) continue;
            if (v.currentHP <= 0) continue;

            Vector3 vPos = v.stackRoot != null ? v.stackRoot.position : v.transform.position;
            float d2 = (vPos - pos).sqrMagnitude;
            if (d2 < bestDistSqr)
            {
                bestDistSqr = d2;
                nearest = v;
            }
        }

        return nearest;
    }

    /// <summary>
    /// 以节奏向目标迈一步
    /// </summary>
    private void TryStepTowardsTarget(Card v)
    {
        if (root == null) return;

        Vector3 pos = root.position;
        Vector3 targetPos = v.stackRoot != null ? v.stackRoot.position : v.transform.position;

        Vector3 toTarget = targetPos - pos;
        float distance = toTarget.magnitude;

        float triggerDist = card.data.enemyBattleTriggerDistance > 0f
            ? card.data.enemyBattleTriggerDistance
            : 0.2f;

        // 到达触发距离 → 尝试开战
        if (distance <= triggerDist)
        {
            TryStartBattleWith(v);
            return;
        }

        if (distance <= 0.0001f) return;

        Vector2 dir = toTarget.normalized;

        // 前方有其它卡，就稍微往侧面偏一点
        dir = ApplyAvoidance(dir);

        float moveSpeed = card.data.enemyMoveSpeed > 0f ? card.data.enemyMoveSpeed : 1.0f;

        // 单次蹦的理论步长
        float stepDistance = moveSpeed * hopMoveDuration * moveDistanceFactor;

        // 接近目标时缩短步长
        float maxAllowed = Mathf.Max(0f, distance - triggerDist * 0.3f);
        stepDistance = Mathf.Min(stepDistance, maxAllowed);

        if (stepDistance <= 0.0001f)
        {
            // 接近到几乎不能走 → 直接尝试战斗
            TryStartBattleWith(v);
            return;
        }

        Vector3 stepTargetPos = pos + (Vector3)(dir * stepDistance);
        StartHopStep(stepTargetPos);
    }

    /// <summary>
    /// 用 DOTween 
    /// </summary>
    private void StartHopStep(Vector3 stepTargetPos)
    {
        if (moveTween != null && moveTween.IsActive())
        {
            moveTween.Kill();
        }

        isHopping = true;

        Vector3 startPos = root.position;
        stepTargetPos.z = startPos.z;

        // Sequence：先快速移动，再原地等待
        Sequence seq = DOTween.Sequence();

        // 快速冲过去
        seq.Append(root.DOMove(stepTargetPos, hopMoveDuration)
                       .SetEase(Ease.OutQuad));

        // 在新位置原地等一会
        if (hopPauseDuration > 0f)
        {
            seq.AppendInterval(hopPauseDuration);
        }

        seq.OnComplete(() =>
        {
            isHopping = false;
        });

        moveTween = seq;
    }

    private Vector2 ApplyAvoidance(Vector2 desiredDir)
    {
        if (avoidDistance <= 0f || avoidRadius <= 0f)
            return desiredDir;

        RaycastHit2D hit = Physics2D.CircleCast(
            root.position,
            avoidRadius,
            desiredDir,
            avoidDistance
        );

        if (hit.collider == null) return desiredDir;

        // 自己的 stack 忽略
        if (card.stackRoot != null && hit.collider.transform.IsChildOf(card.stackRoot))
            return desiredDir;

        // 目标村民也忽略
        Card hitCard = hit.collider.GetComponent<Card>();
        if (hitCard == null) return desiredDir;
        if (hitCard == targetVillager) return desiredDir;

        // 其余卡视作障碍
        Vector2 perp = Vector2.Perpendicular(desiredDir).normalized;

        // 根据障碍相对位置决定往左还是往右绕，避免永远同一边
        Vector2 toObstacle = (Vector2)(hit.point - (Vector2)root.position);
        float side = Vector2.Dot(perp, toObstacle);
        if (side < 0f) perp = -perp;

        Vector2 newDir = (desiredDir + perp * avoidStrength).normalized;
        return newDir;
    }

    private void TryStartBattleWith(Card villager)
    {
        if (BattleManager.Instance == null) return;
        if (villager == null || villager.data == null) return;

        // 任意一方已经在战斗中就不再重复开战
        if (card.currentBattle != null || villager.currentBattle != null)
            return;

        if (villager.data.cardClass != CardClass.Villager)
            return;

        // 追到了
        BattleManager.Instance.StartBattle(villager, card);
    }
}
