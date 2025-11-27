using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// 控制所有配方匹配和合成逻辑
public class RecipeManager : MonoBehaviour
{
    public static RecipeManager Instance { get; private set; }

    [Header("所有Recipe")]
    [Tooltip("把所有的 RecipeData 拖进来")]
    public List<RecipeData> recipes = new List<RecipeData>();

    [Header("card prefab")]
    public GameObject cardPrefab;

    [Header("生成")]
    [Tooltip("生成的新卡相对于原 stack 的偏移")]
    public Vector2 spawnOffset = new Vector2(1f, 0f);

  
    private HashSet<Transform> craftingStacks = new HashSet<Transform>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // 对一个 stack尝试匹配所有Recipe
    public void TryCraftFromStack(Card rootCard)
    {
        if (rootCard == null || rootCard.stackRoot == null) return;

        Transform stackRoot = rootCard.stackRoot;

        // 不重复触发
        if (craftingStacks.Contains(stackRoot))
            return;

     
        List<CardData> stackCards = new List<CardData>();

        // stackRoot 自己这一张
        Card rootCardComp = stackRoot.GetComponent<Card>();
        if (rootCardComp != null && rootCardComp.data != null)
        {
            stackCards.Add(rootCardComp.data);
        }

        // 子物体
        for (int i = 0; i < stackRoot.childCount; i++)
        {
            Transform child = stackRoot.GetChild(i);
            Card c = child.GetComponent<Card>();
            if (c != null && c.data != null)
            {
                stackCards.Add(c.data);
            }
        }

        // 找出所有刚好完全匹配当前 stack 的Recipe
        var candidates = new List<(RecipeData recipe, int totalNeeded)>();

        foreach (var recipe in recipes)
        {
            if (recipe == null || recipe.output == null) continue;

            if (RecipeMatchesStackExact(recipe, stackCards))
            {
                //计算一共要多少card
                int totalNeeded = 0;
                foreach (var ing in recipe.ingredients)
                {
                    if (ing.cardData == null || ing.amount <= 0) continue;
                    totalNeeded += ing.amount;
                }

                candidates.Add((recipe, totalNeeded));
            }
        }
        
        if (candidates.Count == 0)
            return;
        
        //   totalNeeded 越大越优先
        var best = candidates[0];
        foreach (var c in candidates)
        {
            if (c.totalNeeded > best.totalNeeded)
            {
                best = c;
            }
        }

        RecipeData selectedRecipe = best.recipe;

        // 记录这一次参与制作的所有 Card
        List<Card> originalCards = new List<Card>();
        var cardsInStack = stackRoot.GetComponentsInChildren<Card>();
        foreach (var c in cardsInStack)
        {
            if (c != null)
                originalCards.Add(c);
        }

        // 根据 craftTime决定延时合成
        if (selectedRecipe.craftTime > 0f)
        {
            StartCoroutine(CraftRecipeWithDelay(selectedRecipe, stackRoot, originalCards));
        }
        else
        {
            CraftRecipeInstant(selectedRecipe, stackRoot);
        }
    }
    
    //stack 里的每种 CardData 数量要等于recipe要求的数量， stack 里的总数量 得等与 配方所有材料数量之和，不然会有bug我也不知道为啥
    private bool RecipeMatchesStackExact(RecipeData recipe, List<CardData> stackCards)
    {
        if (stackCards == null || stackCards.Count == 0) return false;
        if (recipe.ingredients == null || recipe.ingredients.Count == 0) return false;

        // 统计 stack 里每种 CardData 的数量
        Dictionary<CardData, int> stackCounts = new Dictionary<CardData, int>();
        foreach (var cardData in stackCards)
        {
            if (cardData == null) continue;
            if (!stackCounts.ContainsKey(cardData))
                stackCounts[cardData] = 0;
            stackCounts[cardData]++;
        }

        // 统计配方需要的总数量
        Dictionary<CardData, int> recipeCounts = new Dictionary<CardData, int>();
        int totalNeeded = 0;
        foreach (var ing in recipe.ingredients)
        {
            if (ing.cardData == null || ing.amount <= 0) continue;

            totalNeeded += ing.amount;

            if (!recipeCounts.ContainsKey(ing.cardData))
                recipeCounts[ing.cardData] = 0;

            recipeCounts[ing.cardData] += ing.amount;
        }

        // 数量必须刚好匹配
        if (totalNeeded != stackCards.Count)
            return false;
        
        foreach (var kv in recipeCounts)
        {
            CardData cardType = kv.Key;
            int needed = kv.Value;

            if (!stackCounts.ContainsKey(cardType)) return false;
            if (stackCounts[cardType] != needed) return false;
        }
        
        if (stackCounts.Count != recipeCounts.Count)
            return false;

        return true;
    }

