using System;
using System.Collections.Generic;
using UnityEngine;

/// 一条配方里的一种材料
[Serializable]
public class RecipeIngredient
{
    [Tooltip("CardData")]
    public CardData cardData;

    [Tooltip("需要的数量")]
    public int amount = 1;
    
    [Tooltip("是否在合成后被消耗）")]
    public bool consume = true;  
}

[CreateAssetMenu(fileName = "RecipeData", menuName = "Scriptable Objects/RecipeData")]
public class RecipeData : ScriptableObject
{
    [Header("材料列表")]
    public List<RecipeIngredient> ingredients = new List<RecipeIngredient>();

    [Header("生成的新卡")]
    public CardData output;
    
    [Header("从pool里随机一个输出")]
    [Tooltip("为 true 时，从 outputPack 里随机出一张卡作为结果")]
    public bool useOutputPack = false;
    
    [Tooltip("用 PackData 当 loot table")]
    public PackData outputPack;
    

    [Tooltip("制作时间")]
    public float craftTime = 0f;
}