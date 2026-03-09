using NUnit.Framework.Internal;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SurfaceHeightCalculator : MonoBehaviour
{
    public enum AlhorytmHeightSorting
    {
        MinimumHeight,
        AverageHeight,
        MaximumHeight
    };

    public MeshFilter meshFilter;

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
    }

    /// <summary>
    /// Генерирует точки для проверки поверхности
    /// </summary>
    private List<Vector3> GenerateCheckPoints()
    {
        meshFilter = GetComponent<MeshFilter>();

        List<Vector3> points = new List<Vector3>();

        // Вариант 1: Точки по сетке (для прямоугольных объектов)
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            // Используем вершины меша для более точного определения формы
            Vector3[] vertices = meshFilter.sharedMesh.vertices;
            Vector3[] worldVertices = new Vector3[vertices.Length];

            for (int i = 0; i < vertices.Length; i++)
            {
                worldVertices[i] = transform.TransformPoint(vertices[i]);
            }

            // Берем только нижние вершины (нижние 20% по высоте)
            float minY = worldVertices.Min(v => v.y);
            float maxY = worldVertices.Max(v => v.y);
            float threshold = minY + (maxY - minY) * 0.2f;

            foreach (Vector3 vertex in worldVertices)
            {
                if (vertex.y <= threshold)
                {
                    points.Add(vertex);
                }
            }
        }


        return points;
    }

    public float FindMinimumSurfaceHeight()
    {
        List<Vector3> points = GenerateCheckPoints();

        float minHeight = Terrain.activeTerrain.SampleHeight(transform.position);

        int countheight = 0;

        float averageHeight = Terrain.activeTerrain.SampleHeight(transform.position);

        if(averageHeight != 0.0f)
        {
            countheight++;
        }

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 point = new Vector3(points[i].x, 0.0f, points[i].z);

            float tHeight = Terrain.activeTerrain.SampleHeight(point);

            if(tHeight != 0.0f)
            {
                averageHeight += tHeight;
                countheight++;

                if (minHeight > tHeight)
                {
                    minHeight = tHeight;
                }
            }
        }

        float averageHeightRes = averageHeight / countheight;

        return averageHeightRes;
    }
}