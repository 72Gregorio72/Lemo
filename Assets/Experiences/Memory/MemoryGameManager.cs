using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MemoryGameManager : MonoBehaviour
{
    [Header("Prefab già pronti da usare (9 GameObject):")]
    public List<GameObject> cardsToUse; // 9 prefab: 4 coppie + 1 nera

    [Header("Posizioni 3x3 dove spawnare")]
    public List<Transform> cardPositions; // 9 posizioni

    private List<GameObject> spawnedCards = new List<GameObject>();
    private List<GameObject> selectedCards = new List<GameObject>();
    private GameObject cardsContainer; // Container for all spawned cards

    public TextMeshProUGUI pointsText; // Riferimento al testo dei punti (se necessario)

    private int points = 0; // Punti totali (se necessario)

    private int winCount = 0; // Contatore per le coppie trovate (se necessario)

    public TextMeshProUGUI winPointsText; // Riferimento al testo dei punti di vittoria (se necessario)

    private int matchCount = 0;

    private GameObject selectedCard1;
    private GameObject selectedCard2;

    private AudioSource audioSource;

    public AudioClip correctSound; // Clip audio per il suono di flip

    public AudioClip wrongSound; // Clip audio per il su

    private bool isGameOver = false;

    void Start()
    {
        // Create the container GameObject if it doesn't exist
        CreateCardsContainer();
        
        winPointsText.text = "Partite vinte: " + winCount.ToString();
        pointsText.text = "Coppie trovate: " + points.ToString() + "/4";
        StartNewRound();
    }

    private void CreateCardsContainer()
    {
        // Destroy existing container if it exists
        if (cardsContainer != null)
        {
            Destroy(cardsContainer);
        }

        // Create new container
        cardsContainer = new GameObject("Cards_Container");
        cardsContainer.transform.SetParent(transform); // Parent to the MemoryGameManager
        cardsContainer.transform.localPosition = Vector3.zero;
        cardsContainer.transform.localRotation = Quaternion.identity;
    }

    public void StartNewRound()
    {
        points = 0;
        pointsText.text = "Coppie trovate: " + points.ToString() + "/4";
        if (cardsToUse.Count != 9 || cardPositions.Count != 9)
        {
            Debug.LogError("Devi assegnare esattamente 9 carte e 9 posizioni!");
            return;
        }

        matchCount = 0;
        ClearCards();

        // Create new container for the new round
        CreateCardsContainer();

        List<GameObject> shuffledCards = new List<GameObject>(cardsToUse);
        shuffledCards.Shuffle(); // Estensione usata prima

        for (int i = 0; i < 9; i++)
        {
            StartCoroutine(InstantiateCardWithDelay(i * 0.1f, shuffledCards[i], cardPositions[i].position, cardPositions[i].gameObject));
        }

        // Mostra tutte le carte per 2 secondi all'inizio
        ShowAllCardsTemporarily();
        Invoke("SetGameOver", 1f);
    }

    void SetGameOver()
    {
        isGameOver = false;
    }

    IEnumerator InstantiateCardWithDelay(float delay, GameObject cardPrefab, Vector3 position, GameObject cardPosition)
    {
        yield return new WaitForSeconds(delay);
        InstantiateCard(cardPrefab, position, cardPosition);
    }

    void InstantiateCard(GameObject cardPrefab, Vector3 position, GameObject cardPosition)
    {
        GameObject card = Instantiate(cardPrefab, position, Quaternion.identity, cardsContainer.transform);
        card.GetComponent<MemoryCard>().Init(this);
        card.GetComponent<FollowCameraHeight>().currentRow = cardPosition;
        card.transform.localScale = new Vector3(1f, 1f, 1f); // Imposta la dimensione della carta
        spawnedCards.Add(card);
    }

    void ClearCards()
    {
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            if (spawnedCards[i] != null)
                StartCoroutine(DestroyCardWithDelay(i * 0.1f, spawnedCards[i]));
        }
        spawnedCards.Clear();
        selectedCards.Clear();

        // Destroy the container (it will be recreated in StartNewRound)
        if (cardsContainer != null)
        {
            Destroy(cardsContainer);
        }
    }

    IEnumerator DestroyCardWithDelay(float delay, GameObject card)
    {
        yield return new WaitForSeconds(delay);
        Destroy(card);
    }

    public void CardSelected(GameObject card)
    {
        if (selectedCards.Contains(card) || selectedCards.Count >= 2) return;

        selectedCards.Add(card);
        card.GetComponent<MemoryCard>().Reveal();

        if (card.CompareTag("Black") && !isGameOver)
        {
            isGameOver = true;
            selectedCards.Clear();
            Invoke(nameof(StartNewRound), 2f);
            return;
        }
        else if (selectedCards.Count == 2)
        {
            Invoke(nameof(CheckMatch), 2f);
        }
    }

    void CheckMatch()
    {
        GameObject card1 = selectedCards[0];
        GameObject card2 = selectedCards[1];

        if (card1.tag == card2.tag)
        {
            card1.GetComponent<MemoryCard>().Remove();
            card2.GetComponent<MemoryCard>().Remove();
            matchCount++;
            points++;
            pointsText.text = "Coppie trovate: " + points.ToString() + "/4";
            audioSource = this.gameObject.GetComponent<AudioSource>();
            if (audioSource != null && correctSound != null)
            {
                audioSource.PlayOneShot(correctSound);
            }
            if (matchCount == 4)
            {
                winCount++;
                winPointsText.text = "Partite vinte: " + winCount.ToString();
                Invoke(nameof(StartNewRound), 1f);
            }
        }
        else
        {
            selectedCard1 = selectedCards[0];
            selectedCard2 = selectedCards[1];
            Invoke(nameof(HideCards), 1f);
        }
        selectedCards.Clear();
    }

    void HideCards()
    {
        audioSource = this.gameObject.GetComponent<AudioSource>();
        if (audioSource != null && correctSound != null)
        {
            audioSource.PlayOneShot(wrongSound);
        }
        selectedCard1.GetComponent<MemoryCard>().WrongHide();
        selectedCard2.GetComponent<MemoryCard>().WrongHide();
    }

    void ShowAllCardsTemporarily()
    {
        foreach (var card in spawnedCards)
        {
            if (card != null)
            {
                var memoryCard = card.GetComponent<MemoryCard>();
                if (memoryCard != null)
                    memoryCard.Reveal();
            }
        }
        Invoke(nameof(HideAllCards), 2f);
        Invoke(nameof(ShowBlackCard), 2f);
    }

    void ShowBlackCard()
    {
        foreach (var card in spawnedCards)
        {
            if (card != null && card.CompareTag("Black"))
            {
                var memoryCard = card.GetComponent<MemoryCard>();
                if (memoryCard != null)
                    memoryCard.Reveal();
            }
        }
        Invoke(nameof(HideBlackCard), 1f);
    }

    void HideBlackCard()
    {
        foreach (var card in spawnedCards)
        {
            if (card != null && card.CompareTag("Black"))
            {
                var memoryCard = card.GetComponent<MemoryCard>();
                if (memoryCard != null)
                    memoryCard.Hide();
            }
        }
    }

    void HideAllCards()
    {
        foreach (var card in spawnedCards)
        {
            if (card != null)
            {
                var memoryCard = card.GetComponent<MemoryCard>();
                if (memoryCard != null)
                    memoryCard.Hide();
            }
        }
    }

    public List<GameObject> GetSpawnedCards()
    {
        return new List<GameObject>(spawnedCards);
    }

}
