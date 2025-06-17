using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

public class SelfDestructIfEmpty : MonoBehaviour
{
    public float checkDelay = 0.1f;
    public float destroyDelay = 1f;

    private LineRenderer line;

    void Start()
    {
        line = GetComponent<LineRenderer>();
        InvokeRepeating(nameof(CheckAndDestroy), checkDelay, checkDelay);
    }

    void CheckAndDestroy()
    {
        if (line == null || line.positionCount == 0)
        {
            CancelInvoke(nameof(CheckAndDestroy));
            gameObject.SetActive(false);
        }
    }
}
