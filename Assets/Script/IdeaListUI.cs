using System.Collections.Generic;
using UnityEngine;

public class IdeaListUI : MonoBehaviour
{
    [Header("ScrollView / Layout")]
    public Transform contentRoot;        // ScrollView 的 Content
    public IdeaItemUI ideaItemPrefab;    // 刚做的 prefab

    [Header("Info Bar")]
    public InfoBarIndep infoBar;

    private Dictionary<CardData, IdeaItemUI> itemMap =
        new Dictionary<CardData, IdeaItemUI>();

    private void Start()
    {
        if (IdeaManager.Instance == null)
        {
            Debug.LogWarning("[IdeaListUI] 没有 IdeaManager.Instance");
            return;
        }

        BuildInitialList();

        // 监听新 idea 解锁事件
        IdeaManager.Instance.OnIdeaUnlocked += HandleIdeaUnlocked;
    }

    private void OnDestroy()
    {
        if (IdeaManager.Instance != null)
        {
            IdeaManager.Instance.OnIdeaUnlocked -= HandleIdeaUnlocked;
        }
    }

    private void BuildInitialList()
    {
        foreach (Transform child in contentRoot)
        {
            Destroy(child.gameObject);
        }
        itemMap.Clear();

        foreach (var idea in IdeaManager.Instance.unlockedIdeas)
        {
            CreateItem(idea);
        }
    }

    private void HandleIdeaUnlocked(CardData idea)
    {
        CreateItem(idea);
    }

    private void CreateItem(CardData idea)
    {
        if (idea == null) return;
        if (itemMap.ContainsKey(idea)) return;

        var item = Instantiate(ideaItemPrefab, contentRoot);
        item.Init(idea, infoBar);
        itemMap.Add(idea, item);
    }
}