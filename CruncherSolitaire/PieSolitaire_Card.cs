using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib;

public class PieSolitaire_Card : MonoBehaviour
{
    public static PieSolitaire_Card[] deck = new PieSolitaire_Card[52];
    public static int selectedIndex = -1;
    public static PieSolitaire_Card selectedCard;

    public int index;
    public int number;
    public int suit;

    public Button clickable;

    Sprite visibleSprite;
    Sprite backSprite;

    public bool faceUp = false;
    public bool locked = true;

    private void Awake()
    {
        clickable = GetComponentInChildren<Button>();
        backSprite = PieSolitaire_Loader.CardSpriteBack;
        clickable.GetComponent<Image>().sprite = backSprite;

        clickable.onClick.AddListener(Click);
    }

    private void Start()
    {
        SetCardFromInt(index);
        clickable = GetComponentInChildren<Button>();
        visibleSprite = PieSolitaire_Loader.CardSprites[this.name];
        backSprite = PieSolitaire_Loader.CardSpriteBack;
        clickable.GetComponent<Image>().sprite = faceUp ? visibleSprite : backSprite;
    }

    void SetCardFromInt(int i)
    {
        Dictionary<int, string> numToName = new Dictionary<int, string>()
        {
            { 0, "ace" },
            { 10, "jack" },
            { 11, "queen" },
            { 12, "king" }
        };

        Dictionary<int, string> numToSuit = new Dictionary<int, string>()
        {
            { 0, "clubs" },
            { 1, "diamonds" },
            { 2, "spades" },
            { 3, "hearts" }
        };


        // 3_of_clubs
        number = i % 13;
        string cardName = numToName.TryGetValue(number, out var name) ? name : ((i % 13) + 1).ToString();
        suit = i / 13;
        string cardSuit = numToSuit[suit];

        gameObject.name = $"{cardName}_of_{cardSuit}";
    }

    public void Reset()
    {
        selectedIndex = -1;
        Flip(false);
        locked = true;
    }

    public void Click()
    {
        if (clickable == null) return;

        if (faceUp)
        {
            if (selectedIndex < 0)
            {
                if (!locked)
                {
                    SelectCard(index);
                }
            }
            else if (selectedIndex == index)
            {
                SelectCard(-1);
            }
            else
            {
                var newStack = deck[index].gameObject.GetComponentInParent<PieSolitaire_CardHolder>();
                if (newStack && newStack.WillAcceptCard(deck[selectedIndex].gameObject))
                {
                    var currentStack = deck[selectedIndex].gameObject.GetComponentInParent<PieSolitaire_CardHolder>();

                    if (currentStack)
                    {
                        var moving = false;
                        for (int i = 1; i < currentStack.transform.childCount; i++)
                        {
                            var currentCard = currentStack.transform.GetChild(i).GetComponent<PieSolitaire_Card>();

                            if (currentCard.index == selectedIndex)
                            {
                                moving = true;
                            }

                            if (moving)
                            {
                                newStack.AddCard(currentCard.gameObject);
                                i--;
                            }
                        }

                        currentStack.UnlockLast();
                    }
                    else
                    {
                        newStack.AddCard(deck[selectedIndex].gameObject);
                    }

                    if (newStack.acceptChildTypes == PieSolitaire_CardHolder.AcceptChildTypes.NextUpSameSuit)
                        deck[selectedIndex].locked = true;


                    SelectCard(-1);
                }
                else
                {
                    if (!locked)
                    {
                        SelectCard(index);
                    }
                }
            }
        }
        else
        {
            if (!locked)
            {
                Flip(true);
            }
        }
    }

    public void Flip(bool show)
    {
        clickable.GetComponent<Image>().sprite = show ? visibleSprite : backSprite;
        faceUp = show;
    }

    public static void SelectCard(int index)
    {
        if (selectedIndex != -1)
        {
            var cBlock = deck[selectedIndex].clickable.colors;
            cBlock.normalColor = Color.white;
            cBlock.selectedColor = Color.white;
            deck[selectedIndex].clickable.colors = cBlock;
        }

        selectedIndex = index;

        if (selectedIndex != -1)
        {
            var cBlock = deck[selectedIndex].clickable.colors;
            cBlock.normalColor = new Color(.78f, .78f, .78f);
            cBlock.selectedColor = new Color(.78f, .78f, .78f);
            deck[selectedIndex].clickable.colors = cBlock;
        }
    }
}