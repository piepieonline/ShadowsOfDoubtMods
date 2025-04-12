using CruncherSolitaire;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib;

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
            for (var i = Mathf.Min(cardsInPile, (CruncherSolitairePlugin.OneCardDraw.Value ? 1 : 3)); i > 0; i--)
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
