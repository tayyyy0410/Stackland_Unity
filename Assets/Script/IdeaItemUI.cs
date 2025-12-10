using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class IdeaItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Refs")]
    public TMP_Text titleText;

    [Header("Hover Style")]
    public Color hoverColor = new Color(1f, 0.95f, 0.75f);

    private CardData ideaData;
    private InfoBarIndep infoBar;

    private string originalTitle;
    private Color originalColor;

    public void Init(CardData idea, InfoBarIndep infoBar)
    {
        this.ideaData = idea;
        this.infoBar = infoBar;

        if (titleText != null && idea != null)
        {
            // 用 displayName，当作侧栏的标题
            originalTitle = idea.displayName;
            titleText.text = originalTitle;
            originalColor = titleText.color;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (titleText != null && !string.IsNullOrEmpty(originalTitle))
        {
            // 下划线 + 改一点颜色
            titleText.text = "<u>" + originalTitle + "</u>";
            titleText.color = hoverColor;
        }

        // InfoBar 显示这张 idea 的说明
        if (infoBar != null && ideaData != null)
        {
            infoBar.ShowInfoBar(ideaData);   // 直接用你现在 Card 用的那个函数
        }
    }

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