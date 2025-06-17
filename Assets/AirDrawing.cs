using UnityEngine;
using System.Collections.Generic;
using Oculus;

[RequireComponent(typeof(OVRGrabbable))]
public class AirDrawing : MonoBehaviour
{
    public Transform tip;
    public float minDistance = 0.01f;
    public OVRInput.Button drawButton = OVRInput.Button.Two;
    public Material lineMaterial;
    public float lineWidth = 0.005f;

    private OVRGrabbable grabbable;
    private LineRenderer currentLine;
    private MeshCollider currentCollider;
    private List<Vector3> points = new List<Vector3>();
    private bool isDrawing = false;
    private Vector3 lastPoint;

    public GameObject linePrefab;

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
        GameObject lineObj = Instantiate(linePrefab);
        currentLine = lineObj.GetComponent<LineRenderer>();
        MeshCollider meshCol = lineObj.GetComponent<MeshCollider>();

        
        currentLine.widthCurve = AnimationCurve.Constant(0, 1, lineWidth);
        currentLine.numCapVertices = 5;

        // Materiale unico e colore
        Material uniqueMat = new Material(lineMaterial);
        currentLine.material = uniqueMat;

        // Pulisci e inizializza
        points.Clear();
        Vector3 localPoint = currentLine.transform.InverseTransformPoint(tip.position);
        points.Add(localPoint);
        currentLine.positionCount = 1;
        currentLine.SetPosition(0, localPoint);

        // Bake iniziale del collider
        Mesh mesh = new Mesh();
        currentLine.BakeMesh(mesh, false); // local space
        meshCol.sharedMesh = mesh;

        lastPoint = tip.position;
        isDrawing = true;
    }

    void StopDrawing()
    {
        isDrawing = false;
        currentLine = null;
        currentCollider = null;
    }

    void AddPoint(Vector3 worldPoint)
    {
        Vector3 localPoint = currentLine.transform.InverseTransformPoint(worldPoint);
        points.Add(localPoint);
        currentLine.positionCount = points.Count;
        currentLine.SetPositions(points.ToArray());

        if (points.Count >= 2)
        {
            Mesh mesh = new Mesh();
            currentLine.BakeMesh(mesh, false);
            currentLine.GetComponent<MeshCollider>().sharedMesh = null;
            currentLine.GetComponent<MeshCollider>().sharedMesh = mesh;
        }
    }


    public List<Vector3> GetPoints()
    {
        return new List<Vector3>(points);
    }

    public bool HasLineRenderer(LineRenderer target)
    {
        return currentLine != null && currentLine == target;
    }
}
