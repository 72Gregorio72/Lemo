using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class AirLineData : MonoBehaviour
{
    public List<Vector3> points = new List<Vector3>();
    private LineRenderer line;

    public void Initialize(LineRenderer lr)
    {
        line = lr;
        points.Clear();
    }

    public void AddPoint(Vector3 point)
    {
        points.Add(point);
        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());
    }

    public void EraseNear(Vector3 eraserPosition, float radius)
    {
        bool modified = false;

        for (int i = points.Count - 1; i >= 0; i--)
        {
            if (Vector3.Distance(points[i], eraserPosition) < radius)
            {
                points.RemoveAt(i);
                modified = true;
            }
        }

        if (modified)
        {
            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
        }

        if (points.Count <= 1)
            Destroy(gameObject);
    }
}
