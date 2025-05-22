using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[AddComponentMenu("UI/HoverButton", 31)]
public class HoverButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Hover Settings")]

    [Tooltip("Time in seconds the pointer must hover before triggering the event.")]
    public float hoverDuration = 2f;

    [Tooltip("Event triggered after successful hover.")]
    public UnityEvent onHover;

    [Tooltip("Event triggered when hover is interrupted or completed.")]
    public UnityEvent onHoverEnd;

    private Coroutine hoverCoroutine;
    private bool isPointerOver = false;
    private bool wasHoverCompleted = false;

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerOver = true;
        wasHoverCompleted = false;
        if (hoverCoroutine == null)
        {
            Debug.Log("Hover started");
            hoverCoroutine = StartCoroutine(HoverRoutine());
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
        if (hoverCoroutine != null)
        {
            StopCoroutine(hoverCoroutine);
            hoverCoroutine = null;
        }
        
        // Trigger hover end event
        onHoverEnd?.Invoke();
        Debug.Log("Hover ended");
    }

    private IEnumerator HoverRoutine()
    {
        float timer = 0f;

        while (timer < hoverDuration)
        {
            if (!isPointerOver)
                yield break;

            timer += Time.deltaTime;
            yield return null;
        }

        wasHoverCompleted = true;
        onHover?.Invoke();
    }
}
