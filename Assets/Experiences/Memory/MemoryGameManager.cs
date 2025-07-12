using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class MemoryGameManager : MonoBehaviour
{
    [Header("Impostazioni automatiche")]
    public List<GameObject> cardPrefabs; // Prefab delle carte (colori + Black)
    public int numberOfCards = 6; // Numero di carte da giocare, impostabile da Inspector
    public Transform centerPoint; // Centro del tavolo
    public float radius = 3f; // Raggio del cerchio delle carte

    [Header("UI")]
    public TextMeshProUGUI pointsText;
    public TextMeshProUGUI winPointsText;

    private List<GameObject> spawnedCards = new List<GameObject>();
    private List<GameObject> selectedCards = new List<GameObject>();
    private GameObject cardsContainer;

    private int points = 0;
    private int winCount = 0;
    private int matchCount = 0;
    private int totalPairs = 0;

    private GameObject selectedCard1;
    private GameObject selectedCard2;

    private AudioSource audioSource;
    public AudioClip correctSound;
    public AudioClip wrongSound;

    private bool isGameOver = false;

    void Start()
    {
        CreateCardsContainer();
        winPointsText.text = "Partite vinte: " + winCount.ToString();
        pointsText.text = "Coppie trovate: " + points.ToString();
        StartNewRound();
    }

    private void CreateCardsContainer()
    {
        if (cardsContainer != null)
            Destroy(cardsContainer);

        cardsContainer = new GameObject("Cards_Container");
        cardsContainer.transform.SetParent(transform);
        cardsContainer.transform.localPosition = Vector3.zero;
        cardsContainer.transform.localRotation = Quaternion.identity;
    }

    public void StartNewRound()
    {
        points = 0;
        matchCount = 0;
        ClearCards();
        CreateCardsContainer();

        List<GameObject> cardsToSpawn = new List<GameObject>();
        totalPairs = numberOfCards / 2;
        bool hasBomb = numberOfCards % 2 != 0;

		// Crea le coppie
        // Seleziona solo prefab che NON sono "Black" per le coppie
        var nonBlackPrefabs = cardPrefabs.Where(c => !c.CompareTag("Black")).ToList();
        for (int i = 0; i < totalPairs; i++)
        {
            GameObject prefab = nonBlackPrefabs[i % nonBlackPrefabs.Count];
            cardsToSpawn.Add(prefab);
            cardsToSpawn.Add(prefab);
        }

        // Aggiungi la bomba se dispari
        if (hasBomb)
        {
            GameObject bombPrefab = cardPrefabs.Find(c => c.CompareTag("Black"));
            if (bombPrefab != null)
                cardsToSpawn.Add(bombPrefab);
        }

        // Mischia le carte
        cardsToSpawn = cardsToSpawn.OrderBy(x => Random.value).ToList();

		// Calcola posizioni in cerchio ruotato di 90 gradi sull'asse Y
		for (int i = 0; i < cardsToSpawn.Count; i++)
		{
			float angle = i * Mathf.PI * 2 / cardsToSpawn.Count;
			// Ruota il cerchio di 90 gradi sull'asse Y (asse verticale)
			float rotatedAngle = angle + Mathf.PI / 2;
			Vector3 pos = centerPoint.position + new Vector3(Mathf.Sin(rotatedAngle), Mathf.Cos(rotatedAngle), 0) * radius;
			InstantiateCard(cardsToSpawn[i], pos);
		}

        pointsText.text = $"Coppie trovate: {points}/{totalPairs}";
        ShowAllCardsTemporarily();
        Invoke("SetGameOver", 1f);
    }

    void SetGameOver()
    {
        isGameOver = false;
    }

    void InstantiateCard(GameObject cardPrefab, Vector3 position)
    {
        GameObject card = Instantiate(cardPrefab, position, Quaternion.identity, cardsContainer.transform);
        card.GetComponent<MemoryCard>().Init(this);
        card.transform.localScale = new Vector3(1f, 1f, 1f);
        spawnedCards.Add(card);
    }

    void ClearCards()
    {
        foreach (var card in spawnedCards)
        {
            if (card != null)
                Destroy(card);
        }
        spawnedCards.Clear();
        selectedCards.Clear();

        if (cardsContainer != null)
            Destroy(cardsContainer);
    }

    public void CardSelected(GameObject card)
    {
        if (selectedCards.Contains(card) || selectedCards.Count >= 2 || isGameOver)
            return;

        selectedCards.Add(card);
        card.GetComponent<MemoryCard>().Reveal();

        if (card.CompareTag("Black"))
        {
            isGameOver = true;
            selectedCards.Clear();
            Invoke(nameof(StartNewRound), 2f);
            return;
        }
        else if (selectedCards.Count == 2)
        {
            Invoke(nameof(CheckMatch), 1f);
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
            pointsText.text = $"Coppie trovate: {points}/{totalPairs}";
            audioSource = this.gameObject.GetComponent<AudioSource>();
            if (audioSource != null && correctSound != null)
                audioSource.PlayOneShot(correctSound);

            if (matchCount == totalPairs)
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
        if (audioSource != null && wrongSound != null)
            audioSource.PlayOneShot(wrongSound);

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
