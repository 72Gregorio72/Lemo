using UnityEngine;

public class LayerSelector : MonoBehaviour
{
    public int layerToSelect = 0; // Default to the first layer
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GetComponent<Canvas>().sortingOrder = layerToSelect;
    }


    // Update is called once per frame
    void Update()
    {
        
    }
}
