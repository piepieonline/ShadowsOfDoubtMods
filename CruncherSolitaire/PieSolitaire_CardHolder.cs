using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib;

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
                return newCard.suit == currentCard.suit && newCard.number == currentCard.number + 1 && card.transform.parent.GetChild(card.transform.parent.childCount - 1) == card.transform;
            case AcceptChildTypes.NextDownOtherSuit:
                return newCard.suit % 2f != currentCard.suit % 2f && newCard.number == currentCard.number - 1;
        }

        return false;
    }

    public void AddCard(GameObject card)
    {
        card.transform.SetParent(transform);

        var offset = Vector3.zero;

        if (!holdFlat)
        {
            if (transform.childCount > 2)
            {
                offset += transform.GetChild(transform.childCount - 2).localPosition;

                if (transform.childCount > 1 && transform.GetChild(transform.childCount - 1).GetComponent<PieSolitaire_Card>().faceUp)
                {
                    offset += Vector3.down * 0.04f;
                }
                else
                {
                    offset += Vector3.down * 0.02f;
                }
            }
        }

        card.transform.localPosition = offset;
        card.transform.localEulerAngles = new Vector3(0, 180, 0);

        if (acceptChildTypes == AcceptChildTypes.NextUpSameSuit && transform.childCount == 14)
        {
            PieSolitaire_Game.CompleteSet();
        }
    }

    public void UnlockLast()
    {
        if (transform.childCount >= 2)
        {
            transform.GetChild(transform.childCount - 1).GetComponent<PieSolitaire_Card>().locked = false;
        }
    }

    public void Click()
    {
        if (transform.childCount == 1 && PieSolitaire_Card.selectedIndex >= 0 && WillAcceptCard(PieSolitaire_Card.deck[PieSolitaire_Card.selectedIndex].gameObject))
        {
            var currentCardHolder = PieSolitaire_Card.deck[PieSolitaire_Card.selectedIndex].GetComponentInParent<PieSolitaire_CardHolder>();
            AddCard(PieSolitaire_Card.deck[PieSolitaire_Card.selectedIndex].gameObject);
            currentCardHolder?.UnlockLast();
            PieSolitaire_Card.SelectCard(-1);
        }
    }
}
