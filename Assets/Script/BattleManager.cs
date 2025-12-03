using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 管理所有战斗逻辑
/// </summary>
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [Header("战斗参数")]
    [Tooltip("每次攻击之间的间隔）")]
    public float attackInterval = 0.6f;

    [Tooltip("战斗开始前的延迟时间")]
    public float battleStartDelay = 0.5f;

    [Tooltip("上下阵营之间的垂直距离")]
    public float verticalOffset = 0.7f;

    [Tooltip("同一阵营内部，单位之间的水平间隔")]
    public float horizontalSpacing = 0.6f;

    [Header("生成")]
    [Tooltip("战斗掉落生成用的卡牌 prefab")]
    public GameObject cardPrefab;

    [Tooltip("掉落卡牌相对于敌人位置的偏移")]
    public Vector2 lootOffset = new Vector2(0.7f, 0f);

    private readonly List<BattleInstance> activeBattles = new List<BattleInstance>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// 一场战斗的数据
    /// </summary>
    public class BattleInstance
    {
        // 支持多个村民
        public List<Card> villagers = new List<Card>();
        public Card enemy;
        public bool isRunning;
    }

    /// <summary>
    /// 外部调用draggable card.cs 开始一场战斗
    /// </summary>
    public void StartBattle(Card villager, Card enemy)
    {
        if (villager == null || enemy == null) return;
        if (villager.data == null || enemy.data == null) return;
        if (villager.data.cardClass != CardClass.Villager) return;
        if (enemy.data.cardClass != CardClass.Enemy && enemy.data.cardClass != CardClass.Animals) return;

        villager.EnsureBattleInit();
        enemy.EnsureBattleInit();

        // 如果敌人已经在一场战斗中，就把新的村民加入那场战斗
        BattleInstance existing = enemy.currentBattle;
        if (existing != null)
        {
            if (!existing.villagers.Contains(villager))
            {
                existing.villagers.Add(villager);
                villager.currentBattle = existing;
            }

            AlignBattlePositions(existing);
            string vName = villager.data != null ? villager.data.name : villager.name;
            string eName = enemy.data != null ? enemy.data.name : enemy.name;
            Debug.Log($"[Battle] Join: {vName} 加入对 {eName} 的战斗，当前村民数量 = {existing.villagers.Count}");

            return;
        }

        // 敌人不在战斗中就创建新战斗
        BattleInstance battle = new BattleInstance
        {
            enemy = enemy,
            isRunning = true
        };
        battle.villagers.Add(villager);

        villager.currentBattle = battle;
        enemy.currentBattle = battle;

        activeBattles.Add(battle);

        string firstVName = villager.data != null ? villager.data.name : villager.name;
        string enemyName = enemy.data != null ? enemy.data.name : enemy.name;
        Debug.Log($"[Battle] Start: {firstVName} HP={villager.currentHP}  vs  {enemyName} HP={enemy.currentHP}");

        // 开始协程
        StartCoroutine(RunBattle(battle));
    }

    /// <summary>
    /// 外部调用：当玩家拖走村民时，用这个来中止战斗中的这个村民
    /// 如果这一场没有其他村民，则整场战斗结束
    /// </summary>
    public void StopBattleFor(Card card)
    {
        if (card == null || card.currentBattle == null) return;

        BattleInstance battle = card.currentBattle;
        if (battle == null) return;

        // 如果拖走的是敌人，直接结束整场战斗
        if (card == battle.enemy)
        {
            EndBattle(battle);
            return;
        }

        // 拖走的是村民 -> 从列表中移除这个村民
        if (battle.villagers.Contains(card))
        {
            battle.villagers.Remove(card);
            card.currentBattle = null;
        }

        // 如果没有村民了，整场战斗结束
        if (battle.villagers.Count == 0)
        {
            EndBattle(battle);
        }
        else
        {
            // 还有其他村民在战斗，重新排列站位
            AlignBattlePositions(battle);
        }
    }

   private IEnumerator RunBattle(BattleInstance battle)
    {
        AlignBattlePositions(battle);

        // 战斗开始前的延迟
        if (battleStartDelay > 0f)
        {
            // 这里也可以用 WaitBattleInterval，让暂停/快进生效
            yield return WaitBattleInterval(battleStartDelay);
        }

        while (battle.isRunning && battle.enemy != null && battle.villagers.Count > 0)
        {
            // ====== 新增：如果暂停了，就在这里卡住 ======
            if (IsGamePausedForBattle())
            {
                yield return WaitWhilePaused();

                // 恢复之后保险检查一次
                if (!battle.isRunning || battle.enemy == null || battle.villagers.Count == 0)
                    break;
            }

            // 清理已被 Destroy 或 HP<=0 的村民（保险一次）
            battle.villagers.RemoveAll(v => v == null || v.currentHP <= 0);

            if (battle.villagers.Count == 0 || battle.enemy == null || battle.enemy.currentHP <= 0)
                break;

            // --- 1. 村民方回合：从左到右依次攻击敌人 ---
            battle.villagers.Sort((a, b) =>
            {
                float ax = (a.stackRoot != null ? a.stackRoot.position.x : a.transform.position.x);
                float bx = (b.stackRoot != null ? b.stackRoot.position.x : b.transform.position.x);
                return ax.CompareTo(bx);
            });

            for (int i = 0; i < battle.villagers.Count; i++)
            {
                if (!battle.isRunning) break;

                Card v = battle.villagers[i];
                if (v == null || v.currentHP <= 0) continue;
                if (battle.enemy == null || battle.enemy.currentHP <= 0) break;

                // 这里是打一刀的地方
                yield return AttackOnce(battle, v, battle.enemy);
                if (!battle.isRunning) break;

                // ====== 原来是 WaitForSeconds(attackInterval)，改成： ======
                yield return WaitBattleInterval(attackInterval);
            }

            if (!battle.isRunning || battle.enemy == null || battle.enemy.currentHP <= 0 || battle.villagers.Count == 0)
                break;

            // --- 2. 敌人回合：从现存的村民中挑一个目标攻击 ---
            battle.villagers.RemoveAll(v => v == null || v.currentHP <= 0);
            if (battle.villagers.Count == 0) break;

            Card targetVillager = battle.villagers[Random.Range(0, battle.villagers.Count)];

            yield return AttackOnce(battle, battle.enemy, targetVillager);
            if (!battle.isRunning) break;

            // 同样换成自定义的等待
            yield return WaitBattleInterval(attackInterval);
        }

        EndBattle(battle);
    }


    /// <summary>
    /// 结束一场战斗，清理所有状态
    /// </summary>
    private void EndBattle(BattleInstance battle)
    {
        if (battle == null) return;
        if (!activeBattles.Contains(battle)) return;

        battle.isRunning = false;

        // 清理所有村民的 battle 状态
        foreach (var v in battle.villagers)
        {
            if (v != null)
            {
                v.currentBattle = null;
            }
        }

        // 清理敌人状态
        if (battle.enemy != null)
        {
            battle.enemy.currentBattle = null;
        }

        activeBattles.Remove(battle);
    }
    /// <summary>
    /// 让两个阵营 X 对齐：敌人在上，多个村民在下排成一排
    /// </summary>
    private void AlignBattlePositions(BattleInstance battle)
    {
        if (battle.enemy == null) return;
        if (battle.enemy.stackRoot == null) battle.enemy.stackRoot = battle.enemy.transform;

        // 如果没有村民，只把敌人拉到原来位置的上方就好
        if (battle.villagers.Count == 0)
        {
            return;
        }

        // 确保每个村民都有 stackRoot
        foreach (var v in battle.villagers)
        {
            if (v == null) continue;
            if (v.stackRoot == null) v.stackRoot = v.transform;
        }

        // 以当前敌人位置反推战场中心：
        // 敌人应该在 centerY + verticalOffset * 0.5f
 
        Vector3 enemyPos = battle.enemy.stackRoot.position;
        float centerX = enemyPos.x;
        float centerY = enemyPos.y - verticalOffset * 0.5f;

        // 敌人在上
        battle.enemy.stackRoot.position = new Vector3(
            centerX,
            centerY + verticalOffset * 0.5f,
            battle.enemy.stackRoot.position.z
        );

        // 村民根据数量左右展开，Y 固定在 centerY - verticalOffset * 0.5f
        int count = battle.villagers.Count;
        float totalWidth = (count - 1) * horizontalSpacing;
        float leftX = centerX - totalWidth * 0.5f;
        float villagerY = centerY - verticalOffset * 0.5f;

        for (int i = 0; i < count; i++)
        {
            Card v = battle.villagers[i];
            if (v == null) continue;

            float x = leftX + i * horizontalSpacing;
            Transform root = v.stackRoot != null ? v.stackRoot : v.transform;
            root.position = new Vector3(x, villagerY, root.position.z);
        }
    }


    /// <summary>
    /// attacker 攻击 defender 一次
    /// </summary>
    private IEnumerator AttackOnce(BattleInstance battle, Card attacker, Card defender)
    {
        if (!battle.isRunning || attacker == null || defender == null) yield break;

        // 先播放攻击/受击动画
        yield return PlayAttackAnimation(attacker, defender);

        // 命中判定：假设 hitChance 是 0-100 的整数
        int hitChance = attacker.data.hitChance;   // 改成你真实字段
        int attack = attacker.data.attack;         // 改成你真实字段

        float roll = Random.Range(0f, 100f);
        bool hit = roll < hitChance;

        if (hit)
        {
            defender.currentHP -= attack;
            if (defender.currentHP < 0) defender.currentHP = 0;
        }

        // 检查死亡
        if (defender.currentHP <= 0)
        {
            // Villager 死亡
            if (defender.data.cardClass == CardClass.Villager)
            {
                if (DayManager.Instance != null)
                {
                    DayManager.Instance.KillVillager(defender);
                }
                else
                {
                    Destroy(defender.gameObject);
                }

                // 不在这里改 battle.villagers，交给 RunBattle 里的 RemoveAll 统一清理
            }
            else
            {
                // Enemy / Animals 死亡掉落
                HandleEnemyDeath(defender);

                // 敌人死了，战斗结束
                EndBattle(battle);
            }
        }
    }

    /// <summary>
    /// 敌人死亡时处理：掉落 + 销毁
    /// </summary>
    private void HandleEnemyDeath(Card enemy)
    {
        if (enemy == null) return;

        Vector3 dropPos = enemy.stackRoot != null ? enemy.stackRoot.position : enemy.transform.position;

        // 掉落
        if (enemy.data.hasDeathLoot && enemy.data.deathLootPack != null && cardPrefab != null)
        {
            int count = Random.Range(enemy.data.minDeathLoot, enemy.data.maxDeathLoot + 1);
            if (count < 0) count = 0;

            for (int i = 0; i < count; i++)
            {
                CardData lootData = GetRandomCardFromPack(enemy.data.deathLootPack);
                if (lootData == null) continue;

                Vector3 pos = dropPos + (Vector3)(lootOffset + Random.insideUnitCircle * 0.2f);
                GameObject go = Instantiate(cardPrefab, pos, Quaternion.identity);
                Card card = go.GetComponent<Card>();
                if (card != null)
                {
                    card.data = lootData;
                    card.stackRoot = card.transform;
                    card.EnsureBattleInit(); // 如果是可战斗单位
                    card.ApplyData();        // 更新贴图/数值
                    card.LayoutStack();
                }
            }
        }

        Destroy(enemy.gameObject);
    }

    /// <summary>
    /// 掉落物权重
    /// </summary>
    private CardData GetRandomCardFromPack(PackData packData)
    {
        if (packData == null || packData.entries == null || packData.entries.Count == 0)
            return null;

        int totalWeight = 0;
        foreach (var entry in packData.entries)
        {
            if (entry.cardData == null || entry.weight <= 0) continue;
            totalWeight += entry.weight;
        }

        if (totalWeight <= 0) return null;

        int rand = Random.Range(0, totalWeight);
        int cumulative = 0;
        foreach (var entry in packData.entries)
        {
            if (entry.cardData == null || entry.weight <= 0) continue;

            cumulative += entry.weight;
            if (rand < cumulative)
            {
                return entry.cardData;
            }
        }

        return null;
    }

    //animation
    private IEnumerator PlayAttackAnimation(Card attacker, Card defender)
    {
        if (attacker == null || defender == null) yield break;

        Transform attackerRoot = attacker.stackRoot != null ? attacker.stackRoot : attacker.transform;
        Transform defenderRoot = defender.stackRoot != null ? defender.stackRoot : defender.transform;

        Vector3 attackerStart = attackerRoot.position;
        Vector3 defenderPos = defenderRoot.position;

        // 计算攻击方向
        Vector3 dir = defenderPos - attackerStart;
        float totalDist = dir.magnitude;
        if (totalDist < 0.01f)
        {
            dir = Vector3.right; // 防止两点重合导致除以 0
        }
        else
        {
            dir /= totalDist;
        }

        // 攻击方前冲距离：占总距离 60%，同时限制一个最大值（防止贴得太近）
        float approachDist = Mathf.Min(totalDist * 0.6f, 0.7f);
        Vector3 attackPos = attackerStart + dir * approachDist;

        float moveDurationForward = 0.12f;
        float moveDurationBack = 0.1f;
        float shakeDuration = 0.15f;

        // 收集受击方的 SpriteRenderer，用来闪色
        var renderers = defenderRoot.GetComponentsInChildren<SpriteRenderer>();
        Color[] originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            originalColors[i] = renderers[i].color;
        }

        // 闪一下的颜色
        Color hitColor = Color.red;

        Sequence seq = DOTween.Sequence();

        // 攻击方先冲上去
        seq.Append(
            attackerRoot.DOMove(attackPos, moveDurationForward)
                .SetEase(Ease.OutQuad)
        );

        // 到位置后，让对方抖动 + 闪色
        seq.AppendCallback(() =>
        {
            // 被攻击方抖动
            defenderRoot.DOShakePosition(
                shakeDuration,
                strength: new Vector3(0.12f, 0.12f, 0f),
                vibrato: 20,
                randomness: 90,
                snapping: false,
                fadeOut: true
            );

            // 所有贴图闪一下颜色
            for (int i = 0; i < renderers.Length; i++)
            {
                var sr = renderers[i];
                sr.DOColor(hitColor, shakeDuration * 0.5f)
                  .SetLoops(2, LoopType.Yoyo);
            }
        });

        // 留一点时间让抖动/闪色播放（攻击方此时停在怪面前）
        seq.AppendInterval(shakeDuration * 0.6f);

        // 攻击方退回原位
        seq.Append(
            attackerRoot.DOMove(attackerStart, moveDurationBack)
                .SetEase(Ease.InQuad)
        );

        // 等整套动画播完
        yield return seq.WaitForCompletion();

        // 把色还原一次
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].color = originalColors[i];
            }
        }
        
    }
    
    //战斗暂停相关
    // 游戏是否对战斗来说是暂停
    private bool IsGamePausedForBattle()
    {
        if (DayManager.Instance == null) return false;
        // 如果只想跟随 dayPaused，就用它；
        // 如果你也想在不是 Running 的状态下强制暂停战斗，可以加上 && CurrentState == Running
        return DayManager.Instance.dayPaused;
    }

   // 一直等到不暂停
    private IEnumerator WaitWhilePaused()
    {
        while (IsGamePausedForBattle())
        {
            yield return null;
        }
    }

  // 替代 WaitForSeconds，支持暂停和快进
    private IEnumerator WaitBattleInterval(float baseSeconds)
    {
        float elapsed = 0f;
        while (elapsed < baseSeconds)
        {
            if (!IsGamePausedForBattle())
            {
                float speed = 1f;
                if (DayManager.Instance != null)
                {
                    speed = DayManager.Instance.gameSpeed;  // Tab 快进也会影响战斗节奏
                }

                elapsed += Time.deltaTime * speed;
            }

            yield return null;
        }
    }


}
