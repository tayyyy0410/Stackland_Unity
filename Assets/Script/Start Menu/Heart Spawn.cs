using UnityEngine;

public class HeartSpawn : MonoBehaviour
{
    public GameObject heartPrefab;
    public Transform canvasTransform;
    public Transform[] spawnPositions;

    public void OnButtonClick()
    {
        Debug.Log("ppp");
        if (spawnPositions.Length == 0) return;
        Transform spawn = spawnPositions[Random.Range(0, spawnPositions.Length)];

        GameObject heart = Instantiate(heartPrefab, canvasTransform);
        heart.transform.position = spawn.position;
    }
}
