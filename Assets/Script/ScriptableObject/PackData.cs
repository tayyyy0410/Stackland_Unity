using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PackEntry
{
    [Tooltip("可以从这个卡包里开出来的卡")]
    public CardData cardData;

    [Tooltip("这个卡出现的权重）")]
    public int weight = 1;
}

// 卡包的可配置数据
[CreateAssetMenu(fileName = "PackData", menuName = "Scriptable Objects/PackData")]
public class PackData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("卡包ID，代码里用")]
    public string id;

    [Tooltip("卡包显名字")]
    public string displayName;

    [TextArea]
    [Tooltip("descriptions")]
    public string description;

    [Header("Price")]
    [Tooltip("需要多少金币价值才能买这个卡包")]
    public int price = 0;

    [Header("Open Settings")]
    [Tooltip("最少会掉几张卡")]
    public int minCards = 3;

    [Tooltip("最多会掉几张卡")]
    public int maxCards = 3;

    [Header("Loot Table")]
    [Tooltip("这个卡包里可能开出来的卡 + 权重")]
    public List<PackEntry> entries = new List<PackEntry>();
}