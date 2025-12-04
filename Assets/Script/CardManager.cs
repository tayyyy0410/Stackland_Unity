using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 统计动态中的所有卡牌
/// </summary>

public class CardManager : MonoBehaviour
{
    public static CardManager Instance { get; private set; }

    public List<Card> AllCards { get; private set; } = new List<Card>();
    public List<Card> FoodCards { get; private set; } = new List<Card>();
    public List<Card> VillagerCards { get; private set; } = new List<Card>();

    // UI实时计数统计
    [Header("Runtime Stats (Debug)")]
    [Tooltip("实时更新的卡牌数据 (debug only)")]
    [SerializeField] private int coinCount; 
    [SerializeField] private int totalSaturation;   
    [SerializeField] private int totalHunger;   

    // UI: 其他 Class 调用的数据
    public int CoinCount => coinCount;  // UI：现有coin数量
    public int TotalSaturation => totalSaturation;  // UI：现有饱腹值
    public int TotalHunger => totalHunger;  // UI：需要的饱腹值

    public int NonCoinCount => AllCards.Count - CoinCount;  // UI：现有除了coin的卡牌数量


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

    }

    private void Start()
    {
        Card[] cards = FindObjectsByType<Card>(FindObjectsSortMode.None);

        foreach (Card card in cards)
        {
            if (card)
            RegisterCard(card);
        }
    }


    // 生成卡牌时注册
    public void RegisterCard(Card card)
    {
        if (card == null) return;

        var data = card.data;
        if (data == null)
        {
            Debug.LogWarning($"[Register] {card.name} 没有 data，暂时只加入 AllCards，不做分类统计");
            return;
        }

        if (card.data.cardClass == CardClass.Prefab) return;

        switch (data.cardClass)
        {
            case CardClass.Food:
                if (!FoodCards.Contains(card))
                {
                    FoodCards.Add(card);
                }
                break;

            case CardClass.Villager:
                if (!VillagerCards.Contains(card))
                {
                    VillagerCards.Add(card);
                }
                break;

            case CardClass.Coin:
                if (!card.HasRegisteredToManager)
                {
                    coinCount++;
                }
                break;

            default: break;
        }

        if (!AllCards.Contains(card))
        {
            AllCards.Add(card);
            card.HasRegisteredToManager = true;
        }

        RecalculateTotals();
    }



    // 销毁卡牌时删除数据
    public void UnregisterCard(Card card)
    {
        if (card == null) return;

        AllCards.Remove(card);
        FoodCards.Remove(card);
        VillagerCards.Remove(card);

        var data = card.data;
        if (data != null && data.cardClass == CardClass.Coin)
        {
            coinCount = Mathf.Max(0, CoinCount - 1);
        }

        RecalculateTotals();
    }



    public void ReduceSaturation(int delta)
    {
        totalSaturation -= delta;
    }



    public void RecalculateTotals()
    {
        totalSaturation = 0;
        totalHunger = 0;

        // 重新累加所有 food 的 currentSaturation
        foreach (var food in FoodCards)
        {
            if (food == null) continue;
            if (food.currentSaturation > 0)
            {
                totalSaturation += food.currentSaturation;
            }
        }

        // 重新累加所有 villager 的 currentHunger
        foreach (var v in VillagerCards)
        {
            if (v == null) continue;
            if (v.currentHunger > 0)
            {
                totalHunger += v.currentHunger;
            }
        }

        Debug.Log($"[CardManager]AllCards={AllCards.Count}, Villager={VillagerCards.Count}, Coin={CoinCount}, NonCoin={NonCoinCount}");
    }

}
