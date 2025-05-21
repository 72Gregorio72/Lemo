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

    private Coroutine hoverCoroutine;
    private bool isPointerOver = false;

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerOver = true;
        if (hoverCoroutine == null)
            Debug.Log("Hover started");
            hoverCoroutine = StartCoroutine(HoverRoutine());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
        if (hoverCoroutine != null)
        {
            StopCoroutine(hoverCoroutine);
            hoverCoroutine = null;
        }
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

        onHover?.Invoke();
    }
}
