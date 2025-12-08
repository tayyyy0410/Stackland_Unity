using UnityEngine;

public class IdeaToQuestBarIndep : MonoBehaviour
{
    public GameObject IdeaBar;
    public GameObject QuestBar;

    public GameObject ItQButton;
    public GameObject QtIButton;

    public void ConvertIdeaToQuest()
    {
        IdeaBar.SetActive(false);
        QuestBar.SetActive(true);

        ItQButton.SetActive(false);
        QtIButton.SetActive(true);
    }

    public void ConvertQuestToIdea()
    {
        QuestBar.SetActive(false);
        IdeaBar.SetActive(true);

        ItQButton.SetActive(true);
        QtIButton.SetActive(false);


    }

}
