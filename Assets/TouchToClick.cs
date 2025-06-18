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

    public Color isHoveringColor = Color.yellow;

    public bool startSelected = false;

    private bool isSelected = false;

    public UnityEngine.Events.UnityEvent onClick;

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
        if (startSelected)
        {
            SetSelectedButton(this);
        }
    }

    // void OnTriggerEnter(Collider other)
    // {
    //     if (other.CompareTag("HandHitbox"))
    //     {
    //         button.onClick.Invoke();
    //         SetSelectedButton(this);
    //     }
    // }

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
            this.isSelected = isSelected;
            if (isSelected)
            {
                onClick.Invoke();
            }
        }
    }

    public void SelectButton()
    {
        SetSelectedButton(this);
        Debug.Log("Button selected: " + gameObject.name);
    }

    public void HoveringButton()
    {
        if (buttonImage != null)
        {
            buttonImage.color = isHoveringColor;
        }
    }

    public void StopHoveringButton()
    {
        if (buttonImage != null && !isSelected)
        {
            buttonImage.color = normalColor;
        }
        else if (buttonImage != null && isSelected)
        {
            buttonImage.color = selectedColor;
        }
    }
}
