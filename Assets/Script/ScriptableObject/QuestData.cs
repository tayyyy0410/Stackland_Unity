using UnityEngine;
using System.Collections.Generic;

public enum QuestType
{
    OpenPacks,              // 打开指定/任意卡包
    UseCardOnCard,         // A 拖到 B 上
    CraftCard,             // 造出一张卡
    SellCard,              // 卖掉一张卡
    BuyPack,               // 买某种卡包
    PauseGame,             // 暂停一次
    ReachCardCount,        // 场上某张卡数量 ≥ N
    KillEnemy,             // 杀死某种敌人
    EscapeWithinMoon,      // N 天/月内通关
    UnlockAllPacks,        // 解锁所有卡包
    UnlockAllCardsInPack   // 某个 Pack 的所有卡都解锁过
}

[CreateAssetMenu(fileName = "NewQuest", menuName = "StacklandsClone/Quest")]
public class QuestData : ScriptableObject
{
    [Header("Basic Info")]
    public string questId;          
    public string title;            
    [TextArea]
    public string description;      

    public Sprite icon;             

    [Header("Quest Logic")]
    public QuestType type;

    [Tooltip("需要达到的次数，例如：开 3 个包、造 5 张卡")]
    public int targetCount = 1;

    [Tooltip("某些任务的时间限制（比如 30 Moon）")]
    public int moonLimit = 0;      

    [Header("Filters / Params")]
    [Tooltip("目标卡包（用于 OpenPack / BuyPack / UnlockAllCardsInPack）")]
    public PackData targetPack;

    [Tooltip("主体卡：UseCardOnCard 的 \"谁\"，或者 ReachCardCount 的卡种类")]
    public CardData subjectCard;

    [Tooltip("目标卡：UseCardOnCard 的 \"拖到谁身上\"")]
    public CardData targetCard;

    // QuestData.cs 里，加在 resultCard 下面就行
    [Header("多张结果卡")]
    public List<CardData> resultCards = new List<CardData>();

}