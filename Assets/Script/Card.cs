using UnityEngine;
//这个代码是接入CardData.cs 用来改变卡的数据和外观；目前的stack逻辑也写在这里

public class Card : MonoBehaviour
{
    [Header("Config")]
    public CardData data;        // 这张场上instance引用哪张 CardData
    private SpriteRenderer sr;

    [Header("Stacking")] 
    public Transform stackRoot; //一个stack的root
    public float yOffset = -0.5f; // 往下偏移

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        if (data != null)
        {
            ApplyData();
        }
        else
        {
            Debug.LogWarning($"{name} 没有设置 CardData！");
        }
        //如果没有stackRoot说明自己就是root
        if (stackRoot == null)
        {
            stackRoot = transform;
        }
    }

    private void ApplyData()
    {
        // 替换Sprite
        if (data.backgroundSprite != null)
        {
            sr.sprite = data.backgroundSprite;
        }
    }
    

    /// 是否是这个stack的最下面的
    public bool IsBottomOfStack()
    {
        // 只有自己一个，自己既是root也是最下面
        if (stackRoot == transform && transform.childCount == 0)
            return true;

        // 有子物体时，最下面是 root 的最后一个 child
        if (transform.parent == stackRoot &&
            transform.GetSiblingIndex() == stackRoot.childCount - 1)
        {
            return true;
        }

        return false;
    }

    /// 把自己这一叠叠到 target 的那一叠上
    public void JoinStackOf(Card target)
    {
        if (target == null) return;
        
        // stack的root
        Transform sourceRoot = stackRoot != null ? stackRoot : transform;
        // 目标stack的root
        Transform targetRoot = target.stackRoot != null ? target.stackRoot : target.transform;

        // 自己已经在对方这个stack里了不用处理
        if (sourceRoot == targetRoot) return;


        System.Collections.Generic.List<Transform> cardsToMove = new System.Collections.Generic.List<Transform>();
        cardsToMove.Add(sourceRoot);
        for (int i = 0; i < sourceRoot.childCount; i++)
        {
            cardsToMove.Add(sourceRoot.GetChild(i));
        }

        // 把整叠所有卡都挂到 targetRoot 下，形成一个大stack
        foreach (Transform t in cardsToMove)
        {
            t.SetParent(targetRoot);                // 所有卡的父节点都变成 targetRoot
            Card c = t.GetComponent<Card>();
            if (c != null)
            {
                c.stackRoot = targetRoot;          // 更新每张卡的 stackRoot 
            }
        }

        // 合并之后，对新的 target stack 排列一次
        Card targetRootCard = targetRoot.GetComponent<Card>();
        if (targetRootCard != null)
        {
            targetRootCard.LayoutStack();
        }
    }



    /// stack的layout
    public void LayoutStack()
    {
        if (stackRoot == null) return;

        yOffset = -0.5f; 

        int i = 0;
        foreach (Transform child in stackRoot)
        {
            i++;

            child.localPosition = new Vector3(0f, i * yOffset, 0f);
        }
    }
}
