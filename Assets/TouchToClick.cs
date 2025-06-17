using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TouchToClick : MonoBehaviour
{
    public static List<TouchToClick> allButtons = new List<TouchToClick>();

    private Button button;
    private Image buttonImage;

    public Color selectedColor = Color.red;
    public Color normalColor = Color.white;

    public bool startSelected = false;

    void Awake()
    {
        allButtons.Add(this);
    }

    void OnDestroy()
    {
        allButtons.Remove(this);
    }

    void Start()
    {
        button = GetComponent<Button>();
        buttonImage = GetComponent<Image>();
        SetSelected(false);
        if(startSelected)
        {
            SetSelectedButton(this);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("HandHitbox"))
        {
            button.onClick.Invoke();
            SetSelectedButton(this);
        }
    }

    public static void SetSelectedButton(TouchToClick selected)
    {
        foreach (var btn in allButtons)
        {
            btn.SetSelected(btn == selected);
        }
    }

    public void SetSelected(bool isSelected)
    {
        if (buttonImage != null)
        {
            buttonImage.color = isSelected ? selectedColor : normalColor;
        }
    }
}
