using System.Collections.Generic;
using UnityEngine;

public class QuestListUI : MonoBehaviour
{
    [Header("ScrollView / Layout")]
    public Transform contentRoot;          // ScrollView 的 Content
    public QuestItemUI questItemPrefab;    

    [Header("Sprites")]
    public Sprite checkedSprite;
    public Sprite uncheckedSprite;

    [Header("Info Bar")]
    public InfoBarIndep infoBar;           

    private Dictionary<QuestManager.QuestRuntime, QuestItemUI> itemMap =
        new Dictionary<QuestManager.QuestRuntime, QuestItemUI>();

    private void Start()
    {
        if (QuestManager.Instance == null)
        {
            Debug.LogWarning("[QuestListUI] 没有 QuestManager.Instance");
            return;
        }

        BuildList();

        // 监听任务完成事件
        QuestManager.Instance.OnQuestCompleted += HandleQuestCompleted;
    }

    private void OnDestroy()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted -= HandleQuestCompleted;
        }
    }

    private void BuildList()
    {
        // 清空旧的
        foreach (Transform child in contentRoot)
        {
            Destroy(child.gameObject);
        }
        itemMap.Clear();

        // 按 QuestManager 的 runtimeQuests 顺序生成
        foreach (var qr in QuestManager.Instance.runtimeQuests)
        {
            if (qr == null || qr.data == null) continue;

            var item = Instantiate(questItemPrefab, contentRoot);
            item.Init(qr, infoBar, checkedSprite, uncheckedSprite);
            itemMap[qr] = item;
        }
    }

    private void HandleQuestCompleted(QuestManager.QuestRuntime qr)
    {
        if (qr == null) return;

        QuestItemUI item;
        if (itemMap.TryGetValue(qr, out item) && item != null)
        {
            item.RefreshTick();
        }
    }
}