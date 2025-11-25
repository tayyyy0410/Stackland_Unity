using UnityEngine;
public class KeepInsideBoard : MonoBehaviour
{

    public float margin = 0f;

    private Card card;

    private void Awake()
    {
        card = GetComponent<Card>();
    }

    private void LateUpdate()
    {
        if (BoardBounds.I == null) return;

   
        Transform target = transform;
        if (card != null && card.stackRoot != null)
        {
          
            if (card.stackRoot != transform)
                return;
            
            target = card.stackRoot;
        }

        Vector3 pos = target.position;
        pos = BoardBounds.I.ClampPosition(pos, margin);
        target.position = pos;
    }
}