using UnityEngine;

public class IdeaToQuestBarIndep : MonoBehaviour
{
    public GameObject IdeaBar;
    public GameObject QuestBar;

    private bool ideaBarOn = true;
    private bool questBarOn = false;

    public void Decision()
    {
        if(questBarOn)
        {
            ConvertQuestToIdea();
        }
        else if (ideaBarOn)
        {
            ConvertIdeaToQuest();
        }
    }

    public void ConvertIdeaToQuest()
    {
        IdeaBar.SetActive(false);
        QuestBar.SetActive(true);
        questBarOn = true;
        ideaBarOn = false;
    }

    public void ConvertQuestToIdea()
    {
        QuestBar.SetActive(false);
        IdeaBar.SetActive(true);
        ideaBarOn = true;
        questBarOn = false;

    }

}
