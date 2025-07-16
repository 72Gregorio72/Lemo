using UnityEngine;

public class MemoryCard : MonoBehaviour
{
    private MemoryGameManager gameManager;
    private bool isRevealed = false;

    public GameObject front;
    public GameObject back;

    private Animator anim;

    public bool face = false;

    public bool backFace = true;

    public bool IsPermanentlyRevealed { get; private set; } = false;

    private AudioSource audioSource;
    public AudioClip flipSound;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        this.GetComponent<Animator>().SetBool("Face", face);
        this.GetComponent<Animator>().SetBool("Back", backFace);
    }
    public void Init(MemoryGameManager manager)
    {
        gameManager = manager;
        Hide();
    }

    public void OnSelect()
    {
        if (!isRevealed && !IsPermanentlyRevealed)
        {
            gameManager.CardSelected(this.gameObject);
        }
    }

    public void Reveal()
    {
        isRevealed = true;
        face = true;
        backFace = false;
        // front.SetActive(true);
        // back.SetActive(false);
    }

    public void Hide()
    {
        isRevealed = false;
        this.GetComponent<Animator>().SetTrigger("Hide");
        face = false;
        backFace = true;
        // front.SetActive(false);
        // back.SetActive(true);
    }

    public void WrongHide()
    {
        isRevealed = false;
        this.GetComponent<Animator>().SetTrigger("WrongSelect");
        face = false;
        backFace = true;
        // front.SetActive(false);
        // back.SetActive(true);
    }

    public void PlayFlipSound()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        if (audioSource != null && flipSound != null)
        {
            audioSource.PlayOneShot(flipSound);
        }
    }

	public void Remove()
	{
		this.GetComponent<Animator>().SetTrigger("RightSelect");
		isRevealed = true;
		IsPermanentlyRevealed = true;
		face = true;
		backFace = false;
		this.gameObject.GetComponent<CheckHitbox>().glowEffect.SetActive(false);
        // front.SetActive(true);
		// back.SetActive(false);
	}
}
