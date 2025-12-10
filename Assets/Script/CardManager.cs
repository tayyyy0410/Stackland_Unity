using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class CardManager : MonoBehaviour
{
    public static CardManager Instance { get; private set; }

    public List<Card> AllCards { get; private set; } = new List<Card>();
    public List<Card> FoodCards { get; private set; } = new List<Card>();
    public List<Card> VillagerCards { get; private set; } = new List<Card>();
    public List<string> NewCards { get; private set; } = new List<string>();
    
    
    public HashSet<CardData> discoveredIdeas = new HashSet<CardData>();

    


    // UI需要显示的数据
    [Header("Runtime Stats (For Debug)")]
    [Tooltip("实时统计数据 (debug only)")]
    [SerializeField] private int coinCount;
    [SerializeField] private int totalSaturation;
    [SerializeField] private int totalHunger;
    [SerializeField] private int maxCardCapacity;
    private int fixedMaxCapcity = 20;
    
    [Header("Quest Config")]
    [SerializeField] private CardData passengerCardData;


    // UI: 外部调用
    public int MaxCardCapacity => maxCardCapacity;  
    public int CoinCount => coinCount;  
    public int TotalSaturation => totalSaturation;  
    public int TotalHunger => totalHunger;  

    public int NonCoinCount => AllCards.Count - CoinCount; 

    public int CardToSellCount => NonCoinCount - MaxCardCapacity;
    public int NewCardCount => NewCards.Count;

    public TMP_Text finalNewCardText;
    public TMP_Text finalNewCardTextSuccesss;


    //idea card////
    public bool HasDiscoveredIdea(CardData data)
    {
        return data != null && discoveredIdeas.Contains(data);
    }

    // new idea
    public void HasDiscoveredNewCard(Card card)
    {
        if (card == null || card.data == null) return;
        if (card.data.cardClass == CardClass.Idea) return;
        string name = card.data.displayName;
        if (!NewCards.Contains(name))
        {
            NewCards.Add(name);
            Debug.Log($"[NewCard] 解锁新卡牌：{name}，一共解锁{NewCardCount}张新卡牌");
        }
    }

    public void RegisterIdeaIfNeeded(Card card)
    {
        if (card == null || card.data == null) return;
        if (card.data.cardClass != CardClass.Idea) return;

        if (!discoveredIdeas.Contains(card.data))
        {
            discoveredIdeas.Add(card.data);
            Debug.Log($"[Idea] 解锁新 Idea：{card.data.displayName}");
        }
    }

    // ==========================================================================================
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
        coinCount = 0;
        totalSaturation = 0;
        totalHunger = 0;
        maxCardCapacity = fixedMaxCapcity;

        finalNewCardText.text = $"{NewCards.Count} New Cards Found";
        finalNewCardTextSuccesss.text = NewCards.Count.ToString();

        Card[] cards = FindObjectsByType<Card>(FindObjectsSortMode.None);

        foreach (Card card in cards)
        {
            if (card)
                RegisterCard(card);
        }
    }


    // ===================================== Register Helpers =============================================

        /// <summary>
        /// 在 card enable时调用，把卡牌注册到 CardManager 并更新一遍数据
        /// </summary>
    public void RegisterCard(Card card)
    {
        if (card == null) return;
        if (!card.IsOnBoard) return;    // do not register cards in equip

        var data = card.data;
        if (data == null)
        {
            Debug.LogWarning($"[Register] {card.name} û�� data");
            return;
        }

        if (card.data.cardClass == CardClass.Prefab) return;
        
        RegisterIdeaIfNeeded(card); 
        HasDiscoveredNewCard(card);

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
                if (EquipManager.Instance != null && EquipManager.Instance.allEquipStates.ContainsKey(card))
                {
                    EquipManager.Instance.allEquipStates[card] = new EquipManager.VillagerEquipState();
                }
                break;

            case CardClass.Coin:
                if (!card.HasRegisteredToManager)
                {
                    coinCount++;
                }
                break;

            case CardClass.Structure:
                if (!card.HasRegisteredToManager && card.data.hasCapacity)
                {
                    maxCardCapacity += card.data.capacity;
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



    /// <summary>
    /// 在 card disable时调用，把卡牌从 CardManager 中删除并更新一遍数据
    /// </summary>
    public void UnregisterCard(Card card)
    {
        if (card == null) return;

        AllCards.Remove(card);
        FoodCards.Remove(card);
        VillagerCards.Remove(card);
        EquipManager.Instance.allEquipStates.Remove(card);

        var data = card.data;
        if (data != null && data.cardClass == CardClass.Coin)
        {
            coinCount = Mathf.Max(0, CoinCount - 1);
        }

        if (data != null && data.hasCapacity)
        {
            maxCardCapacity = Mathf.Max(0, MaxCardCapacity - data.capacity);
        }

        // villager取消注册时同时清除装备栏UI
        if (card.data != null && card.data.cardClass == CardClass.Villager)
        {
            if (EquipmentUIController.Instance != null)
            {
                EquipmentUIController.Instance.CloseSmallBar(card);
                EquipmentUIController.Instance.CloseBigPanel(card);
            }
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

        // 计算所有 food 的 currentSaturation
        foreach (var food in FoodCards)
        {
            if (food == null) continue;
            if (food.currentSaturation > 0)
            {
                totalSaturation += food.currentSaturation;
            }
        }

        // 计算所有 villager 的 currentHunger
        foreach (var v in VillagerCards)
        {
            if (v == null) continue;
            if (v.currentHunger > 0)
            {
                totalHunger += v.currentHunger;
            }
        }
        
        
        //村民任务
        if (passengerCardData != null && QuestManager.Instance != null)
        {
            int passengerCount = 0;

            // 这里我用 AllCards 来统计，只要 data == passengerCardData 就算 Passenger
            foreach (var c in AllCards)
            {
                if (c == null || c.data == null) continue;
                if (c.data == passengerCardData)
                {
                    passengerCount++;
                }
            }

            QuestManager.Instance.NotifyCardCountChanged(passengerCardData, passengerCount);
        }

        Debug.Log($"[CardManager]AllCards={AllCards.Count}, Villager={VillagerCards.Count}, Coin={CoinCount}, NonCoin={NonCoinCount}");
    }


    public void RefreshVillagerHPBeforeNewMoon()
    {
        foreach (var v in VillagerCards)
        {
            int oldOwnHP = v.currentOwnHP;
            v.currentOwnHP = Mathf.Min(v.currentOwnHP + 5, v.data.baseHP);
            v.currentHP += v.currentOwnHP - oldOwnHP;
            
        }
    }

}
