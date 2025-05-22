using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MemoryGameManagerNumbers : MonoBehaviour
{
    // [Header("Prefab della tessera numerica")]
    // public GameObject numberCardPrefab;

    // [Header("Posizioni delle carte (9 elementi)")]
    // public List<Transform> cardPositions;

    // private List<GameObject> spawnedCards = new List<GameObject>();
    // private List<GameObject> selectedCards = new List<GameObject>();

    // private int matchCount = 0;

    // void Start()
    // {
    //     StartNewRound();
    // }

    // void StartNewRound()
    // {
    //     ClearCards();

    //     // Prepara numeri: 1-4 a coppie, più 9 come bomba
    //     List<int> numbers = new List<int>();
    //     for (int i = 1; i <= 4; i++)
    //     {
    //         numbers.Add(i);
    //         numbers.Add(i);
    //     }
    //     numbers.Add(9); // la "nera"

    //     numbers.Shuffle(); // mescola i numeri

    //     for (int i = 0; i < 9; i++)
    //     {
    //         GameObject card = Instantiate(numberCardPrefab, cardPositions[i].position, Quaternion.identity);
    //         card.GetComponent<NumberCard>().Init(this, numbers[i]);
    //         spawnedCards.Add(card);
    //     }

    //     matchCount = 0;
    // }

    // void ClearCards()
    // {
    //     foreach (var card in spawnedCards)
    //     {
    //         Destroy(card);
    //     }
    //     spawnedCards.Clear();
    //     selectedCards.Clear();
    // }

    // public void CardSelected(GameObject card)
    // {
    //     if (selectedCards.Contains(card) || selectedCards.Count >= 2)
    //         return;

    //     selectedCards.Add(card);
    //     card.GetComponent<NumberCard>().Reveal();

    //     if (selectedCards.Count == 2)
    //     {
    //         Invoke(nameof(CheckMatch), 1f);
    //     }
    // }

    // void CheckMatch()
    // {
    //     var card1 = selectedCards[0];
    //     var card2 = selectedCards[1];

    //     int val1 = card1.GetComponent<NumberCard>().cardValue;
    //     int val2 = card2.GetComponent<NumberCard>().cardValue;

    //     if (val1 == 9 || val2 == 9)
    //     {
    //         Debug.Log("Hai selezionato il 9! GAME OVER");
    //         StartNewRound();
    //         return;
    //     }

    //     if (val1 == val2)
    //     {
    //         Debug.Log("Match: " + val1);
    //         card1.GetComponent<NumberCard>().Remove();
    //         card2.GetComponent<NumberCard>().Remove();
    //         matchCount++;

    //         if (matchCount == 4)
    //         {
    //             Debug.Log("Hai vinto! Tutte le coppie trovate.");
    //             StartNewRound();
    //         }
    //     }
    //     else
    //     {
    //         card1.GetComponent<NumberCard>().Hide();
    //         card2.GetComponent<NumberCard>().Hide();
    //     }

    //     selectedCards.Clear();
    // }
}
