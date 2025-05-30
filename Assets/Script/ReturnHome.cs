using UnityEngine;
using UnityEngine.SceneManagement;

public class ReturnHome : MonoBehaviour
{


    private void Start()
    {

    }

    // This can be called directly or via button click/event
    public void ReturnToHomeScene()
    {
        Debug.Log("Returning to home scene (index 1)");
        SceneManager.LoadScene(1);
    }


}