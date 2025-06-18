using UnityEngine;
using System.Collections.Generic;
using Oculus;

[RequireComponent(typeof(OVRGrabbable))]
public class handAirDrawing : MonoBehaviour
{
    public Transform tip;
    public float minDistance = 0.01f;
    public OVRInput.Button drawButton = OVRInput.Button.Two;

    public OVRInput.Controller controller = OVRInput.Controller.RTouch;
    public Material lineMaterial;
    public float lineWidth = 0.005f;
    private LineRenderer currentLine;
    private List<Vector3> points = new List<Vector3>();
    private bool isDrawing = false;
    private Vector3 lastPoint;

    public GameObject linePrefab;

    private PencilTypeSelector pencilTypeSelector;

    public Transform eraserSphere;

    public float eraseRadius = 0.5f;

    void Start()
    {
        pencilTypeSelector = GetComponent<PencilTypeSelector>();
    }

    void Update()
    {
        if (OVRInput.GetDown(drawButton, controller) && pencilTypeSelector.isPencil)
        {
            StartDrawing();
        }
        else if (OVRInput.GetUp(drawButton, controller) && pencilTypeSelector.isPencil)
        {
            StopDrawing();
        }

        if (isDrawing && currentLine != null)
        {
            Vector3 tipPos = tip.position;
            if (Vector3.Distance(tipPos, lastPoint) > minDistance)
            {
                AddPoint(tipPos);
                lastPoint = tipPos;
            }
        }

        if (pencilTypeSelector.isEraser && OVRInput.Get(drawButton, controller))
        {
            EraseNearbyLines(tip.position, eraseRadius);
        }

        if (pencilTypeSelector.isEraser)
        {
            if (eraserSphere != null)
            {
                eraserSphere.position = tip.position;
                eraserSphere.gameObject.SetActive(true);
            }
        }
        else
        {
            if (eraserSphere != null)
            {
                eraserSphere.gameObject.SetActive(false);
            }
        }
    }

