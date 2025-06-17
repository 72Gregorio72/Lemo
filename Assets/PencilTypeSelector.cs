using UnityEngine;

public class PencilTypeSelector : MonoBehaviour
{
    public bool isPencil = true; // True per matita, false per penna
    public bool isEraser = false; // True per gomma, false per matita/penna

    public bool isErasingLines = false; // True se si sta cancellando una linea, false altrimenti
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void TogglePencil()
    {
        SetAllFalse(); // Disabilita tutti gli strumenti
        isPencil = true; // Abilita matita
        Debug.Log("Pencil selected: " + isPencil);
    }

    public void ToggleEraser()
    {
        SetAllFalse(); // Disabilita tutti gli strumenti
        isEraser = true; // Abilita gomma
        Debug.Log("Eraser selected: " + isEraser);
    }

    public void ToggleErasingLines()
    {
        SetAllFalse(); // Disabilita tutti gli strumenti
        isErasingLines = true; // Abilita cancellazione linee
        Debug.Log("Erasing lines: " + isErasingLines);
    }
    
    public void SetAllFalse()
    {
        isPencil = false; // Disabilita matita
        isEraser = false; // Disabilita gomma
        isErasingLines = false; // Disabilita cancellazione linee
        Debug.Log("All tools disabled");
    }
}
