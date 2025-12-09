using UnityEngine;
using TMPro;

public class InfoBarIndep : MonoBehaviour
{
    public TMP_Text cardName;
    public TMP_Text cardDescription;
    public TMP_Text cardPrice;

    public CanvasGroup infoTextGroup;

    private void Start()
    {
        infoTextGroup.alpha = 0f;
    }

    public void ShowInfoBar(CardData shownData)
    {
        cardName.text = shownData.displayName;
        cardDescription.text = shownData.description;
        cardPrice.text = shownData.value.ToString();
        infoTextGroup.alpha = 1f;
    }
    
    

    public void HideInfoBar()
    {
        infoTextGroup.alpha =0f;
    }
    
    public void ShowQuestInfo(QuestData quest)
    {
        if (quest == null) return;

        if (cardName != null)       cardName.text = quest.title;
        if (cardDescription != null) cardDescription.text = quest.description;
        if (cardPrice != null)       cardPrice.text = "";   
        infoTextGroup.alpha = 1f;
    }




}
