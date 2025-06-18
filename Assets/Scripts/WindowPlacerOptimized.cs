using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WindowLevel
{
    public float yPosition;
    public float windowWidth = 1f;
    public float windowHeight = 1f;
    public float spacing = 0.5f;
    public float margin = 0.2f;
}

public class WindowPlacerOptimized : MonoBehaviour
{
    public float windowWidth = 1f;
    public float windowHeight = 1f;
    public float windowSpacing = 2.0f;
    public float margin = 0.5f;

    private List<Vector3> combinedVertices = new List<Vector3>();
    private List<int> combinedTriangles = new List<int>();
    private List<Vector2> combinedUvs = new List<Vector2>();
    private List<Vector3> combinedNormals = new List<Vector3>();

    void Start()
    {
        // Проверяем наличие необходимых компонентов и добавляем их при необходимости
        MeshFilter meshFilter = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();

        // Устанавливаем материал по умолчанию, если его нет
        if (meshRenderer.material == null)
        {
            meshRenderer.material = new Material(Shader.Find("Standard"));
            meshRenderer.material.SetColor("_Color", Color.red);
        }

        CreateWindows();
    }

    public void CreateWindows()
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        Dictionary<Vector3, List<Vector3>> walls = new Dictionary<Vector3, List<Vector3>>();

        // Группируем вершины по нормалям (стены)
        for (int i = 0; i < mesh.triangles.Length; i += 3)
        {
            Vector3 normal = GetFaceNormal(mesh, i);
            Vector3 key = normal.normalized.Round(5);

            if (!walls.ContainsKey(key))
                walls[key] = new List<Vector3>();

            walls[key].Add(mesh.vertices[mesh.triangles[i]]);
            walls[key].Add(mesh.vertices[mesh.triangles[i + 1]]);
            walls[key].Add(mesh.vertices[mesh.triangles[i + 2]]);
        }

        // Очищаем списки для нового расчета
        combinedVertices.Clear();
        combinedTriangles.Clear();
        combinedUvs.Clear();
        combinedNormals.Clear();

        // Для каждой стены
        foreach (var wall in walls)
        {
            Vector3 normal = wall.Key;
            List<Vector3> vertices = wall.Value;

            // Вычисляем границы стены
            Bounds bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (Vector3 v in vertices)
                bounds.Encapsulate(v);

            // Вычисляем ориентацию стены
            Vector3 right = Vector3.Cross(normal, Vector3.up).normalized;
            Vector3 up = Vector3.Cross(normal, right).normalized;

            // Создаем сетку окон
            Vector2Int grid = CalculateGrid(bounds.size, right, up);

            for (int x = 0; x < grid.x; x++)
            {
                for (int y = 0; y < grid.y; y++)
                {
                    Vector3 position = CalculateWindowPosition(
                        bounds.center, right, up,
                        bounds.size,
                        x, y, grid
                    );

                    if (position == Vector3.zero) continue;

                    AddWindowToMesh(position, normal, right, up);
                }
            }
        }

        // Создаем общий меш
        Mesh combinedMesh = new Mesh();
        combinedMesh.vertices = combinedVertices.ToArray();
        combinedMesh.triangles = combinedTriangles.ToArray();
        combinedMesh.uv = combinedUvs.ToArray();
        combinedMesh.SetNormals(combinedNormals);
        combinedMesh.RecalculateTangents();
        combinedMesh.Optimize();

        // Назначаем меш компоненту MeshFilter
        GetComponent<MeshFilter>().mesh = combinedMesh;
    }

    void AddWindowToMesh(Vector3 position, Vector3 normal, Vector3 right, Vector3 up)
    {
        // Вычисляем вершины окна
        Vector3 halfRight = right * windowWidth / 2;
        Vector3 halfUp = up * windowHeight / 2;

        Vector3 v0 = position - halfRight - halfUp;
        Vector3 v1 = position + halfRight - halfUp;
        Vector3 v2 = position + halfRight + halfUp;
        Vector3 v3 = position - halfRight + halfUp;

        // Добавляем вершины
        int startIndex = combinedVertices.Count;
        combinedVertices.Add(v0);
        combinedVertices.Add(v1);
        combinedVertices.Add(v2);
        combinedVertices.Add(v3);

        // Добавляем треугольники
        combinedTriangles.Add(startIndex);
        combinedTriangles.Add(startIndex + 1);
        combinedTriangles.Add(startIndex + 2);

        combinedTriangles.Add(startIndex);
        combinedTriangles.Add(startIndex + 2);
        combinedTriangles.Add(startIndex + 3);

        // Добавляем UVs
        combinedUvs.Add(new Vector2(0, 0));
        combinedUvs.Add(new Vector2(1, 0));
        combinedUvs.Add(new Vector2(1, 1));
        combinedUvs.Add(new Vector2(0, 1));

        // Добавляем нормали
        for (int i = 0; i < 4; i++)
        {
            combinedNormals.Add(-normal); // Нормаль направлена наружу
        }
    }

    void OnValidate()
    {
        windowSpacing = Mathf.Max(0.1f, windowSpacing);
        margin = Mathf.Max(0, margin);
    }

    Vector3 GetFaceNormal(Mesh mesh, int startIndex)
    {
        Vector3 a = mesh.vertices[mesh.triangles[startIndex]];
        Vector3 b = mesh.vertices[mesh.triangles[startIndex + 1]];
        Vector3 c = mesh.vertices[mesh.triangles[startIndex + 2]];
        return Vector3.Cross(b - a, c - a).normalized;
    }

    Vector2Int CalculateGrid(Vector3 size, Vector3 right, Vector3 up)
    {
        // Рассчитываем проекции размера стены на локальные оси
        float width = Mathf.Abs(Vector3.Dot(size, right.normalized));
        float height = Mathf.Abs(Vector3.Dot(size, up.normalized));

        // Рассчитываем доступное пространство для окон
        float availableWidth = width - 2 * margin;
        float availableHeight = height - 2 * margin;

        // Гарантируем минимальное количество окон (0 если места недостаточно)
        int xCount = Mathf.Max(0, Mathf.FloorToInt(availableWidth / windowSpacing));
        int yCount = Mathf.Max(0, Mathf.FloorToInt(availableHeight / windowSpacing));

        return new Vector2Int(xCount, yCount);
    }

    Vector3 CalculateWindowPosition(Vector3 center, Vector3 right, Vector3 up,
                                   Vector3 size, int x, int y, Vector2Int grid)
    {
        if (grid.x <= 0 || grid.y <= 0) return Vector3.zero;

        float effectiveWidth = Mathf.Abs(Vector3.Dot(size, right.normalized));
        float effectiveHeight = Mathf.Abs(Vector3.Dot(size, up.normalized));

        float horizontalSpacing = (effectiveWidth - 2 * margin) / (grid.x + 1);
        float verticalSpacing = (effectiveHeight - 2 * margin) / (grid.y + 1);

        Vector3 offset =
            right.normalized * (margin + (x + 1) * horizontalSpacing - effectiveWidth / 2) +
            up.normalized * (margin + (y + 1) * verticalSpacing - effectiveHeight / 2);

        return center + offset;
    }
}

public static class Vector3Extensions
{
    public static Vector3 Round(this Vector3 v, int decimals)
    {
        return new Vector3(
            (float)System.Math.Round(v.x, decimals),
            (float)System.Math.Round(v.y, decimals),
            (float)System.Math.Round(v.z, decimals)
        );
    }
}