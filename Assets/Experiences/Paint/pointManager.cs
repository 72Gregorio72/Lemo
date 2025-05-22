using UnityEngine;
using TMPro;

public class pointManager : MonoBehaviour
{
    public TextMeshProUGUI textMeshPro;

    private int points = 0;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("PaintBall"))
        {
            points++;
            textMeshPro.text = points.ToString();
            Destroy(other.gameObject);
        }
    }
}
