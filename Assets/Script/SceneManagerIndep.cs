using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManagerIndep : MonoBehaviour
{

    public void switchScene()
    {
        SceneManager.LoadScene("startScene", LoadSceneMode.Single);
    }


}
