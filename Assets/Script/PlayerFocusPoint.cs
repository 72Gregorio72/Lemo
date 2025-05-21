using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerFocusPoint : MonoBehaviour
{
    public float ViewDistance=3f;

    public Transform FocusPointTransform;
    public Transform FocusPointPlaneTransform;

    Transform player;

    Vector3 viewPoint;
    Vector3 viewPointOnPlane;
    Vector3 viewDirectionOnPlane;

    Vector3 focusPointOnPlane;
    Vector3 focusDirectionOnPlane;

    Vector3 focusPoint;
    Vector3 focusDirection;

    Vector3 velocity;
    bool tooFar = false;


    public Vector3 FocusPointOnPlane { get{return focusPointOnPlane;}}
    public Vector3 FocusDirectionOnPlane { get { return focusDirectionOnPlane; }}
    public Vector3 FocusPoint { get { return focusPoint; } }
    public Vector3 FocusDirection { get { return focusDirection; } }



    void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;

        UpdateViewPoint();
    }

    private void OnEnable()
    {
        SetFocusPoint();
    }


    private void Update()
    {
        UpdateViewPoint();


        var distance = Vector3.Distance(viewPointOnPlane, focusPointOnPlane);

        if (distance > 2.5f)
        {
            tooFar = true;
        }

        if (tooFar)
        {
            tooFar = distance > .05f;
            focusPointOnPlane = viewPointOnPlane;
            
        }

        if (FocusPointPlaneTransform)
        {
            FocusPointPlaneTransform.position = Vector3.SmoothDamp(FocusPointPlaneTransform.position, focusPointOnPlane, ref velocity, .2f);

            focusDirectionOnPlane = (FocusPointPlaneTransform.position - player.position).normalized;
        }
    }


    private void UpdateViewPoint()
    {
        var playerDir = player.transform.forward;
        viewPoint = player.position + playerDir * ViewDistance;
        var v = viewPoint;
        v.y = player.position.y;
        var viewDir = (v - player.position).normalized;
        viewPointOnPlane=player.position + viewDir * ViewDistance;
        viewDirectionOnPlane = (viewPointOnPlane - player.position).normalized;


    }

    private void SetFocusPoint()
    {
        UpdateViewPoint();
        focusPointOnPlane = viewPointOnPlane;
        FocusPointPlaneTransform.position = focusPointOnPlane;
        focusDirectionOnPlane=(FocusPointOnPlane-player.position).normalized;
    }

}
