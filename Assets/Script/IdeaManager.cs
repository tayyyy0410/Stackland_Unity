using System;
using System.Collections.Generic;
using UnityEngine;

public class IdeaManager : MonoBehaviour
{
    public static IdeaManager Instance { get; private set; }

    [Header("开局就有的 Idea")]
    public List<CardData> startingIdeas = new List<CardData>();

    [Header("运行时已解锁的 Idea（只读）")]
    public List<CardData> unlockedIdeas = new List<CardData>();

    // 内部用来去重
    private HashSet<CardData> unlockedSet = new HashSet<CardData>();

    // UI 用：当解锁新 idea 时通知
    public event Action<CardData> OnIdeaUnlocked;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        unlockedIdeas.Clear();
        unlockedSet.Clear();

       
        foreach (var idea in startingIdeas)
        {
            if (idea == null) continue;
            if (unlockedSet.Add(idea))
            {
                unlockedIdeas.Add(idea);
            }
        }
    }

    public void UnlockIdea(CardData idea)
    {
        if (idea == null) return;

        if (idea.cardClass != CardClass.Idea)
        {
            Debug.LogWarning($"[IdeaManager] UnlockIdea 收到的不是 Idea：{idea.name}");
        }

        if (unlockedSet.Add(idea))
        {
            unlockedIdeas.Add(idea);
            Debug.Log($"[IdeaManager] 解锁新 Idea：{idea.displayName}");
            OnIdeaUnlocked?.Invoke(idea);
        }
    }

    // 给 开包 / 合成 / 其他生成卡牌地方调用：
    public void NotifyIdeaCardCreated(CardData cardData)
    {
        if (cardData == null) return;
        if (cardData.cardClass != CardClass.Idea) return;

        UnlockIdea(cardData);
    }

    public bool IsIdeaUnlocked(CardData idea)
    {
        return idea != null && unlockedSet.Contains(idea);
    }
}