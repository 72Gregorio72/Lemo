using UnityEngine;
using UnityEngine.UI;

public class TouchToClick : MonoBehaviour
{
    private Button button;

    void Start()
    {
        button = GetComponent<Button>();
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("Collider entered: " + other.name);
        if (other.CompareTag("HandHitbox"))  // Assicurati che il controller abbia il tag "Hand"
        {
            Debug.Log("Button clicked: " + button.name);
            button.onClick.Invoke();
        }
    }
}
