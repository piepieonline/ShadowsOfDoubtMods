using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib;

public class PieSolitaire_Game : MonoBehaviour
{
    static Transform youWinPanel;

    public static int completedSets = 0;

    void Start()
    {
        transform.parent.Find("Reset").GetComponentInChildren<Button>().onClick.AddListener(StartGame);
        foreach (var holder in transform.Find("DrawPile").GetComponentsInChildren<PieSolitaire_CardHolder>())
        {
            holder.holdFlat = true;
        }

        foreach (var holder in transform.Find("Shelf").GetComponentsInChildren<PieSolitaire_CardHolder>())
        {
            holder.holdFlat = true;
            holder.acceptChildTypes = PieSolitaire_CardHolder.AcceptChildTypes.NextUpSameSuit;
        }

        foreach (var holder in transform.Find("PlayArea").GetComponentsInChildren<PieSolitaire_CardHolder>())
        {
            holder.acceptChildTypes = PieSolitaire_CardHolder.AcceptChildTypes.NextDownOtherSuit;
        }

        youWinPanel = transform.Find("YouWinPanel");

        for (int i = 0; i < PieSolitaire_Card.deck.Length; i++)
        {
            if (!PieSolitaire_Card.deck[i])
            {
                var newCardGO = GameObject.Instantiate(PieSolitaire_Loader.CardPrefab);
                newCardGO.transform.SetParent(transform.Find("Deck").transform, false);
                var newCard = newCardGO.GetComponent<PieSolitaire_Card>();
                newCard.index = i;
                PieSolitaire_Card.deck[i] = newCard;
            }
        }

        StartGame();
    }

    void StartGame()
    {
        completedSets = 0;
        youWinPanel.gameObject.SetActive(false);

        foreach (var card in PieSolitaire_Card.deck)
        {
            card.transform.SetParent(transform.Find("Deck"), false);
            card.transform.localPosition = Vector3.zero;
            card.transform.localEulerAngles = new Vector3(0, 180, 0);
            card.transform.localScale = Vector3.one;
            card.Reset();
        }

        var dealSpotParent = transform.Find("PlayArea");
        var drawPileParent = transform.Find("DrawPile/CardPosition");

        var rnd = new System.Random();
        var randomDeck = PieSolitaire_Card.deck.OrderBy(x => rnd.Next()).ToArray();

        int i = 0;
        for (int x = 0; x < dealSpotParent.childCount; x++)
        {
            for (int y = 0; y <= x; y++)
            {
                dealSpotParent.GetChild(x).GetComponent<PieSolitaire_CardHolder>().AddCard(randomDeck[i].gameObject);
                if (y == x)
                {
                    randomDeck[i].Flip(true);
                    randomDeck[i].locked = false;
                }
                i++;
            }
        }

        for (; i < 52; i++)
        {
            drawPileParent.GetComponent<PieSolitaire_CardHolder>().AddCard(randomDeck[i].gameObject);
        }
    }

    public static void CompleteSet()
    {
        completedSets++;

        if (completedSets >= 4)
        {
            youWinPanel.gameObject.SetActive(true);
        }
    }
}
