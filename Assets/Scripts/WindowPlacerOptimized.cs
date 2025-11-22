using System.Collections.Generic;
using UnityEngine;

// Добавим структуру для хранения данных окон
public struct WindowData
{
    public Vector3 position;
    public Vector3 normal;
    public Vector3 right;
    public Vector3 up;
    public bool isLit;
}

public class WindowPlacerOptimized : MonoBehaviour
{
    public float windowWidth = 1f;
    public float windowHeight = 1f;
    public float windowSpacing = 2.0f;
    public float margin = 0.5f;
    public float windowDepthOffset = 0.01f; // Новый параметр

    private List<Vector3> combinedVertices = new List<Vector3>();
    private List<Vector2> combinedUvs = new List<Vector2>();
    private List<Vector3> combinedNormals = new List<Vector3>();

    public Material matLit;
    public Material matDark;

    void Start()
    {
        // Проверяем наличие необходимых компонентов и добавляем их при необходимости
        MeshFilter meshFilter = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();

        // Устанавливаем материалы по умолчанию, если их нет
        if (meshRenderer.sharedMaterials == null || meshRenderer.sharedMaterials.Length == 0)
        {
            if (matLit == null) matLit = new Material(Shader.Find("Standard"));
            if (matDark == null) matDark = new Material(Shader.Find("Standard"));

            matLit.SetColor("_Color", Color.yellow); // Пример
            matDark.SetColor("_Color", Color.black);

            meshRenderer.sharedMaterials = new Material[] { matLit, matDark };
        }

        CreateWindows();
    }

    public void CreateWindows()
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        Dictionary<Vector3, List<List<Vector3>>> groupedWalls = new Dictionary<Vector3, List<List<Vector3>>>();

        // Группируем вершины по нормалям, но разбиваем на отдельные "поверхности"
        for (int i = 0; i < mesh.triangles.Length; i += 3)
        {
            Vector3 normal = GetFaceNormal(mesh, i);
            Vector3 key = normal.normalized.Round(5);

            if (!groupedWalls.ContainsKey(key))
                groupedWalls[key] = new List<List<Vector3>>();

            Vector3 v1 = mesh.vertices[mesh.triangles[i]];
            Vector3 v2 = mesh.vertices[mesh.triangles[i + 1]];
            Vector3 v3 = mesh.vertices[mesh.triangles[i + 2]];

            // Проверяем, к какой группе вершин принадлежит этот треугольник
            List<Vector3> targetGroup = null;
            foreach (var group in groupedWalls[key])
            {
                if (IsConnectedToGroup(new List<Vector3> { v1, v2, v3 }, group))
                {
                    targetGroup = group;
                    break;
                }
            }

            if (targetGroup != null)
            {
                targetGroup.AddRange(new List<Vector3> { v1, v2, v3 });
            }
            else
            {
                groupedWalls[key].Add(new List<Vector3> { v1, v2, v3 });
            }
        }

        // Собираем окна
        List<WindowData> allWindows = new List<WindowData>();

        // Обрабатываем каждую стену
        foreach (var normalGroup in groupedWalls)
        {
            foreach (var vertices in normalGroup.Value)
            {
                // Вычисляем границы стены
                Bounds bounds = new Bounds(vertices[0], Vector3.zero);
                foreach (Vector3 v in vertices)
                    bounds.Encapsulate(v);

                // Вычисляем ориентацию стены
                Vector3 normal = normalGroup.Key;
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

                        // Случайно выбираем, светится окно или нет
                        bool isLit = Random.value > 0.7f; // 30% окон светятся

                        allWindows.Add(new WindowData
                        {
                            position = position,
                            normal = normal,
                            right = right,
                            up = up,
                            isLit = isLit
                        });
                    }
                }
            }
        }

        // Теперь разделим окна на 2 группы: светящиеся и темные
        List<WindowData> litWindows = allWindows.FindAll(w => w.isLit);
        List<WindowData> darkWindows = allWindows.FindAll(w => !w.isLit);

        // Очищаем списки для нового расчета
        combinedVertices.Clear();
        combinedUvs.Clear();
        combinedNormals.Clear();

        List<int> litTriangles = new List<int>();
        List<int> darkTriangles = new List<int>();

        // Добавляем светящиеся окна
        foreach (var window in litWindows)
        {
            AddWindowToMesh(window.position, window.normal, window.right, window.up, litTriangles);
        }

        // Добавляем темные окна
        foreach (var window in darkWindows)
        {
            AddWindowToMesh(window.position, window.normal, window.right, window.up, darkTriangles);
        }

        // Создаем общий меш с 2 submeshes
        Mesh combinedMesh = new Mesh();
        combinedMesh.vertices = combinedVertices.ToArray();
        combinedMesh.uv = combinedUvs.ToArray();
        combinedMesh.SetNormals(combinedNormals);

        // Устанавливаем submeshes
        combinedMesh.subMeshCount = 2;
        combinedMesh.SetTriangles(litTriangles, 0);
        combinedMesh.SetTriangles(darkTriangles, 1);

        combinedMesh.RecalculateTangents();
        combinedMesh.Optimize();

        // Назначаем меш компоненту MeshFilter
        GetComponent<MeshFilter>().mesh = combinedMesh;
    }

    bool IsConnectedToGroup(List<Vector3> triangle, List<Vector3> group)
    {
        float threshold = 0.1f; // Порог близости
        foreach (var v in triangle)
        {
            foreach (var gv in group)
            {
                if (Vector3.Distance(v, gv) < threshold)
                    return true;
            }
        }
        return false;
    }

    void AddWindowToMesh(Vector3 position, Vector3 normal, Vector3 right, Vector3 up, List<int> triangles)
    {
        // Сдвигаем позицию окна вдоль нормали наружу
        Vector3 offsetPosition = position + normal * windowDepthOffset;

        // Вычисляем вершины окна
        Vector3 halfRight = right * windowWidth / 2;
        Vector3 halfUp = up * windowHeight / 2;

        Vector3 v0 = offsetPosition - halfRight - halfUp;
        Vector3 v1 = offsetPosition + halfRight - halfUp;
        Vector3 v2 = offsetPosition + halfRight + halfUp;
        Vector3 v3 = offsetPosition - halfRight + halfUp;

        // Добавляем вершины
        int startIndex = combinedVertices.Count;
        combinedVertices.Add(v0);
        combinedVertices.Add(v1);
        combinedVertices.Add(v2);
        combinedVertices.Add(v3);

        // Добавляем треугольники
        triangles.Add(startIndex);
        triangles.Add(startIndex + 1);
        triangles.Add(startIndex + 2);

        triangles.Add(startIndex);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex + 3);

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