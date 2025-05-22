using UnityEngine;
using UnityEngine.SceneManagement;

public class ReturnHome : MonoBehaviour
{
    [SerializeField] private float activationDelay = 0f; // Optional delay before scene load
    
    private void Start()
    {
        // Optional: Add automatic activation after the delay
        if (activationDelay > 0)
        {
            Invoke(nameof(ReturnToHomeScene), activationDelay);
        }
    }
    
    // This can be called directly or via button click/event
    public void ReturnToHomeScene()
    {
        Debug.Log("Returning to home scene (index 1)");
        SceneManager.LoadScene(1);
    }

    
} 