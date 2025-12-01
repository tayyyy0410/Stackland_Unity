using NUnit.Framework.Interfaces;
using Unity.VisualScripting;
using UnityEngine;
using System.Collections.Generic;

//这个代码是接入CardData.cs 用来改变卡的数据和外观；目前的stack逻辑也写在这里

public class Card : MonoBehaviour
{
    [Header("Config")]
    public CardData data;        // 这张场上instance引用哪张 CardData
    private SpriteRenderer sr;

    [Header("Stacking")] 
    public Transform stackRoot; //一个stack的root
    public float yOffset = -0.5f; // 往下偏移
    
    [Header("Harvest Runtime")]
    [HideInInspector] public int harvestUsesLeft = -1;

    [Header("Feeding Runtime")]
    public int currentSaturation = -1;  //food剩余的饱腹值，卡牌ui显示这个
    public int currentHunger = 0;   //villager的饥饿值

    [Header("UI Display")]
    private InfoBarIndep infoBar;

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
        
        if (stackRoot == null)
        {
            stackRoot = transform;
        }

        GameObject infoBarObj = GameObject.FindWithTag("UI-Infobar");
        infoBar = infoBarObj.GetComponent<InfoBarIndep>();
    }
    
    public void EnsureHarvestInit()
    {
        if (data == null) return;
        if (!data.isHarvestable) return;

        if (harvestUsesLeft < 0)
        {
            harvestUsesLeft = Mathf.Max(1, data.maxHarvestUses);
        }
    }

    public void FoodInit()
    {
        if (data.cardClass == CardClass.Food && data.hasSaturation && data.saturation > 0)
        {
            currentSaturation = data.saturation;
        }
        else currentSaturation = -1;    //不是食物没有饱腹值
    }

    public void HungerInit()
    {
        if (data.cardClass == CardClass.Villager)
        {
            sr= GetComponent<SpriteRenderer>();
            sr.sortingOrder = -100;
            currentHunger = data.hunger;
        }
        else currentHunger = -1;    //不是villager没有饥饿值
    }


    public void ApplyData()
    {
        // 替换Sprite
        if (data.backgroundSprite != null)
        {
            sr.sprite = data.backgroundSprite;
        }
        
        harvestUsesLeft = -1;
        EnsureHarvestInit();
        FoodInit();
        HungerInit();
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
            t.SetParent(targetRoot);               
            Card c = t.GetComponent<Card>();
            if (c != null)
            {
                c.stackRoot = targetRoot;          // 更新每张卡的 stackRoot 
            }
        }

        // 合并之后对新的 target stack 排
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



    private void OnMouseEnter()
    {
        infoBar.ShowInfoBar(data);
    }

    private void OnMouseExit()
    {
        infoBar.HideInfoBar();
    }


    // ====================== Feeding Helpers =====================
    public bool IsTopOfStack()
    {
        Transform parentRoot = stackRoot != null ? stackRoot : transform;
        if (parentRoot.childCount == 0)
        {
            return transform == parentRoot;
        }

        if (transform == parentRoot)
        {
            return false;
        }

        return transform.parent == parentRoot && 
               transform.GetSiblingIndex() == parentRoot.childCount - 1;
    }


    /// <summary>
    /// 从 stack 中抽出一张卡牌
    /// </summary>
    public void TakeOutOfStack()
    {
        Transform root = stackRoot != null ? stackRoot : transform;

        // 自己是stackRoot
        if (transform == root && root.childCount > 0)
        {
            Transform newRoot = root.GetChild(0);
            List<Transform> toMove = new List<Transform>();

            for (int i = 1; i < root.childCount; i++)
            {
                toMove.Add(root.GetChild(i));
            }

            foreach (Transform child in toMove)
            {
                child.SetParent(newRoot);
            }

            newRoot.SetParent(null);
            Card newRootCard = newRoot.GetComponent<Card>();

            if (newRootCard != null)
            {
                newRootCard.stackRoot = newRoot;

                foreach (Transform t in newRoot)
                {
                    Card c = t.GetComponent<Card>();

                    if (c != null)
                    {
                        c.stackRoot = newRoot;
                    }
                }

                newRootCard.LayoutStack();
            }

            stackRoot = transform;
            transform.SetParent(null);
            
        }

        /*// 自己是 stack 中间的卡牌
        else if (transform != root)
        {
            Transform oldRoot = root;

            transform.SetParent(null);
            stackRoot = transform;

            Card oldRootCard = root.GetComponent<Card>();
            if (oldRootCard != null)
            {
                oldRootCard.LayoutStack();
            }
        }*/
    }

}
