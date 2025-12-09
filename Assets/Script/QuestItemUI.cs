using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class QuestItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Refs")]
    public TMP_Text titleText;
    public Image tickImage;

    [Header("Sprites")]
    public Sprite checkedSprite;
    public Sprite uncheckedSprite;

    [Header("Hover Style")]
    public Color hoverColor = new Color(1f, 0.95f, 0.75f); 

    private QuestManager.QuestRuntime runtime;
    private InfoBarIndep infoBar;

    // 记录原始标题 & 原始颜色，用来在 hover 结束时恢复
    private string originalTitle;
    private Color originalColor;

    // 初始化
    public void Init(QuestManager.QuestRuntime runtime, InfoBarIndep infoBar,
        Sprite checkedSprite, Sprite uncheckedSprite)
    {
        this.runtime = runtime;
        this.infoBar = infoBar;
        this.checkedSprite = checkedSprite;
        this.uncheckedSprite = uncheckedSprite;

        if (titleText != null && runtime != null && runtime.data != null)
        {
            originalTitle = runtime.data.title;
            titleText.text = originalTitle;
            originalColor = titleText.color;   
        }

        RefreshTick();
    }

    public void RefreshTick()
    {
        if (tickImage == null) return;

        if (runtime != null && runtime.isCompleted)
        {
            tickImage.sprite = checkedSprite;
        }
        else
        {
            tickImage.sprite = uncheckedSprite;
        }
    }

    // 下划线 + 改颜色 + 显示 description
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (titleText != null && !string.IsNullOrEmpty(originalTitle))
        {
            // TMP 下划线：用 <u> 标签包住
            titleText.text = "<u>" + originalTitle + "</u>";
            titleText.color = hoverColor;
        }

        if (infoBar != null && runtime != null && runtime.data != null)
        {
            infoBar.ShowQuestInfo(runtime.data);
        }
    }

    // 还原标题 & 颜色 & 隐藏 info
    public void OnPointerExit(PointerEventData eventData)
    {
        if (titleText != null)
        {
            titleText.text = originalTitle;
            titleText.color = originalColor;
        }

        if (infoBar != null)
        {
            infoBar.HideInfoBar();
        }
    }
}