    /// 先等待 craftTime 秒再执行 CraftRecipeInstant
    private IEnumerator CraftRecipeWithDelay(RecipeData recipe, Transform stackRoot, List<Card> originalCards)
    {
        if (stackRoot == null) yield break;

        craftingStacks.Add(stackRoot);

        float timer = 0f;
        int originalCount = originalCards.Count;

        // UI，比如在 stack 上显示剩余时间
        while (timer < recipe.craftTime)
        {
            // 如果 stack 在过程中被销毁，直接中止
            if (stackRoot == null || stackRoot.gameObject == null)
            {
                craftingStacks.Remove(stackRoot);
                yield break;
            }

            // 检查 stack 是否被拖走
            var currentCards = stackRoot.GetComponentsInChildren<Card>();
            if (currentCards.Length != originalCount)
            {
        
                craftingStacks.Remove(stackRoot);
                Debug.Log("制作被打断：stack 里卡牌数量发生变化，取消本次合成。");
                yield break;
            }

            // 每一张参与制作的卡，必须仍然在这个 stackRoot 下
            foreach (var c in originalCards)
            {
                if (c == null) 
                {
                    craftingStacks.Remove(stackRoot);
                    Debug.Log("制作被打断：有参与制作的卡被销毁，取消本次合成。");
                    yield break;
                }

                // 仍然是该 stack 的 root 或子物体
                if (c.transform != stackRoot && !c.transform.IsChildOf(stackRoot))
                {
                    craftingStacks.Remove(stackRoot);
                    Debug.Log("制作被打断：有参与制作的卡不再属于这个 stack，取消本次合成。");
                    yield break;
                }
            }

            timer += Time.deltaTime;
            // 这里也可以把 timer / craftTime 传给 UI 显示进度
            yield return null;
        }

        // 如果 stack 还存在就执行合成
        if (stackRoot != null && stackRoot.gameObject != null)
        {
            CraftRecipeInstant(recipe, stackRoot);
        }

        craftingStacks.Remove(stackRoot);
    }

    /// 生成新card
    private void CraftRecipeInstant(RecipeData recipe, Transform stackRoot)
    {
        if (cardPrefab == null)
        {
            Debug.LogError("RecipeManager 没有设置 cardPrefab，无法生成结果卡！");
            return;
        }

        Vector3 spawnPos = stackRoot.position + (Vector3)spawnOffset;

        // 先收集所有要删的卡
        List<GameObject> toDestroy = new List<GameObject>();
        Card rootCard = stackRoot.GetComponent<Card>();
        if (rootCard != null)
        {
            toDestroy.Add(rootCard.gameObject);
        }
        for (int i = 0; i < stackRoot.childCount; i++)
        {
            Transform child = stackRoot.GetChild(i);
            Card c = child.GetComponent<Card>();
            if (c != null)
            {
                toDestroy.Add(c.gameObject);
            }
        }

        // 删掉旧的 stack
        foreach (var go in toDestroy)
        {
            if (go != null)
            {
                Destroy(go);
            }
        }

        // 生成新卡
        GameObject newCardObj = Instantiate(cardPrefab, spawnPos, Quaternion.identity);
        Card newCard = newCardObj.GetComponent<Card>();
        if (newCard != null)
        {
            newCard.data = recipe.output;
            newCard.stackRoot = newCard.transform;
            newCard.ApplyData();   // 根据 data 更新sprite
            newCard.LayoutStack(); // 以防以后有子卡
        }

        Debug.Log($"配方合成完成：生成 {recipe.output.displayName}");
    }
}
