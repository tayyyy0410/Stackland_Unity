using UnityEngine;
using TMPro;

[RequireComponent(typeof(Card))]
[RequireComponent(typeof(SpriteRenderer))]
public class CardStatsUI : MonoBehaviour
{
    [Header("Root")]
    public GameObject statsRoot;

    [Header("HP Texts")]
    public TMP_Text hpTextDark;
    public TMP_Text hpTextWhite;

    [Header("Hunger Texts")]
    public TMP_Text hungerTextDark;
    public TMP_Text hungerTextWhite;

    [Header("Coin Texts")]
    public TMP_Text coinTextDark;
    public TMP_Text coinTextWhite;

    private Card card;
    private SpriteRenderer cardSR;
    private Canvas statsCanvas;

    private int lastHp = int.MinValue;
    private int lastHunger = int.MinValue;
    private int lastValue = int.MinValue;

    private bool showHP;
    private bool showHunger;
    private bool showValue;
    private bool useDarkText;

    private void Awake()
    {
        card   = GetComponent<Card>();
        cardSR = GetComponent<SpriteRenderer>();

        if (statsRoot != null)
        {
            statsCanvas = statsRoot.GetComponent<Canvas>();

            // 如果你在 prefab 里不小心把它关了，顺便帮你打开
            if (!statsRoot.activeSelf)
            {
                statsRoot.SetActive(true);
            }
        }
    }

    private void Start()
    {
        InitFromData();
        RefreshAll();
    }

    public void InitFromData()
    {
        if (card.data == null) return;

        // 这里根据你的 CardData 自己的字段来写
        showHP     = card.data.hasHP     && card.data.showHP;
        showHunger = card.data.hasHunger && card.data.showHunger;
        showValue  = card.data.value > 0 && card.data.showValue;

        useDarkText = card.data.useDarkText;
        
        if (showHP && !card.hasInitHP)
        {
            card.EnsureBattleInit();
        }

        // 只控制 Text，根节点 statsRoot 一律不关
        hpTextDark.gameObject.SetActive(showHP && useDarkText);
        hpTextWhite.gameObject.SetActive(showHP && !useDarkText);

        hungerTextDark.gameObject.SetActive(showHunger && useDarkText);
        hungerTextWhite.gameObject.SetActive(showHunger && !useDarkText);

        coinTextDark.gameObject.SetActive(showValue && useDarkText);
        coinTextWhite.gameObject.SetActive(showValue && !useDarkText);
    }

    // 用 LateUpdate 更稳一点：等所有 sortingOrder 都调完再跟随
    private void LateUpdate()
    {
        // ① 让 Canvas 的排序跟随这张卡的 sprite
        if (statsCanvas != null && cardSR != null)
        {
            statsCanvas.sortingLayerID = cardSR.sortingLayerID;
            statsCanvas.sortingOrder   = cardSR.sortingOrder + 1; // 永远比卡本身高一点
        }

        // ② 这张卡本来就不需要任何数值，直接退
        if (!showHP && !showHunger && !showValue)
            return;

        // ③ 不是这一叠的顶牌：隐藏文字（但不关 root）
        if (!card.isTopVisual)
        {
            SetStatsVisible(false);
            return;
        }

        // ④ 顶牌：显示文字
        SetStatsVisible(true);
        RefreshAll();
    }

    private void SetStatsVisible(bool visible)
    {
        if (showHP)
        {
            hpTextDark.gameObject.SetActive(visible && useDarkText);
            hpTextWhite.gameObject.SetActive(visible && !useDarkText);
        }
        else
        {
            hpTextDark.gameObject.SetActive(false);
            hpTextWhite.gameObject.SetActive(false);
        }

        if (showHunger)
        {
            hungerTextDark.gameObject.SetActive(visible && useDarkText);
            hungerTextWhite.gameObject.SetActive(visible && !useDarkText);
        }
        else
        {
            hungerTextDark.gameObject.SetActive(false);
            hungerTextWhite.gameObject.SetActive(false);
        }

        if (showValue)
        {
            coinTextDark.gameObject.SetActive(visible && useDarkText);
            coinTextWhite.gameObject.SetActive(visible && !useDarkText);
        }
        else
        {
            coinTextDark.gameObject.SetActive(false);
            coinTextWhite.gameObject.SetActive(false);
        }
    }

    private void RefreshAll()
    {
        if (showHP)
        {
            int hp = Mathf.Max(0, card.currentHP);
            if (hp != lastHp)
            {
                GetHpText().text = hp.ToString();
                lastHp = hp;
            }
        }

        if (showHunger)
        {
            int hunger = Mathf.Max(0, card.currentHunger);
            if (hunger != lastHunger)
            {
                GetHungerText().text = hunger.ToString();
                lastHunger = hunger;
            }
        }

        if (showValue)
        {
            int value = Mathf.Max(0, card.data.value);
            if (value != lastValue)
            {
                GetCoinText().text = value.ToString();
                lastValue = value;
            }
        }
    }

    private TMP_Text GetHpText()
    {
        return useDarkText ? hpTextDark : hpTextWhite;
    }

    private TMP_Text GetHungerText()
    {
        return useDarkText ? hungerTextDark : hungerTextWhite;
    }

    private TMP_Text GetCoinText()
    {
        return useDarkText ? coinTextDark : coinTextWhite;
    }

    public void ForceRefreshOnDataChanged()
    {
        lastHp = lastHunger = lastValue = int.MinValue;
        InitFromData();
        RefreshAll();
    }
}
