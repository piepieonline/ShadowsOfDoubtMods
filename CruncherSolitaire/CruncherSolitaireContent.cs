using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib;

public class SolitaireCruncherAppPrefab : CruncherAppContent
{
    // This one seems to be retired?
    public override void Setup(ComputerController cc)
    {
        base.controller = cc;
        DoSetup();
    }

    public override void OnSetup()
    {
        DoSetup();
    }

    private void DoSetup()
    {
        GetComponentsInChildren<UnityEngine.UI.Button>().Where(button => button.name == "Exit").FirstOrDefault().onClick.AddListener(() => controller.OnAppExit());
    }

    public override void PrintButton()
    {
    }
}

/*
public class PieSolitaire_CardHolder : MonoBehaviour
{
    public bool holdFlat;

    public enum AcceptChildTypes
    {
        None,
        NextUpSameSuit,
        NextDownOtherSuit
    }

    public AcceptChildTypes acceptChildTypes;

    private void Awake()
    {
        GetComponentInChildren<Button>().onClick.AddListener(Click);
    }

    public bool WillAcceptCard(GameObject card)
    {
        var newCard = card.GetComponent<PieSolitaire_Card>();
        var currentCard = transform.GetChild(transform.childCount - 1).GetComponent<PieSolitaire_Card>();

        if (currentCard == null)
        {
            switch (acceptChildTypes)
            {
                case AcceptChildTypes.NextUpSameSuit:
                    return newCard.number == 0;
                case AcceptChildTypes.NextDownOtherSuit:
                    return newCard.number == 12;
            }
        }

        switch (acceptChildTypes)
        {
            case AcceptChildTypes.NextUpSameSuit:
                return newCard.suit == currentCard.suit && newCard.number == currentCard.number + 1;
            case AcceptChildTypes.NextDownOtherSuit:
                return newCard.suit / 2f != currentCard.suit / 2f && newCard.number == currentCard.number - 1;
        }

        return false;
    }

    public void AddCard(GameObject card)
    {
        card.transform.SetParent(transform);
        card.transform.localPosition = holdFlat ? Vector3.zero : Vector3.zero + ((Vector3.down * 0.05f) * (transform.childCount - 2));
        card.transform.localEulerAngles = new Vector3(0, 180, 0);
    }

    public void UnlockLast()
    {
        if (transform.childCount > 2)
        {
            transform.GetChild(transform.childCount - 2).GetComponent<PieSolitaire_Card>().locked = false;
        }
    }

    public void Click()
    {
        if (transform.childCount == 1 && PieSolitaire_Card.selectedIndex >= 0 && WillAcceptCard(PieSolitaire_Card.deck[PieSolitaire_Card.selectedIndex].gameObject))
        {
            PieSolitaire_Card.deck[PieSolitaire_Card.selectedIndex].gameObject.GetComponentInParent<PieSolitaire_CardHolder>()?.UnlockLast();
            AddCard(PieSolitaire_Card.deck[PieSolitaire_Card.selectedIndex].gameObject);
            PieSolitaire_Card.SelectCard(-1);
        }
    }
}

public class PieSolitaire_Card : MonoBehaviour
{
    public static PieSolitaire_Card[] deck;
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

    private void Start()
    {
        SetCardFromInt(index);
        clickable = GetComponentInChildren<Button>();
        visibleSprite = PieSolitaire_Loader.CardSprites[this.name];
        backSprite = PieSolitaire_Loader.CardSpriteBack;
        clickable.GetComponent<Image>().sprite = faceUp ? visibleSprite : backSprite;

        clickable.onClick.AddListener(Click);
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
            { 2, "hearts" },
            { 3, "spades" }
        };


        // 3_of_clubs
        number = i % 13;
        string cardName = numToName.TryGetValue(number, out var name) ? name : ((i % 13) + 1).ToString();
        suit = i / 13;
        string cardSuit = numToSuit[suit];

        gameObject.name = $"{cardName}_of_{cardSuit}";
    }

    public void Click()
    {
        if (clickable == null || locked) return;

        if (faceUp)
        {
            if (selectedIndex < 0)
            {
                SelectCard(index);
            }
            else if (selectedIndex == index)
            {
                SelectCard(-1);
            }
            else
            {
                var currentStack = deck[index].gameObject.GetComponentInParent<PieSolitaire_CardHolder>();
                if (currentStack && currentStack.WillAcceptCard(deck[selectedIndex].gameObject))
                {
                    deck[selectedIndex].gameObject.GetComponentInParent<PieSolitaire_CardHolder>()?.UnlockLast(); // todo unlock stack

                    var newHolder = deck[index].gameObject.GetComponentInParent<PieSolitaire_CardHolder>();
                    newHolder.AddCard(deck[selectedIndex].gameObject);

                    if (newHolder.acceptChildTypes == PieSolitaire_CardHolder.AcceptChildTypes.NextUpSameSuit)
                        deck[selectedIndex].locked = true;

                    SelectCard(-1);
                }
            }
        }
        else
        {
            Flip(true);
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
            cBlock.normalColor = cBlock.pressedColor;
            cBlock.selectedColor = cBlock.pressedColor;
            deck[selectedIndex].clickable.colors = cBlock;
        }

        selectedIndex = index;

        if (selectedIndex != -1)
        {
            var cBlock = deck[selectedIndex].clickable.colors;
            cBlock.normalColor = Color.white;
            cBlock.selectedColor = Color.white;
            deck[selectedIndex].clickable.colors = cBlock;
        }
    }
}

public class PieSolitaire_Game : MonoBehaviour
{
    void Start()
    {
        if (PieSolitaire_Card.deck == null || PieSolitaire_Card.deck.Length == 0)
        {
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

            PieSolitaire_Card.deck = new PieSolitaire_Card[52];

            var deckGO = new GameObject("Deck");
            deckGO.transform.SetParent(transform);

            for (int i = 0; i < PieSolitaire_Card.deck.Length; i++)
            {
                var newCardGO = GameObject.Instantiate(PieSolitaire_Loader.CardPrefab);
                newCardGO.transform.SetParent(deckGO.transform, false);
                var newCard = newCardGO.GetComponent<PieSolitaire_Card>();
                newCard.index = i;
                PieSolitaire_Card.deck[i] = newCard;
            }
        }

        StartGame();
    }

    void StartGame()
    {
        var dealSpotParent = transform.Find("PlayArea");
        var drawPileParent = transform.Find("DrawPile/CardPosition");

        var rnd = new System.Random();
        var randomDeck = PieSolitaire_Card.deck.OrderBy(x => rnd.Next()).ToArray();

        int i = 0;
        for (int x = 1; x < dealSpotParent.childCount; x++)
        {
            for (int y = 0; y < x; y++)
            {
                dealSpotParent.GetChild(x).GetComponent<PieSolitaire_CardHolder>().AddCard(randomDeck[i].gameObject);
                if (y == x - 1)
                {
                    randomDeck[i].faceUp = true;
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
}

public class PieSolitaire_CardDeck : MonoBehaviour
{
    Transform deckContainer;
    Transform[] displayContainers = new Transform[3];

    private void Awake()
    {
        GetComponentInChildren<Button>().onClick.AddListener(Click);

        deckContainer = transform.parent.GetChild(0);
        displayContainers[0] = transform.parent.GetChild(1).GetChild(2);
        displayContainers[1] = transform.parent.GetChild(1).GetChild(1);
        displayContainers[2] = transform.parent.GetChild(1).GetChild(0);
    }

    public void Click()
    {
        int cardsInPile = deckContainer.childCount - 1;
        if (displayContainers[1].childCount > 0)
            displayContainers[1].GetChild(0).SetParent(displayContainers[2], false);
        if (displayContainers[0].childCount > 0)
            displayContainers[0].GetChild(0).SetParent(displayContainers[2], false);

        if (cardsInPile > 0)
        {
            for (var i = Mathf.Min(cardsInPile, 3); i > 0; i--)
            {
                var cardTransform = deckContainer.GetChild(deckContainer.childCount - 1);
                cardTransform.SetParent(displayContainers[i - 1], false);
                cardTransform.GetComponent<PieSolitaire_Card>().Flip(true);
                cardTransform.GetComponent<PieSolitaire_Card>().locked = false;
            }
        }
        else
        {
            for (var i = displayContainers[2].childCount - 1; i >= 0; i--)
            {
                var cardTransform = displayContainers[2].GetChild(i);
                cardTransform.SetParent(deckContainer, false);
                cardTransform.GetComponent<PieSolitaire_Card>().Flip(false);
                cardTransform.GetComponent<PieSolitaire_Card>().locked = false;
            }
        }
    }
}
*/