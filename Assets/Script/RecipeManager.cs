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
        
        [Header("Craft 进度条")]
        public CraftBar craftBarPrefab;

// 记录每个 stackRoot 对应的进度条（可以同时有多条）
        private Dictionary<Transform, CraftBar> activeCraftBars = new Dictionary<Transform, CraftBar>();


      
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
        
        /// 按权重从 PackData 里随机出一张 CardData
        private CardData GetRandomFromPack(PackData pack)
        {
            if (pack == null || pack.entries == null || pack.entries.Count == 0)
            {
                Debug.Log("[Loot] pack 为空或没有 entries");
                return null;
            }
               

            int totalWeight = 0;
            foreach (var e in pack.entries)
            {
                if (e.cardData == null || e.weight <= 0) continue;
                totalWeight += e.weight;
            }

            if (totalWeight <= 0) return null;

            int rand = Random.Range(0, totalWeight);
            int cumulative = 0;

            foreach (var e in pack.entries)
            {
                if (e.cardData == null || e.weight <= 0) continue;

                cumulative += e.weight;
                if (rand < cumulative)
                {
                    return e.cardData;
                }
            }

            return null;
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
                if (recipe == null) continue;

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

        // 为这个 stackRoot  获取一个进度条
        CraftBar bar = GetOrCreateCraftBar(stackRoot);

        // 先看看这次参与制作的卡里有没有“可采集”的结构
        Card harvestCard = null;
        foreach (var c in originalCards)
        {
            if (c != null && c.data != null && c.data.harvestLootPack != null)
            {
                harvestCard = c;
                break;
            }
        }


        // 一般配方
        if (harvestCard == null || recipe.craftTime <= 0f)
        {
            float timer = 0f;
            int originalCount = originalCards.Count;

            while (timer < recipe.craftTime)
            {
                // dayManager 控制合成暂停
                while (DayManager.Instance.dayPaused)
                {
                    // 确保即使在暂停时停止合成也能立刻移除 craftBar
                    if (stackRoot == null || stackRoot.gameObject == null)
                    {
                        if (bar != null) RemoveCraftBar(stackRoot);
                        craftingStacks.Remove(stackRoot);
                        yield break;
                    }
                    var _currentCards = stackRoot.GetComponentsInChildren<Card>();
                    if (_currentCards.Length != originalCount)
                    {
                        if (bar != null) RemoveCraftBar(stackRoot);
                        craftingStacks.Remove(stackRoot);
                        Debug.Log("制作被打断：stack 里卡牌数量发生变化，取消本次合成。");
                        yield break;
                    }

                    yield return null;
                }

                // 如果 stack 在过程中被销毁，直接中止
                if (stackRoot == null || stackRoot.gameObject == null)
                {
                    if (bar != null) RemoveCraftBar(stackRoot);
                    craftingStacks.Remove(stackRoot);
                    yield break;
                }

                // 检查 stack 是否被拖走 / 牌数量改变
                var currentCards = stackRoot.GetComponentsInChildren<Card>();
                if (currentCards.Length != originalCount)
                {
                    if (bar != null) RemoveCraftBar(stackRoot);
                    craftingStacks.Remove(stackRoot);
                    Debug.Log("制作被打断：stack 里卡牌数量发生变化，取消本次合成。");
                    yield break;
                }

                // 每一张参与制作的卡必须仍然在这个 stackRoot 下
                foreach (var c in originalCards)
                {
                    if (c == null)
                    {
                        if (bar != null) RemoveCraftBar(stackRoot);
                        craftingStacks.Remove(stackRoot);
                        Debug.Log("制作被打断：有参与制作的卡被销毁，取消本次合成。");
                        yield break;
                    }

                    if (c.transform != stackRoot && !c.transform.IsChildOf(stackRoot))
                    {
                        if (bar != null) RemoveCraftBar(stackRoot);
                        craftingStacks.Remove(stackRoot);
                        Debug.Log("制作被打断：有参与制作的卡不再属于这个 stack，取消本次合成。");
                        yield break;
                    }
                }

                // 更新进度条（0 ~ 1）
                if (bar != null && recipe.craftTime > 0f)
                {
                    bar.SetProgress(timer / recipe.craftTime);
                }

                timer += Time.deltaTime * DayManager.Instance.gameSpeed;
                yield return null;
            }

            // 正常完成一次制作：拉满再清掉进度条
            if (bar != null)
            {
                bar.SetProgress(1f);
                RemoveCraftBar(stackRoot);
            }

            if (stackRoot != null && stackRoot.gameObject != null)
            {
                CraftRecipeInstant(recipe, stackRoot);
            }

            craftingStacks.Remove(stackRoot);
            yield break;
        }

        // 采集多次的逻辑

        harvestCard.EnsureHarvestInit();
        int maxUses = Mathf.Max(1, harvestCard.data.maxHarvestUses);
        float perUseTime = recipe.craftTime / maxUses;

        int originalTotalCount2 = originalCards.Count;

        while (true)
        {
            // 被打断 / 用完就结束总循环
            if (stackRoot == null || stackRoot.gameObject == null) break;
            if (harvestCard == null || harvestCard.data == null) break;
            if (harvestCard.harvestUsesLeft <= 0) break;

            float timer = 0f;

            // 每一“口”的计时循环
            while (timer < perUseTime)
            {
                // dayManager 控制合成暂停
                while (DayManager.Instance.dayPaused)
                {
                    // 确保即使在暂停时停止合成也能立刻移除 craftBar
                    if (stackRoot == null || stackRoot.gameObject == null)
                    {
                        if (bar != null) RemoveCraftBar(stackRoot);
                        craftingStacks.Remove(stackRoot);
                        yield break;
                    }
                    var _currentCards = stackRoot.GetComponentsInChildren<Card>();
                    if (_currentCards.Length != originalTotalCount2)
                    {
                        if (bar != null) RemoveCraftBar(stackRoot);
                        craftingStacks.Remove(stackRoot);
                        Debug.Log("制作被打断：stack 里卡牌数量发生变化，取消本次采集循环。");
                        yield break;
                    }

                    yield return null;

                }

                if (stackRoot == null || stackRoot.gameObject == null)
                {
                    if (bar != null) RemoveCraftBar(stackRoot);
                    craftingStacks.Remove(stackRoot);
                    yield break;
                }

                var currentCards = stackRoot.GetComponentsInChildren<Card>();
                if (currentCards.Length != originalTotalCount2)
                {
                    if (bar != null) RemoveCraftBar(stackRoot);
                    craftingStacks.Remove(stackRoot);
                    Debug.Log("制作被打断：stack 里卡牌数量发生变化，取消本次采集循环。");
                    yield break;
                }

                foreach (var c in originalCards)
                {
                    if (c == null)
                    {
                        if (bar != null) RemoveCraftBar(stackRoot);
                        craftingStacks.Remove(stackRoot);
                        Debug.Log("制作被打断：有参与制作的卡被销毁，取消本次采集循环。");
                        yield break;
                    }

                    if (c.transform != stackRoot && !c.transform.IsChildOf(stackRoot))
                    {
                        if (bar != null) RemoveCraftBar(stackRoot);
                        craftingStacks.Remove(stackRoot);
                        Debug.Log("制作被打断：有参与制作的卡不再属于这个 stack，取消本次采集循环。");
                        yield break;
                    }
                }

                //  更新这一“口”的局部进度（0 ~ 1）
                if (bar != null && perUseTime > 0f)
                {
                    bar.SetProgress(timer / perUseTime);
                }

                timer += Time.deltaTime * DayManager.Instance.gameSpeed;
                yield return null;
            }

            // 这一口结束，可以拉满一下
            if (bar != null)
            {
                bar.SetProgress(1f);
            }

            // 掉一次资源、扣一次耐久
            if (stackRoot != null && stackRoot.gameObject != null)
            {
                CraftRecipeInstant(recipe, stackRoot);
            }
            else
            {
                break;
            }
        }

        // 整个采集循环结束，清理进度条
        if (bar != null)
        {
            RemoveCraftBar(stackRoot);
        }

        craftingStacks.Remove(stackRoot);
    }



        /// 生成新card
        private void CraftRecipeInstant(RecipeData recipe, Transform stackRoot)
        {
            if (cardPrefab == null)
            {
                Debug.LogError("RecipeManager 没有设置 cardPrefab，无法生成结果卡");
                return;
            }

            if (stackRoot == null) return;

            Vector3 spawnPos = stackRoot.position + (Vector3)spawnOffset;

           
            Dictionary<CardData, int> needConsume = new Dictionary<CardData, int>();
            foreach (var ing in recipe.ingredients)
            {
                if (ing.cardData == null || ing.amount <= 0) continue;
                if (!ing.consume) continue;              

                if (!needConsume.ContainsKey(ing.cardData))
                    needConsume[ing.cardData] = 0;

                needConsume[ing.cardData] += ing.amount;
            }

            // stack 里所有 Card
            List<Card> allCards = new List<Card>(stackRoot.GetComponentsInChildren<Card>());
            List<GameObject> toDestroy = new List<GameObject>();


            foreach (var c in allCards)
            {
                if (c == null || c.data == null) continue;

                if (needConsume.TryGetValue(c.data, out int remaining) && remaining > 0)
                {
                    Debug.Log($"[Recipe] 将要消耗：{c.data.displayName}");
                    toDestroy.Add(c.gameObject);
                    needConsume[c.data] = remaining - 1;
                }
            }

            if (toDestroy.Count > 0)
            {
                foreach (var c in allCards)
                {
                    if (c == null) continue;
                    if (toDestroy.Contains(c.gameObject)) 
                        continue;  

                    Transform t = c.transform;
                    t.SetParent(null); 
                    c.stackRoot = t;         // 让自己当自己的 stackRoot
                    c.LayoutStack();
                    Debug.Log($"[Recipe] 保留：{c.data.displayName}");
                }
            }
            foreach (var go in toDestroy)
            {
                if (go != null)
                {
                    Destroy(go);
                }
            }
            
            CardData harvestOutput = null;  // 这次采集要掉的那张卡

            foreach (var c in allCards)
            {
                if (c == null) continue;
                if (toDestroy.Contains(c.gameObject)) 
                    continue;  // 已经被删掉的不管

                if (c.data != null && c.data.harvestLootPack != null)
                {
                    // 初始化次数
                    c.EnsureHarvestInit();
                    c.harvestUsesLeft--;

                    Debug.Log($"[Harvest] {c.data.displayName} 被采集一次，剩余 {c.harvestUsesLeft}");

                    // 本次掉落
                    if (harvestOutput == null)
                    {
                        harvestOutput = GetRandomFromPack(c.data.harvestLootPack);
                    }

                    // 用完了就销毁
                    if (c.harvestUsesLeft <= 0)
                    {
                        if (c.data.depletedCardData != null)
                        {
                            c.data = c.data.depletedCardData;
                            c.ApplyData();
                            c.LayoutStack();
                        }
                        else
                        {
                            
                            var children = new List<Transform>();
                            foreach (Transform child in c.transform)
                            {
                                children.Add(child);
                            }

                            // 逐个脱离 parent
                            foreach (var child in children)
                            {
                                child.SetParent(null);
                                Card childCard = child.GetComponent<Card>();
                                if (childCard != null)
                                {
                                    childCard.stackRoot = child;
                                    childCard.LayoutStack();
                                }
                            }

                    
                            Destroy(c.gameObject);
                        }
                    }
                }
            }
            
            CardData resultData = harvestOutput != null ? harvestOutput : recipe.output;

            if (resultData == null)
            {
                Debug.Log("[Recipe] resultData 为 null，本次不生成新卡。");
                return;
            }

            // 生成结果卡
            GameObject newCardObj = Instantiate(cardPrefab, spawnPos, Quaternion.identity);
            Card newCard = newCardObj.GetComponent<Card>();
            if (newCard != null)
            {
                newCard.data = resultData;
                newCard.stackRoot = newCard.transform;
                newCard.ApplyData();
                newCard.LayoutStack();
            }

            Debug.Log($"配方合成完成：生成 {resultData.displayName}");
        }
        
        
        //craftBar相关
        private CraftBar GetOrCreateCraftBar(Transform stackRoot)
        {
            if (craftBarPrefab == null || stackRoot == null) return null;

            CraftBar bar;
            if (!activeCraftBars.TryGetValue(stackRoot, out bar) || bar == null)
            {
                bar = Instantiate(craftBarPrefab);
                bar.Init(stackRoot);             // 告诉它要跟谁
                activeCraftBars[stackRoot] = bar;
            }
            return bar;
        }

        private void RemoveCraftBar(Transform stackRoot)
        {
            if (stackRoot == null) return;

            CraftBar bar;
            if (activeCraftBars.TryGetValue(stackRoot, out bar))
            {
                if (bar != null)
                {
                    Destroy(bar.gameObject);
                }
                activeCraftBars.Remove(stackRoot);
            }
        }





    }
