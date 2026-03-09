using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class HouseNumberPlacer : MonoBehaviour
{
    [Header("Configuration")]
    public GameObject houseNumberPrefab;
    [Tooltip("Высота установки таблички над уровнем земли")]
    public float placementHeight = 1.5f;
    [Tooltip("Отступ от края стены")]
    public float wallMargin = 0.25f;
    [Tooltip("Минимальная длина стены для размещения таблички")]
    public float minWallLengthForPlacement = 2.0f;

    private BaseDataObject buildingData;
    private Mesh buildingMesh;

    public float font_size = 0.14f;

    [Tooltip("Высота таблички")]
    public float tabletHeight = 0.5f;
    [Tooltip("Толщина таблички")]
    public float tabletThickness = 0.05f;
    [Tooltip("Коэффициент ширины таблички относительно длины текста")]
    public float widthPerCharacter = 1.60f;

    // ============================================
    // ОПТИМИЗАЦИЯ: Предварительно выделенные списки
    // ============================================
    private List<WallInfo> suitableWallsCache = new List<WallInfo>(8);
    private List<TriangleData> allTrianglesCache = new List<TriangleData>(256);
    private List<TriangleData> verticalTrianglesCache = new List<TriangleData>(128);
    private Dictionary<Vector3, List<TriangleData>> groupedByNormalCache = new Dictionary<Vector3, List<TriangleData>>(16);
    private List<int> clusterIndicesCache = new List<int>(64);
    private HashSet<int> uniqueVertexIndicesCache = new HashSet<int>();
    private List<Vector3> wallVerticesCache = new List<Vector3>(32);

    // ============================================
    // ОПТИМИЗАЦИЯ: Кэш для вершин меша
    // ============================================
    private Vector3[] cachedVertices;
    private int[] cachedTriangles;

    void Start()
    {
        buildingData = GetComponent<BaseDataObject>();
        if (buildingData == null)
        {
            Debug.LogWarning($"HouseNumberPlacer on {gameObject.name}: No BaseDataObject component found.");
            Destroy(this);
            return;
        }

        var meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogWarning($"HouseNumberPlacer on {gameObject.name}: No MeshFilter with mesh found.");
            Destroy(this);
            return;
        }
        buildingMesh = meshFilter.sharedMesh;

        AttemptPlaceHouseNumbers();
    }

    private void AttemptPlaceHouseNumbers()
    {
        string houseNumber = buildingData.GetValueStringByKey("addr:housenumber");
        string streetName = buildingData.GetValueStringByKey("addr:street");

        if (string.IsNullOrEmpty(houseNumber))
        {
            return;
        }

        string displayText = houseNumber;
        if (!string.IsNullOrEmpty(streetName))
        {
            displayText = $"{houseNumber}, {streetName}";
        }

        var suitableWalls = FindSuitableWalls();

        int wallCount = suitableWalls.Count;
        for (int i = 0; i < wallCount; i++)
        {
            PlacePlatesOnWall(suitableWalls[i], displayText);
        }
    }

    private List<WallInfo> FindSuitableWalls()
    {
        // ============================================
        // ОПТИМИЗАЦИЯ: Очищаем кэши
        // ============================================
        suitableWallsCache.Clear();
        allTrianglesCache.Clear();
        verticalTrianglesCache.Clear();
        groupedByNormalCache.Clear();
        uniqueVertexIndicesCache.Clear();
        wallVerticesCache.Clear();

        // ============================================
        // ОПТИМИЗАЦИЯ: Кэшируем массивы меша
        // ============================================
        if (cachedVertices == null || cachedVertices.Length != buildingMesh.vertexCount)
        {
            cachedVertices = buildingMesh.vertices;
            cachedTriangles = buildingMesh.triangles;
        }

        int triangleCount = cachedTriangles.Length;

        // Собираем все треугольники
        for (int i = 0; i < triangleCount; i += 3)
        {
            Vector3 v1 = cachedVertices[cachedTriangles[i]];
            Vector3 v2 = cachedVertices[cachedTriangles[i + 1]];
            Vector3 v3 = cachedVertices[cachedTriangles[i + 2]];

            Vector3 faceNormal = CalculateNormal(v1, v2, v3);
            if (faceNormal.sqrMagnitude == 0) continue;
            faceNormal.Normalize();

            allTrianglesCache.Add(new TriangleData
            {
                indices = new int[] { cachedTriangles[i], cachedTriangles[i + 1], cachedTriangles[i + 2] },
                normal = faceNormal,
                center = (v1 + v2 + v3) / 3f
            });
        }

        const float verticalThreshold = 0.7f;
        int allTrianglesCount = allTrianglesCache.Count;

        // Фильтруем вертикальные треугольники
        for (int i = 0; i < allTrianglesCount; i++)
        {
            if (Mathf.Abs(allTrianglesCache[i].normal.y) < verticalThreshold)
            {
                verticalTrianglesCache.Add(allTrianglesCache[i]);
            }
        }

        // Группировка по нормали
        int verticalCount = verticalTrianglesCache.Count;
        for (int i = 0; i < verticalCount; i++)
        {
            var tri = verticalTrianglesCache[i];
            Vector3 roundedNormal = new Vector3(
                Mathf.Round(tri.normal.x * 1000f) / 1000f,
                Mathf.Round(tri.normal.y * 1000f) / 1000f,
                Mathf.Round(tri.normal.z * 1000f) / 1000f
            );

            if (!groupedByNormalCache.ContainsKey(roundedNormal))
                groupedByNormalCache[roundedNormal] = new List<TriangleData>();

            groupedByNormalCache[roundedNormal].Add(tri);
        }

        foreach (var kvp in groupedByNormalCache)
        {
            Vector3 normal = kvp.Key;
            List<TriangleData> groupTriangles = kvp.Value;
            int groupCount = groupTriangles.Count;

            // Создаём граф смежности
            Dictionary<int, List<int>> adjacencyList = new Dictionary<int, List<int>>(groupCount);

            for (int i = 0; i < groupCount; i++)
            {
                adjacencyList[i] = new List<int>(4);
            }

            for (int i = 0; i < groupCount; i++)
            {
                for (int j = i + 1; j < groupCount; j++)
                {
                    if (AreTrianglesAdjacentFast(groupTriangles[i], groupTriangles[j]))
                    {
                        adjacencyList[i].Add(j);
                        adjacencyList[j].Add(i);
                    }
                }
            }

            // Поиск связных компонентов
            bool[] visited = new bool[groupCount];
            for (int i = 0; i < groupCount; i++)
            {
                if (visited[i]) continue;

                clusterIndicesCache.Clear();
                Queue<int> queue = new Queue<int>();
                queue.Enqueue(i);
                visited[i] = true;

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    clusterIndicesCache.Add(current);

                    var neighbors = adjacencyList[current];
                    int neighborCount = neighbors.Count;
                    for (int n = 0; n < neighborCount; n++)
                    {
                        int neighbor = neighbors[n];
                        if (!visited[neighbor])
                        {
                            visited[neighbor] = true;
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                // Пропускаем одиночные треугольники
                if (clusterIndicesCache.Count < 2) continue;

                // Собираем вершины кластера
                uniqueVertexIndicesCache.Clear();
                int clusterCount = clusterIndicesCache.Count;
                for (int c = 0; c < clusterCount; c++)
                {
                    var tri = groupTriangles[clusterIndicesCache[c]];
                    uniqueVertexIndicesCache.Add(tri.indices[0]);
                    uniqueVertexIndicesCache.Add(tri.indices[1]);
                    uniqueVertexIndicesCache.Add(tri.indices[2]);
                }

                wallVerticesCache.Clear();
                foreach (int idx in uniqueVertexIndicesCache)
                {
                    wallVerticesCache.Add(cachedVertices[idx]);
                }

                if (wallVerticesCache.Count < 4) continue;

                var (length, width, localRight, localForward, localUp, center) = ComputeWallDimensionsAndAxes(wallVerticesCache, normal);

                if (length >= minWallLengthForPlacement)
                {
                    Vector3 worldCenter = new Vector3(center.x, placementHeight, center.z);

                    suitableWallsCache.Add(new WallInfo
                    {
                        center = worldCenter,
                        normal = normal,
                        forward = localForward,
                        right = localRight,
                        up = localUp,
                        length = length,
                        bounds = new Bounds(center, new Vector3(length, 0, width))
                    });
                }
            }
        }

        return suitableWallsCache;
    }

    private (float length, float width, Vector3 right, Vector3 forward, Vector3 up, Vector3 center) ComputeWallDimensionsAndAxes(List<Vector3> wallVertices, Vector3 normal)
    {
        Vector3 up = Vector3.up;
        Vector3 forward = -normal;
        Vector3 right = Vector3.Cross(up, forward).normalized;

        if (right.sqrMagnitude == 0)
        {
            right = Vector3.right;
            forward = Vector3.Cross(right, up).normalized;
        }

        Vector3 planeNormal = normal.normalized;
        Vector3 pointOnPlane = wallVertices[0];

        int vertexCount = wallVertices.Count;

        // ============================================
        // ОПТИМИЗАЦИЯ: Вычисляем bounds напрямую без List<Vector2>
        // ============================================
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        for (int i = 0; i < vertexCount; i++)
        {
            Vector3 v = wallVertices[i];
            Vector3 vLocal = v - pointOnPlane;
            Vector3 proj = vLocal - Vector3.Dot(vLocal, planeNormal) * planeNormal;

            float x = Vector3.Dot(proj, right);
            float y = Vector3.Dot(proj, up);

            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        float length = maxX - minX;
        float height = maxY - minY;
        float center2DX = (minX + maxX) / 2;
        float center2DY = (minY + maxY) / 2;

        Vector3 center3D = pointOnPlane + center2DX * right + center2DY * up;

        return (length, height, right, forward, up, center3D);
    }

    // ============================================
    // ОПТИМИЗАЦИЯ: Быстрая проверка смежности
    // ============================================
    private bool AreTrianglesAdjacentFast(TriangleData t1, TriangleData t2)
    {
        int sharedCount = 0;
        for (int i = 0; i < 3; i++)
        {
            int idx1 = t1.indices[i];
            for (int j = 0; j < 3; j++)
            {
                if (idx1 == t2.indices[j])
                {
                    sharedCount++;
                    if (sharedCount >= 2) return true;
                }
            }
        }
        return false;
    }

    private void PlacePlatesOnWall(WallInfo wallInfo, string text)
    {
        // ============================================
        // ОПТИМИЗАЦИЯ: Вычисляем ширину без создания TMP
        // ============================================
        float tabletWidth = text.Length * widthPerCharacter * font_size;
        float halfWallLength = wallInfo.length / 2.0f;

        if (halfWallLength < tabletWidth + wallMargin) return;

        float offsetMagnitude = halfWallLength - (tabletWidth / 2.0f) - wallMargin;

        Vector3 position1 = wallInfo.center + wallInfo.right * offsetMagnitude;
        Vector3 position2 = wallInfo.center - wallInfo.right * offsetMagnitude;

        PlacePlateAtPosition(position1, wallInfo.forward, wallInfo.up, text, "Left", tabletWidth);
        PlacePlateAtPosition(position2, wallInfo.forward, wallInfo.up, text, "Right", tabletWidth);
    }

    private void PlacePlateAtPosition(Vector3 position, Vector3 forward, Vector3 up, string text, string suffix, float tabletWidth)
    {
        GameObject plateObject = new GameObject("Plate");
        plateObject.transform.position = position;
        plateObject.transform.rotation = Quaternion.LookRotation(forward, up);
        plateObject.transform.SetParent(transform, false);

        MeshFilter meshFilter = plateObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = plateObject.AddComponent<MeshRenderer>();

        Mesh tabletMesh = CreateTabletMesh(tabletWidth, tabletHeight, tabletThickness);
        meshFilter.mesh = tabletMesh;

        meshRenderer.material = new Material(Shader.Find("HDRP/Lit"));
        meshRenderer.material.SetColor("_BaseColor", Color.blue);

        plateObject.name = $"HouseNumberPlate_{buildingData.Name ?? buildingData.Id}_{suffix}";

        GameObject canvasObject = new GameObject("Canvas");
        canvasObject.transform.SetParent(plateObject.transform, false);
        canvasObject.transform.localPosition = Vector3.zero + Vector3.back * (tabletThickness / 2 + 0.01f);
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one;

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        GameObject tmpObject = new GameObject("Text");
        tmpObject.transform.SetParent(canvasObject.transform, false);
        tmpObject.transform.localPosition = Vector3.zero;
        tmpObject.transform.localRotation = Quaternion.identity;
        tmpObject.transform.localScale = Vector3.one;

        TextMeshProUGUI textMesh = tmpObject.AddComponent<TextMeshProUGUI>();
        textMesh.text = text;
        textMesh.color = Color.white;
        textMesh.fontSize = font_size;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.verticalAlignment = VerticalAlignmentOptions.Middle;
        textMesh.enableWordWrapping = false;

        RectTransform textRect = tmpObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private Mesh CreateTabletMesh(float width, float height, float thickness)
    {
        Mesh mesh = new Mesh();

        float halfWidth = width / 2;
        float halfHeight = height / 2;
        float halfThickness = thickness / 2;

        Vector3[] vertices = new Vector3[8]
        {
            new Vector3(-halfWidth, -halfHeight, -halfThickness),
            new Vector3(halfWidth, -halfHeight, -halfThickness),
            new Vector3(halfWidth, halfHeight, -halfThickness),
            new Vector3(-halfWidth, halfHeight, -halfThickness),
            new Vector3(-halfWidth, -halfHeight, halfThickness),
            new Vector3(halfWidth, -halfHeight, halfThickness),
            new Vector3(halfWidth, halfHeight, halfThickness),
            new Vector3(-halfWidth, halfHeight, halfThickness)
        };

        int[] triangles = new int[]
        {
            0, 2, 1, 0, 3, 2,
            5, 6, 4, 6, 7, 4,
            3, 6, 2, 3, 7, 6,
            4, 1, 5, 4, 0, 1,
            4, 7, 0, 7, 3, 0,
            1, 6, 5, 1, 2, 6
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private Vector3 CalculateNormal(Vector3 v1, Vector3 v2, Vector3 v3)
    {
        Vector3 edge1 = v2 - v1;
        Vector3 edge2 = v3 - v1;
        return Vector3.Cross(edge1, edge2);
    }

    private struct WallInfo
    {
        public Vector3 center;
        public Vector3 normal;
        public Vector3 forward;
        public Vector3 right;
        public Vector3 up;
        public float length;
        public Bounds bounds;
    }

    private struct TriangleData
    {
        public int[] indices;
        public Vector3 normal;
        public Vector3 center;
    }
}
