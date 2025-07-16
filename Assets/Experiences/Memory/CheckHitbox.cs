using System;
using UnityEngine;

public class CheckHitbox : MonoBehaviour
{
    private MemoryGameManager gameManager;
	
	public GameObject glowEffect;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
        GameObject gameManagerObject = GameObject.FindGameObjectWithTag("GameManager");
		glowEffect.SetActive(false);
        if (gameManagerObject != null)
		{
			gameManager = gameManagerObject.GetComponent<MemoryGameManager>();
		}
        if (gameManager == null)
        {
            Debug.LogError("MemoryGameManager non trovato nella scena!");
            return;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger: " + other.name);
        if (other.CompareTag("HandHitbox"))
        {
			if (!this.gameObject.GetComponent<MemoryCard>().IsPermanentlyRevealed)
			{
				gameManager.CardSelected(this.gameObject);
				glowEffect.SetActive(true);
            }
			else
			{
				Debug.Log("Carta già rivelata!");
			} 
        }
    }
}
