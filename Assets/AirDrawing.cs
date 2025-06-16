using System.Collections.Generic;
using UnityEngine;
using Oculus;

[RequireComponent(typeof(OVRGrabbable))]
public class AirDrawing : MonoBehaviour
{
    public Transform tip; // la punta della matita
    public float minDistance = 0.01f; // distanza minima per aggiungere un punto
    public OVRInput.Button drawButton = OVRInput.Button.Two; // 'B' su Oculus
    public Material lineMaterial;
    public float lineWidth = 0.005f;

    private OVRGrabbable grabbable;
    private LineRenderer currentLine;
    private List<Vector3> points = new List<Vector3>();
    private bool isDrawing = false;
    private Vector3 lastPoint;

    void Start()
    {
        grabbable = GetComponent<OVRGrabbable>();
    }

    void Update()
    {
        if (!grabbable.isGrabbed) return;

        if (OVRInput.GetDown(drawButton))
        {
            StartDrawing();
        }
        else if (OVRInput.GetUp(drawButton))
        {
            StopDrawing();
        }

        if (isDrawing)
        {
            Vector3 tipPos = tip.position;
            if (Vector3.Distance(tipPos, lastPoint) > minDistance)
            {
                AddPoint(tipPos);
                lastPoint = tipPos;
            }
        }
    }

    void StartDrawing()
    {
        GameObject lineObj = new GameObject("AirLine");
        currentLine = lineObj.AddComponent<LineRenderer>();
        currentLine.positionCount = 0;

        // 🔧 Crea un materiale nuovo per questa linea (così non viene condiviso)
        Material uniqueMat = new Material(lineMaterial);
        currentLine.material = uniqueMat;

        currentLine.widthCurve = AnimationCurve.Constant(0, 1, lineWidth);
        currentLine.numCapVertices = 5;
        currentLine.useWorldSpace = true;

        points.Clear();
        AddPoint(tip.position);
        isDrawing = true;
        lastPoint = tip.position;
    }


    void StopDrawing()
    {
        isDrawing = false;
    }

    void AddPoint(Vector3 point)
    {
        points.Add(point);
        currentLine.positionCount = points.Count;
        currentLine.SetPositions(points.ToArray());
    }
}
