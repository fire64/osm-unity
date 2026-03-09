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
    public float windowDepthOffset = 0.01f;

    public Material matLit;
    public Material matDark;

    // ============================================
    // ОПТИМИЗАЦИЯ: Предварительно выделенные списки
    // ============================================
    private List<Vector3> combinedVertices = new List<Vector3>(256);
    private List<Vector2> combinedUvs = new List<Vector2>(256);
    private List<Vector3> combinedNormals = new List<Vector3>(256);
    private List<WindowData> allWindows = new List<WindowData>(128);
    private List<WindowData> litWindows = new List<WindowData>(64);
    private List<WindowData> darkWindows = new List<WindowData>(64);
    private List<int> litTriangles = new List<int>(384);
    private List<int> darkTriangles = new List<int>(384);

    // Кэш для сгруппированных стен
    private Dictionary<Vector3, List<List<Vector3>>> groupedWallsCache = new Dictionary<Vector3, List<List<Vector3>>>(16);

    void Start()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();

        if (meshRenderer.sharedMaterials == null || meshRenderer.sharedMaterials.Length == 0)
        {
            if (matLit == null) matLit = new Material(Shader.Find("HDRP/Lit"));
            if (matDark == null) matDark = new Material(Shader.Find("HDRP/Lit"));

            matLit.SetColor("_Color", Color.yellow);
            matLit.SetColor("_BaseColor", Color.yellow);

            matDark.SetColor("_Color", Color.black);
            matDark.SetColor("_BaseColor", Color.black);

            meshRenderer.sharedMaterials = new Material[] { matLit, matDark };
        }

        CreateWindows();
    }

    public void CreateWindows()
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;

        // ============================================
        // ОПТИМИЗАЦИЯ: Очищаем кэш вместо создания нового
        // ============================================
        groupedWallsCache.Clear();
        allWindows.Clear();
        litWindows.Clear();
        darkWindows.Clear();
        combinedVertices.Clear();
        combinedUvs.Clear();
        combinedNormals.Clear();
        litTriangles.Clear();
        darkTriangles.Clear();

        // ============================================
        // ОПТИМИЗАЦИЯ: Кэшируем массивы меша
        // ============================================
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;
        int triangleCount = triangles.Length;

        // Группируем вершины по нормалям
        for (int i = 0; i < triangleCount; i += 3)
        {
            Vector3 normal = GetFaceNormalFast(vertices, triangles, i);
            Vector3 key = normal.Round(5);

            if (!groupedWallsCache.ContainsKey(key))
                groupedWallsCache[key] = new List<List<Vector3>>();

            Vector3 v1 = vertices[triangles[i]];
            Vector3 v2 = vertices[triangles[i + 1]];
            Vector3 v3 = vertices[triangles[i + 2]];

            List<Vector3> targetGroup = null;
            var groups = groupedWallsCache[key];
            int groupCount = groups.Count;

            for (int g = 0; g < groupCount; g++)
            {
                if (IsConnectedToGroupFast(v1, v2, v3, groups[g]))
                {
                    targetGroup = groups[g];
                    break;
                }
            }

            if (targetGroup != null)
            {
                targetGroup.Add(v1);
                targetGroup.Add(v2);
                targetGroup.Add(v3);
            }
            else
            {
                var newGroup = new List<Vector3>(3) { v1, v2, v3 };
                groups.Add(newGroup);
            }
        }

        // Обрабатываем каждую стену
        foreach (var normalGroup in groupedWallsCache)
        {
            foreach (var vertexList in normalGroup.Value)
            {
                int vertexCount = vertexList.Count;
                if (vertexCount == 0) continue;

                // Вычисляем границы стены
                Bounds bounds = new Bounds(vertexList[0], Vector3.zero);
                for (int v = 1; v < vertexCount; v++)
                {
                    bounds.Encapsulate(vertexList[v]);
                }

                Vector3 normal = normalGroup.Key;
                Vector3 right = Vector3.Cross(normal, Vector3.up).normalized;
                Vector3 up = Vector3.Cross(normal, right).normalized;

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

                        bool isLit = Random.value > 0.7f;

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

        // Разделяем окна на группы
        int windowCount = allWindows.Count;
        for (int i = 0; i < windowCount; i++)
        {
            if (allWindows[i].isLit)
                litWindows.Add(allWindows[i]);
            else
                darkWindows.Add(allWindows[i]);
        }

        // Добавляем светящиеся окна
        int litCount = litWindows.Count;
        for (int i = 0; i < litCount; i++)
        {
            AddWindowToMesh(litWindows[i], litTriangles);
        }

        // Добавляем темные окна
        int darkCount = darkWindows.Count;
        for (int i = 0; i < darkCount; i++)
        {
            AddWindowToMesh(darkWindows[i], darkTriangles);
        }

        // Создаем общий меш
        Mesh combinedMesh = new Mesh();
        combinedMesh.vertices = combinedVertices.ToArray();
        combinedMesh.uv = combinedUvs.ToArray();
        combinedMesh.SetNormals(combinedNormals);

        combinedMesh.subMeshCount = 2;
        combinedMesh.SetTriangles(litTriangles, 0);
        combinedMesh.SetTriangles(darkTriangles, 1);

        combinedMesh.RecalculateTangents();
        combinedMesh.Optimize();

        GetComponent<MeshFilter>().mesh = combinedMesh;
    }

    // ============================================
    // ОПТИМИЗАЦИЯ: Быстрая проверка связи
    // ============================================
    private bool IsConnectedToGroupFast(Vector3 v1, Vector3 v2, Vector3 v3, List<Vector3> group)
    {
        const float threshold = 0.1f;
        const float thresholdSqr = threshold * threshold;

        int groupCount = group.Count;
        for (int g = 0; g < groupCount; g++)
        {
            if ((v1 - group[g]).sqrMagnitude < thresholdSqr) return true;
            if ((v2 - group[g]).sqrMagnitude < thresholdSqr) return true;
            if ((v3 - group[g]).sqrMagnitude < thresholdSqr) return true;
        }
        return false;
    }

    // ============================================
    // ОПТИМИЗАЦИЯ: Быстрое получение нормали
    // ============================================
    private Vector3 GetFaceNormalFast(Vector3[] vertices, int[] triangles, int startIndex)
    {
        Vector3 a = vertices[triangles[startIndex]];
        Vector3 b = vertices[triangles[startIndex + 1]];
        Vector3 c = vertices[triangles[startIndex + 2]];
        return Vector3.Cross(b - a, c - a).normalized;
    }

    private void AddWindowToMesh(WindowData window, List<int> triangles)
    {
        Vector3 offsetPosition = window.position + window.normal * windowDepthOffset;

        Vector3 halfRight = window.right * windowWidth / 2;
        Vector3 halfUp = window.up * windowHeight / 2;

        Vector3 v0 = offsetPosition - halfRight - halfUp;
        Vector3 v1 = offsetPosition + halfRight - halfUp;
        Vector3 v2 = offsetPosition + halfRight + halfUp;
        Vector3 v3 = offsetPosition - halfRight + halfUp;

        int startIndex = combinedVertices.Count;
        combinedVertices.Add(v0);
        combinedVertices.Add(v1);
        combinedVertices.Add(v2);
        combinedVertices.Add(v3);

        triangles.Add(startIndex);
        triangles.Add(startIndex + 1);
        triangles.Add(startIndex + 2);

        triangles.Add(startIndex);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex + 3);

        combinedUvs.Add(new Vector2(0, 0));
        combinedUvs.Add(new Vector2(1, 0));
        combinedUvs.Add(new Vector2(1, 1));
        combinedUvs.Add(new Vector2(0, 1));

        Vector3 invertedNormal = -window.normal;
        combinedNormals.Add(invertedNormal);
        combinedNormals.Add(invertedNormal);
        combinedNormals.Add(invertedNormal);
        combinedNormals.Add(invertedNormal);
    }

    void OnValidate()
    {
        windowSpacing = Mathf.Max(0.1f, windowSpacing);
        margin = Mathf.Max(0, margin);
    }

    Vector2Int CalculateGrid(Vector3 size, Vector3 right, Vector3 up)
    {
        float width = Mathf.Abs(Vector3.Dot(size, right.normalized));
        float height = Mathf.Abs(Vector3.Dot(size, up.normalized));

        float availableWidth = width - 2 * margin;
        float availableHeight = height - 2 * margin;

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
