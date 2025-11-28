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
}

[CreateAssetMenu(fileName = "RecipeData", menuName = "Scriptable Objects/RecipeData")]
public class RecipeData : ScriptableObject
{
    [Header("材料列表")]
    public List<RecipeIngredient> ingredients = new List<RecipeIngredient>();

    [Header("生成的新卡")]
    public CardData output;

    [Tooltip("是否生成多于一张卡")]
    public bool hasMultiple;

    [Tooltip("如果多于一张生成卡的数量")]
    public int ammount = 0;

    [Tooltip("制作时间")]
    public float craftTime = 0f;
}