    void OnDrawGizmos()
    {
        if (pencilTypeSelector != null && pencilTypeSelector.isEraser)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawSphere(tip.position, eraseRadius);
        }
    }


    void EraseNearbyLines(Vector3 eraserPosition, float radius)
    {
        GameObject[] allLines = GameObject.FindGameObjectsWithTag("Line");

        foreach (GameObject lineObj in allLines)
        {
            LineRenderer line = lineObj.GetComponent<LineRenderer>();
            if (line == null || line.positionCount < 2) continue;

            Vector3[] points = new Vector3[line.positionCount];
            line.GetPositions(points);

            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 worldA = line.transform.TransformPoint(points[i]);
                Vector3 worldB = line.transform.TransformPoint(points[i + 1]);

                float distance = DistancePointToSegment(eraserPosition, worldA, worldB);
                if (distance <= radius)
                {
                    SplitLineRendererOnErase(line, eraserPosition, radius, linePrefab);
                    break;
                }
            }
        }
    }

    float DistancePointToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        Vector3 ap = point - a;
        float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / ab.sqrMagnitude);
        Vector3 closest = a + t * ab;
        return Vector3.Distance(point, closest);
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Line") && pencilTypeSelector.isEraser)
        {
            if (OVRInput.Get(drawButton, controller))
            {
                LineRenderer line = other.GetComponent<LineRenderer>();
                if (line != null)
                {
                    SplitLineRendererOnErase(line, tip.position, eraseRadius, linePrefab);
                }
            }
        }
    }


    void RemoveNearbyPoints(LineRenderer line, Vector3 worldEraserPos, float radius)
    {
        List<Vector3> keptPositions = new List<Vector3>();
        bool removedAny = false;

        for (int i = 0; i < line.positionCount; i++)
        {
            Vector3 localPoint = line.GetPosition(i);
            Vector3 worldPoint = line.transform.TransformPoint(localPoint);
            float distance = Vector3.Distance(worldPoint, worldEraserPos);

            if (distance > radius)
            {
                keptPositions.Add(localPoint); // Mantieni i punti lontani
            }
            else
            {
                removedAny = true; // Questo punto verrà rimosso
            }
        }

        if (removedAny)
        {
            Debug.Log($"Punti rimossi: {line.positionCount - keptPositions.Count}");
            line.positionCount = keptPositions.Count;
            line.SetPositions(keptPositions.ToArray());
        }
    }

    public void SplitLineRendererOnErase(LineRenderer line, Vector3 eraserPos, float radius, GameObject linePrefab)
    {
        // 1. Ottieni tutti i punti in world space (corretti anche se l'oggetto è stato spostato)
        Vector3[] localPoints = new Vector3[line.positionCount];
        line.GetPositions(localPoints);

        List<Vector3> worldPoints = new List<Vector3>();
        foreach (Vector3 local in localPoints)
        {
            worldPoints.Add(line.transform.TransformPoint(local));
        }

        // 2. Dividi i punti in segmenti in base alla distanza dalla gomma
        List<List<Vector3>> segments = new List<List<Vector3>>();
        List<Vector3> currentSegment = new List<Vector3>();

        foreach (Vector3 point in worldPoints)
        {
            float dist = Vector3.Distance(point, eraserPos);

            if (dist > radius)
            {
                currentSegment.Add(point);
            }
            else
            {
                if (currentSegment.Count >= 2)
                {
                    segments.Add(new List<Vector3>(currentSegment));
                }
                currentSegment.Clear(); // split qui
            }
        }

        if (currentSegment.Count >= 2)
        {
            segments.Add(new List<Vector3>(currentSegment));
        }

        // 3. Crea nuove linee per ciascun segmento
        foreach (var segment in segments)
        {
            if (segment.Count < 2) continue;

            GameObject newLine = Instantiate(linePrefab);
            LineRenderer newLR = newLine.GetComponent<LineRenderer>();
            MeshCollider newCol = newLine.GetComponent<MeshCollider>();

            // Copia lo stile
            newLR.material = new Material(line.material);
            newLR.widthCurve = line.widthCurve;
            newLR.numCapVertices = line.numCapVertices;
            newLR.alignment = line.alignment;
            newLR.textureMode = line.textureMode;

            // Converte i punti del segmento da world space → local space nel nuovo oggetto
            Vector3[] localSegment = new Vector3[segment.Count];
            for (int i = 0; i < segment.Count; i++)
            {
                localSegment[i] = newLR.transform.InverseTransformPoint(segment[i]);
            }

            newLR.positionCount = localSegment.Length;
            newLR.SetPositions(localSegment);

            // Aggiorna il MeshCollider se valido
            if (newCol != null && localSegment.Length >= 2)
            {
                Mesh m = new Mesh();
                newLR.BakeMesh(m, false);
                if (m.vertexCount >= 3)
                {
                    newCol.sharedMesh = m;
                }
                else
                {
                    Destroy(newLine);
                    continue;
                }
            }

            newLine.tag = "Line";
        }

        // 4. Nascondi la linea originale in sicurezza
        line.positionCount = 0;
    }


    private List<Vector3> rawPoints = new List<Vector3>();

    void StartDrawing()
    {
        GameObject lineObj = Instantiate(linePrefab);
        currentLine = lineObj.GetComponent<LineRenderer>();
        MeshCollider meshCol = lineObj.GetComponent<MeshCollider>();
        lineObj.tag = "Line";

        // Configura LineRenderer
        currentLine.widthCurve = AnimationCurve.Constant(0, 1, lineWidth);
        currentLine.numCapVertices = 20;
        currentLine.numCornerVertices = 20;
        currentLine.useWorldSpace = false;

        // Materiale unico
        Material uniqueMat = new Material(lineMaterial);
        currentLine.material = uniqueMat;

        // Inizializza
        rawPoints.Clear();
        points.Clear();
        Vector3 localPoint = currentLine.transform.InverseTransformPoint(tip.position);
        rawPoints.Add(localPoint);
        points.Add(localPoint);
        currentLine.positionCount = 1;
        currentLine.SetPosition(0, localPoint);

        lastPoint = tip.position;
        isDrawing = true;
    }


    void StopDrawing()
    {
        isDrawing = false;
        currentLine = null;
    }

    void AddPoint(Vector3 worldPoint)
    {
        if (currentLine == null)
        {
            Debug.LogWarning("Tentativo di disegnare su una linea distrutta.");
            isDrawing = false; // Interrompi il disegno per sicurezza
            return;
        }

        Vector3 localPoint = currentLine.transform.InverseTransformPoint(worldPoint);

        if (rawPoints.Count > 0 && Vector3.Distance(localPoint, rawPoints[rawPoints.Count - 1]) < minDistance)
            return;

        rawPoints.Add(localPoint);
        points = SmoothLine(rawPoints, 1); // Interpola ogni segmento

        currentLine.positionCount = points.Count;
        currentLine.SetPositions(points.ToArray());
    }


    List<Vector3> SmoothLine(List<Vector3> rawPoints, int stepsPerSegment = 1)
    {
        List<Vector3> smooth = new List<Vector3>();

        for (int i = 0; i < rawPoints.Count - 1; i++)
        {
            Vector3 start = rawPoints[i];
            Vector3 end = rawPoints[i + 1];

            for (int j = 0; j < stepsPerSegment; j++)
            {
                float t = j / (float)stepsPerSegment;
                smooth.Add(Vector3.Lerp(start, end, t));
            }
        }

        smooth.Add(rawPoints[rawPoints.Count - 1]);
        return smooth;
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
