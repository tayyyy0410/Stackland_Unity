using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("所有可用任务")]
    public List<QuestData> allQuests = new List<QuestData>();

    public TMP_Text displayText;

    [Serializable]
    public class QuestRuntime
    {
        public QuestData data;
        public int currentCount;
        public bool isCompleted;

        // 给“多结果卡”用（比如 Coordinates 任务需要两张不同卡）
        [NonSerialized] public HashSet<CardData> collectedResults;

        public void EnsureCollectedSet()
        {
            if (collectedResults == null)
                collectedResults = new HashSet<CardData>();
        }
    }

    [Header("运行时任务状态（调试用）")]
    public List<QuestRuntime> runtimeQuests = new List<QuestRuntime>();

    // UI 以后可以订阅
    public Action<QuestRuntime> OnQuestCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitRuntimeQuests();
    }

    // 初始化所有任务的运行时状态
    private void InitRuntimeQuests()
    {
        runtimeQuests.Clear();
        foreach (var q in allQuests)
        {
            if (q == null) continue;

            runtimeQuests.Add(new QuestRuntime
            {
                data = q,
                currentCount = 0,
                isCompleted = false
            });
        }
    }

    // ========== 工具 ==========

    private IEnumerable<QuestRuntime> EachOfType(QuestType type)
    {
        foreach (var qr in runtimeQuests)
        {
            if (qr == null) continue;
            if (qr.isCompleted) continue;
            if (qr.data == null) continue;
            if (qr.data.type != type) continue;

            yield return qr;
        }
    }

    private void AddProgress(QuestRuntime qr, int amount = 1)
    {
        if (qr == null || qr.data == null || qr.isCompleted) return;

        qr.currentCount += amount;
        CheckQuestCompleted(qr);
    }

    private CardData GetPrimaryResultCard(QuestData q)
    {
        if (q == null) return null;
        if (q.resultCards == null || q.resultCards.Count == 0) return null;
        return q.resultCards[0];
    }

    private bool ResultListContains(QuestData q, CardData card)
    {
        if (q == null || card == null) return false;
        if (q.resultCards == null || q.resultCards.Count == 0) return false;
        return q.resultCards.Contains(card);
    }

    private void CheckQuestCompleted(QuestRuntime qr)
    {
        if (qr == null) return;
        if (qr.isCompleted) return;
        if (qr.data == null) return;

        int target = Mathf.Max(1, qr.data.targetCount);
        if (qr.currentCount >= target)
        {
            qr.isCompleted = true;
            Debug.Log($"[Quest] 完成任务：{qr.data.title}");

            OnQuestCompleted?.Invoke(qr);
            // TODO: 在这里接 UI / 播音效 / 奖励
        }
    }

    // ========== 对外接口==========

    /// 打开卡包
    public void NotifyPackOpened(PackData pack)
    {
        if (pack == null) return;

        foreach (var qr in EachOfType(QuestType.OpenPacks))
        {
            var q = qr.data;
            // targetPack 为空 = 任意包
            if (q.targetPack == null || q.targetPack == pack)
            {
                AddProgress(qr);
            }
        }
    }

    /// A 拖到 B 上（Passenger on Berry Bush / Rock / Tree / Soil 等）
    public void NotifyUseCardOnCard(Card subject, Card target)
    {
        if (subject == null || target == null) return;
        if (subject.data == null || target.data == null) return;

        foreach (var qr in EachOfType(QuestType.UseCardOnCard))
        {
            var q = qr.data;

            bool subjectMatch = (q.subjectCard == null || q.subjectCard == subject.data);
            bool targetMatch  = (q.targetCard == null  || q.targetCard == target.data);

            if (subjectMatch && targetMatch)
            {
                AddProgress(qr);
            }
        }
    }

    /// 造出一张卡
    /// - Make a Stick from Wood
    /// - Find the Coordinates（多张不同卡）
    /// - Cook a Christmas Meal
    /// - Build Simple Raft / Rocket / Jet Pack（造出来那一次）
    public void NotifyCardCrafted(CardData cardData)
    {
        if (cardData == null) return;

        foreach (var qr in EachOfType(QuestType.CraftCard))
        {
            var q = qr.data;
            if (q.resultCards == null || q.resultCards.Count == 0)
                continue;

            // 不在要求列表里：忽略
            if (!ResultListContains(q, cardData))
                continue;

            // 特殊：多种结果卡（比如 Coordinates）
            if (q.resultCards.Count > 1)
            {
                // 同一种卡只算一次
                qr.EnsureCollectedSet();
                if (!qr.collectedResults.Contains(cardData))
                {
                    qr.collectedResults.Add(cardData);
                    qr.currentCount = qr.collectedResults.Count;
                    CheckQuestCompleted(qr);
                }
            }
            else
            {
                // 只有一种结果卡，每次 craft 都 +1
                AddProgress(qr);
            }
        }
    }

    /// 卖出卡牌（
    public void NotifyCardSold(CardData soldCard)
    {
        foreach (var qr in EachOfType(QuestType.SellCard))
        {
            var q = qr.data;
            // subjectCard 为空 = 卖任意卡都算
            if (q.subjectCard == null || q.subjectCard == soldCard)
            {
                AddProgress(qr);
            }
        }
    }

    /// 买卡包（Buy the Humble Beginnings Pack）
    public void NotifyPackBought(PackData pack)
    {
        if (pack == null) return;

        foreach (var qr in EachOfType(QuestType.BuyPack))
        {
            var q = qr.data;
            if (q.targetPack == null || q.targetPack == pack)
            {
                AddProgress(qr);
            }
        }
    }

    /// 暂停一次
    public void NotifyPaused()
    {
        foreach (var qr in EachOfType(QuestType.PauseGame))
        {
            AddProgress(qr);
        }
    }

    /// 场上某张卡数量改变
    /// newCount >= targetCount 时直接判定完成
    public void NotifyCardCountChanged(CardData cardType, int newCount)
    {
        if (cardType == null) return;

        foreach (var qr in EachOfType(QuestType.ReachCardCount))
        {
            var q = qr.data;
            if (q.subjectCard != cardType) continue;

            if (newCount >= q.targetCount)
            {
                qr.currentCount = q.targetCount;
                CheckQuestCompleted(qr);
            }
        }
    }

    /// 杀死某种敌人
    public void NotifyEnemyKilled(CardData enemyCard)
    {
        if (enemyCard == null)
        {
            Debug.Log("[Quest] NotifyEnemyKilled: enemyCard 为 null");
            return;
        }

        Debug.Log($"[Quest] NotifyEnemyKilled: 被杀死的敌人 = {enemyCard.name}");

        foreach (var qr in EachOfType(QuestType.KillEnemy))
        {
            var q = qr.data;
            if (q == null) continue;

            // 用整个 resultCards 列表来匹配
            if (ResultListContains(q, enemyCard))
            {
                Debug.Log($"[Quest] KillEnemy 命中任务：{q.title}，目标列表中包含 {enemyCard.name}");
                AddProgress(qr);
            }
            else
            {
                Debug.Log($"[Quest] KillEnemy 未命中任务：{q.title}，目标列表不包含 {enemyCard.name}");
            }
        }
    }


    /// Jet Pack 被装备（“Build and Equip the Jet Pack” 的第二次进度）
    /// 这里假设这条任务的 resultCards 里只有 Jet Pack 一张卡，targetCount = 2
    public void NotifyItemEquipped(CardData equipCard)
    {
        if (equipCard == null) return;

        foreach (var qr in EachOfType(QuestType.CraftCard))
        {
            var q = qr.data;
            if (!ResultListContains(q, equipCard)) continue;

            // 这里直接 +1，让“造出 Jet Pack + 装备 Jet Pack”累计到 2
            AddProgress(qr);
        }
    }

    /// 逃离岛屿（Escape from the Island within X Moon）
    /// 一般在通关那一刻调用
    public void NotifyEscape(CardData escapeCard, int currentMoon)
    {
        foreach (var qr in EachOfType(QuestType.EscapeWithinMoon))
        {
            var q = qr.data;

            var targetResult = GetPrimaryResultCard(q);
            if (targetResult != null && targetResult != escapeCard)
                continue;

            if (q.moonLimit > 0 && currentMoon > q.moonLimit)
                continue;

            qr.currentCount = 1;
            CheckQuestCompleted(qr);
        }
    }

    /// 所有卡包都解锁了（Unlock All Packs）
    public void NotifyAllPacksUnlocked()
    {
        foreach (var qr in EachOfType(QuestType.UnlockAllPacks))
        {
            qr.currentCount = Mathf.Max(1, qr.data.targetCount);
            CheckQuestCompleted(qr);
        }
    }

    /// 某个 Pack 里的卡解锁进度改变（Unlock Every Card from the Developer’s Pack）
    /// 参数：这个 pack 已解锁的卡数 / 总卡数
    public void NotifyCardUnlockedInPack(PackData pack, int unlockedInPack, int totalInPack)
    {
        if (pack == null) return;

        foreach (var qr in EachOfType(QuestType.UnlockAllCardsInPack))
        {
            var q = qr.data;
            if (q.targetPack != pack) continue;

            if (unlockedInPack >= totalInPack)
            {
                qr.currentCount = Mathf.Max(1, q.targetCount);
                CheckQuestCompleted(qr);
            }
        }
    }

    public int GetCompletedQuestCount()
    {
        int count = 0;
        foreach (var qr in runtimeQuests)
        {
            if (qr == null) continue;
            if (qr.isCompleted) count++;
        }
        return count;
    }

    public void Update()
    {
        displayText.text = $"{GetCompletedQuestCount()} Quests Completed";
    }
}
