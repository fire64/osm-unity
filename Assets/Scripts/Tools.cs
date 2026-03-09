using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using UnityEngine;
using Color = UnityEngine.Color;


[System.Serializable]
public struct Item
{
    public string key;
    public string value;
}

[System.Serializable]
public class TessellationSettings
{
    public float maxEdgeLength = 1.5f;
    public float heightSensitivity = 0.3f;
    public int maxVertexCount = 8000;
    public float edgeBlendDistance = 2.0f;
    public float heightfix = 0.0f;
}

/// <summary>
/// ОПТИМИЗИРОВАННЫЙ класс GR (Geometry Renderer)
/// 
/// Ключевые улучшения:
/// 1. Кэширование Terrain объектов
/// 2. Асинхронные методы для тяжелых вычислений
/// 3. Пул объектов для снижения аллокаций
/// 4. Оптимизированные алгоритмы
/// </summary>
public static class GR
{
    public enum AlgorithmHeightSorting
    {
        MinimumHeight,
        AverageHeight,
        MaximumHeight,
        CenterHeight,
    };

    // ========== КЭШ TERRAIN ==========

    private static Terrain[] _cachedTerrains;
    private static float _terrainCacheTime;
    private static readonly float _terrainCacheLifetime = 1.0f; // секунды

    // ========== ПУЛЫ ОБЪЕКТОВ ==========

    private static readonly ConcurrentBag<List<Vector3>> _vector3ListPool = new ConcurrentBag<List<Vector3>>();
    private static readonly ConcurrentBag<List<int>> _intListPool = new ConcurrentBag<List<int>>();
    private static readonly ConcurrentBag<List<Vector2>> _vector2ListPool = new ConcurrentBag<List<Vector2>>();

    // ========== МЕТОДЫ ПУЛА ==========

    private static List<Vector3> RentVector3List()
    {
        if (_vector3ListPool.TryTake(out var list))
        {
            list.Clear();
            return list;
        }
        return new List<Vector3>();
    }

    private static void ReturnVector3List(List<Vector3> list)
    {
        if (list.Capacity < 10000) // Не возвращаем слишком большие списки
        {
            _vector3ListPool.Add(list);
        }
    }

    private static List<int> RentIntList()
    {
        if (_intListPool.TryTake(out var list))
        {
            list.Clear();
            return list;
        }
        return new List<int>();
    }

    private static void ReturnIntList(List<int> list)
    {
        if (list.Capacity < 100000)
        {
            _intListPool.Add(list);
        }
    }

    // ========== КЭШИРОВАНИЕ TERRAIN ==========

    private static Terrain[] GetCachedTerrains()
    {
        // Обновляем кэш только раз в секунду
        if (_cachedTerrains == null || (Time.time - _terrainCacheTime) > _terrainCacheLifetime)
        {
            _cachedTerrains = Terrain.activeTerrains; // Более эффективно чем FindObjectsByType
            _terrainCacheTime = Time.time;
        }
        return _cachedTerrains;
    }

    public static void InvalidateTerrainCache()
    {
        _cachedTerrains = null;
    }

    // ========== HEIGHT CALCULATION (ОПТИМИЗИРОВАННЫЙ) ==========

    /// <summary>
    /// ОПТИМИЗИРОВАННЫЙ метод получения высоты с кэшированием Terrain
    /// </summary>
    static public float GetHeightFromAllTerrains(Vector3 position)
    {
        Terrain[] terrains = GetCachedTerrains();

        if (terrains == null || terrains.Length == 0)
        {
            return Terrain.activeTerrain != null
                ? Terrain.activeTerrain.SampleHeight(position)
                : 0f;
        }

        float maxHeight = float.NegativeInfinity;
        bool found = false;

        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            Vector3 terrainPos = terrain.transform.position;
            TerrainData terrainData = terrain.terrainData;

            if (terrainData == null) continue;

            Vector3 terrainSize = terrainData.size;

            // Проверка границ
            if (position.x >= terrainPos.x && position.x <= terrainPos.x + terrainSize.x &&
                position.z >= terrainPos.z && position.z <= terrainPos.z + terrainSize.z)
            {
                found = true;
                float h = terrain.SampleHeight(position);
                if (h > maxHeight)
                    maxHeight = h;
            }
        }

        return found ? maxHeight : 0f;
    }

    // ========== ASYNC МЕТОДЫ ==========

    /// <summary>
    /// Асинхронная тесселяция меша для выполнения в фоновом потоке
    /// Возвращает MeshGenerationData вместо прямого изменения Mesh
    /// </summary>
    public static Task<MeshGenerationData> ApplyAdaptiveTessellationAsync(
        Mesh baseMesh,
        Transform transform,
        TessellationSettings settings,
        System.Threading.CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ApplyAdaptiveTessellationInternal(baseMesh, transform, settings, cancellationToken), cancellationToken);
    }

    private static MeshGenerationData ApplyAdaptiveTessellationInternal(
        Mesh baseMesh,
        Transform transform,
        TessellationSettings settings,
        System.Threading.CancellationToken cancellationToken)
    {
        var result = new MeshGenerationData();

        if (cancellationToken.IsCancellationRequested) return result;

        Vector3[] vertices = baseMesh.vertices;
        Vector2[] uvs = baseMesh.uv;
        int[] triangles = baseMesh.triangles;

        // Получаем мировые координаты
        Matrix4x4 localToWorld = transform.localToWorldMatrix;
        Vector3[] worldVertices = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            worldVertices[i] = localToWorld.MultiplyPoint3x4(vertices[i]);
        }

        var edgeDictionary = new Dictionary<(int, int), EdgeData>();
        var newVertices = new List<Vector3>(vertices);
        var newUVs = new List<Vector2>(uvs);
        var newTriangles = new List<int>();

        // Предвычисляем высоты terrain
        Vector3[] terrainHeights = new Vector3[worldVertices.Length];
        for (int i = 0; i < worldVertices.Length; i++)
        {
            if (cancellationToken.IsCancellationRequested) return result;
            terrainHeights[i] = new Vector3(worldVertices[i].x, GetHeightFromAllTerrains(worldVertices[i]), worldVertices[i].z);
        }

        // Обработка треугольников
        for (int i = 0; i < triangles.Length; i += 3)
        {
            if (cancellationToken.IsCancellationRequested) return result;

            int a = triangles[i];
            int b = triangles[i + 1];
            int c = triangles[i + 2];

            int ab = GetOrCreateMidpoint(a, b, worldVertices, terrainHeights, edgeDictionary, newVertices, newUVs, vertices, uvs, settings);
            int bc = GetOrCreateMidpoint(b, c, worldVertices, terrainHeights, edgeDictionary, newVertices, newUVs, vertices, uvs, settings);
            int ca = GetOrCreateMidpoint(c, a, worldVertices, terrainHeights, edgeDictionary, newVertices, newUVs, vertices, uvs, settings);

            AddSubTriangle(a, ab, ca, newTriangles);
            AddSubTriangle(ab, b, bc, newTriangles);
            AddSubTriangle(ca, bc, c, newTriangles);
            AddSubTriangle(ab, bc, ca, newTriangles);
        }

        // Защита от перегрузки
        if (newVertices.Count > settings.maxVertexCount)
        {
            Debug.LogWarning($"Tessellation exceeded max vertex count ({newVertices.Count} > {settings.maxVertexCount})");
            return result;
        }

        result.Vertices = newVertices.ToArray();
        result.UV = newUVs.ToArray();
        result.Triangles = newTriangles.ToArray();

        return result;
    }

    /// <summary>
    /// Асинхронное создание меша с высотой
    /// </summary>
    public static Task<MeshGenerationData> CreateMeshWithHeightAsync(
        List<Vector3> corners,
        float minHeight,
        float height,
        List<List<Vector3>> holes = null,
        bool flatUV = false,
        bool reverseUV = false,
        bool isFloorDown = false,
        System.Threading.CancellationToken cancellationToken = default)
    {
        return Task.Run(() => CreateMeshWithHeightInternal(corners, minHeight, height, holes, flatUV, reverseUV, isFloorDown, cancellationToken), cancellationToken);
    }

    private static MeshGenerationData CreateMeshWithHeightInternal(
        List<Vector3> corners,
        float minHeight,
        float height,
        List<List<Vector3>> holes,
        bool flatUV,
        bool reverseUV,
        bool isFloorDown,
        System.Threading.CancellationToken cancellationToken)
    {
        var result = new MeshGenerationData();

        if (corners == null || corners.Count < 3) return result;
        if (cancellationToken.IsCancellationRequested) return result;

        // Создаем копию для работы
        var workingCorners = new List<Vector3>(corners);

        if (IsClockwise(workingCorners))
        {
            workingCorners.Reverse();
        }

        // Триангуляция
        var polygon = new Polygon();
        var exteriorContour = new Contour(workingCorners.Select(v => new Vertex(v.x, v.z)).ToList());
        polygon.Add(exteriorContour);

        if (holes != null && holes.Count > 0)
        {
            foreach (var hole in holes.Where(h => h != null && h.Count > 0))
            {
                if (cancellationToken.IsCancellationRequested) return result;
                var holeContour = new Contour(hole.Select(v => new Vertex(v.x, v.z)).ToList());
                polygon.Add(holeContour, true);
            }
        }

        IMesh mesh;
        try
        {
            mesh = polygon.Triangulate(
                new ConstraintOptions { ConformingDelaunay = false },
                new QualityOptions { MinimumAngle = 0 });
        }
        catch (Exception ex)
        {
            Debug.LogError($"Triangulation failed: {ex.Message}");
            return result;
        }

        if (cancellationToken.IsCancellationRequested) return result;

        // Подготовка данных
        var vertices2D = mesh.Vertices.ToList();
        var triangles2D = mesh.Triangles;

        var allVertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var allTriangles = new List<int>();

        // Верхняя поверхность
        var upperVertices = vertices2D.Select(v => new Vector3((float)v.X, height, (float)v.Y)).ToList();
        allVertices.AddRange(upperVertices);
        GenerateSurfaceUV(upperVertices, flatUV, reverseUV, uv);

        // Нижняя поверхность
        var lowerVertices = vertices2D.Select(v => new Vector3((float)v.X, minHeight, (float)v.Y)).ToList();
        allVertices.AddRange(lowerVertices);
        GenerateSurfaceUV(lowerVertices, flatUV, reverseUV, uv);

        // Боковые поверхности
        var sideVertices = new List<Vector3>();
        var sideUV = new List<Vector2>();
        GenerateSideVerticesAndUV(workingCorners, holes, vertices2D, minHeight, height, sideVertices, sideUV, reverseUV);
        allVertices.AddRange(sideVertices);
        uv.AddRange(sideUV);

        // Треугольники верхней поверхности
        var upperTriangles = triangles2D
            .SelectMany(t => new[] { t.GetVertexID(2), t.GetVertexID(1), t.GetVertexID(0) }).ToList();
        allTriangles.AddRange(upperTriangles);

        // Нижняя поверхность
        var lowerTriangles = triangles2D
            .SelectMany(t => isFloorDown ? new[]
            {
                t.GetVertexID(0) + upperVertices.Count,
                t.GetVertexID(2) + upperVertices.Count,
                t.GetVertexID(1) + upperVertices.Count
            } : new[]
            {
                t.GetVertexID(2) + upperVertices.Count,
                t.GetVertexID(0) + upperVertices.Count,
                t.GetVertexID(1) + upperVertices.Count
            }).ToList();
        allTriangles.AddRange(lowerTriangles);

        // Боковые поверхности
        GenerateSideFaces(GetAllContours(workingCorners, holes), upperVertices.Count + lowerVertices.Count, allTriangles, 4);

        // Нормали
        var normals = CalculateNormals(allVertices, allTriangles);

        if (!isFloorDown)
        {
            int lowerStart = upperVertices.Count;
            int lowerEnd = lowerStart + lowerVertices.Count;
            for (int i = lowerStart; i < lowerEnd; i++)
            {
                normals[i] = -normals[i];
            }
        }

        result.Vertices = allVertices.ToArray();
        result.Triangles = allTriangles.ToArray();
        result.Normals = normals.ToArray();
        result.UV = uv.ToArray();

        return result;
    }

    // ========== СИНХРОННЫЕ МЕТОДЫ (для совместимости) ==========

    /// <summary>
    /// Генерирует точки для проверки поверхности
    /// </summary>
    private static List<Vector3> GenerateCheckPoints(GameObject gameObject)
    {
        MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
        List<Vector3> points = RentVector3List();

        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Vector3[] vertices = meshFilter.sharedMesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                points.Add(gameObject.transform.TransformPoint(vertices[i]));
            }
        }

        return points;
    }

    static public IEnumerator AdjustMeshToTerrainCorutine(GameObject targetObject, TessellationSettings settings)
    {
        yield return new WaitForSeconds(1.0f);
        AdjustMeshToTerrain(targetObject, settings);
    }

    static public IEnumerator SpawnInHeight(GameObject targetObject, AlgorithmHeightSorting typesorting)
    {
        yield return new WaitForSeconds(1.0f);
        targetObject.transform.position += Vector3.up * getTerrianHeightPosition(targetObject, typesorting);
    }

    public static void AdjustMeshToTerrain(GameObject gameObject, TessellationSettings settings = null)
    {
        if (settings == null)
            settings = new TessellationSettings { maxEdgeLength = 2.0f, heightSensitivity = 0.5f };

        MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogWarning($"MeshFilter or mesh missing on {gameObject.name}. Skipping adjustment.");
            return;
        }

        Mesh baseMesh = CreateMeshCopy(meshFilter.sharedMesh);
        baseMesh.name = $"Base_{meshFilter.sharedMesh.name}";

        Mesh tessellatedMesh = ApplyAdaptiveTessellation(baseMesh, gameObject.transform, settings);
        Mesh finalMesh = CreateMeshCopy(tessellatedMesh);
        AdjustVerticesHeight(finalMesh, gameObject.transform);

        meshFilter.mesh = finalMesh;

        if (gameObject.TryGetComponent<MeshCollider>(out MeshCollider collider))
        {
            collider.sharedMesh = null;
            collider.sharedMesh = finalMesh;
        }
    }

    private static void AdjustVerticesHeight(Mesh mesh, Transform transform)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] worldVertices = new Vector3[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            worldVertices[i] = transform.TransformPoint(vertices[i]);
        }

        for (int i = 0; i < worldVertices.Length; i++)
        {
            Vector3 worldPos = worldVertices[i];
            worldPos.y = GetHeightFromAllTerrains(worldPos);
            worldVertices[i] = worldPos;
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = transform.InverseTransformPoint(worldVertices[i]);
        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private static Mesh ApplyAdaptiveTessellation(Mesh baseMesh, Transform transform, TessellationSettings settings)
    {
        Vector3[] vertices = baseMesh.vertices;
        Vector2[] uvs = baseMesh.uv;
        int[] triangles = baseMesh.triangles;

        Vector3[] worldVertices = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            worldVertices[i] = transform.TransformPoint(vertices[i]);
        }

        var edgeDictionary = new Dictionary<(int, int), EdgeData>();
        var newVertices = new List<Vector3>(vertices);
        var newUVs = new List<Vector2>(uvs);
        var newTriangles = new List<int>();

        Vector3[] terrainHeights = new Vector3[worldVertices.Length];
        for (int i = 0; i < worldVertices.Length; i++)
        {
            terrainHeights[i] = new Vector3(worldVertices[i].x, GetHeightFromAllTerrains(worldVertices[i]), worldVertices[i].z);
        }

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = triangles[i];
            int b = triangles[i + 1];
            int c = triangles[i + 2];

            int ab = GetOrCreateMidpoint(a, b, worldVertices, terrainHeights, edgeDictionary, newVertices, newUVs, vertices, uvs, settings);
            int bc = GetOrCreateMidpoint(b, c, worldVertices, terrainHeights, edgeDictionary, newVertices, newUVs, vertices, uvs, settings);
            int ca = GetOrCreateMidpoint(c, a, worldVertices, terrainHeights, edgeDictionary, newVertices, newUVs, vertices, uvs, settings);

            AddSubTriangle(a, ab, ca, newTriangles);
            AddSubTriangle(ab, b, bc, newTriangles);
            AddSubTriangle(ca, bc, c, newTriangles);
            AddSubTriangle(ab, bc, ca, newTriangles);
        }

        Mesh tessellatedMesh = new Mesh();
        tessellatedMesh.vertices = newVertices.ToArray();
        tessellatedMesh.uv = newUVs.ToArray();
        tessellatedMesh.triangles = newTriangles.ToArray();

        if (baseMesh.colors != null && baseMesh.colors.Length > 0)
            tessellatedMesh.colors = baseMesh.colors;

        tessellatedMesh.RecalculateNormals();
        tessellatedMesh.RecalculateBounds();

        if (tessellatedMesh.vertexCount > settings.maxVertexCount)
        {
            Debug.LogWarning($"Tessellation exceeded max vertex count ({tessellatedMesh.vertexCount} > {settings.maxVertexCount}). Consider reducing detail.");
            return baseMesh;
        }

        return tessellatedMesh;
    }

    private static int GetOrCreateMidpoint(
        int i1, int i2,
        Vector3[] worldVertices,
        Vector3[] terrainHeights,
        Dictionary<(int, int), EdgeData> edgeDictionary,
        List<Vector3> newVertices,
        List<Vector2> newUVs,
        Vector3[] originalVertices,
        Vector2[] originalUVs,
        TessellationSettings settings)
    {
        var key = (Mathf.Min(i1, i2), Mathf.Max(i1, i2));

        if (edgeDictionary.TryGetValue(key, out EdgeData edgeData))
        {
            return edgeData.vertexIndex;
        }

        float worldDistance = Vector3.Distance(worldVertices[i1], worldVertices[i2]);
        float heightDelta = Mathf.Abs(terrainHeights[i1].y - terrainHeights[i2].y);

        bool needsTessellation = worldDistance > settings.maxEdgeLength ||
                                heightDelta > settings.heightSensitivity;

        if (!needsTessellation)
        {
            edgeData = new EdgeData { vertexIndex = -1, needsSplit = false };
            edgeDictionary[key] = edgeData;
            return -1;
        }

        Vector3 localMidpoint = (originalVertices[i1] + originalVertices[i2]) * 0.5f;
        Vector2 uvMidpoint = (originalUVs[i1] + originalUVs[i2]) * 0.5f;

        int newIndex = newVertices.Count;
        newVertices.Add(localMidpoint);
        newUVs.Add(uvMidpoint);

        edgeData = new EdgeData { vertexIndex = newIndex, needsSplit = true };
        edgeDictionary[key] = edgeData;

        return newIndex;
    }

    private static void AddSubTriangle(int v1, int v2, int v3, List<int> triangleList)
    {
        if (v1 == -1 || v2 == -1 || v3 == -1) return;
        triangleList.Add(v1);
        triangleList.Add(v2);
        triangleList.Add(v3);
    }

    private class EdgeData
    {
        public int vertexIndex;
        public bool needsSplit;
    }

    private static Mesh CreateMeshCopy(Mesh original)
    {
        Mesh copy = new Mesh();
        copy.vertices = original.vertices;
        copy.triangles = original.triangles;

        if (original.uv != null && original.uv.Length > 0)
            copy.uv = original.uv;

        if (original.uv2 != null && original.uv2.Length > 0)
            copy.uv2 = original.uv2;

        if (original.colors != null && original.colors.Length > 0)
            copy.colors = original.colors;

        if (original.normals != null && original.normals.Length > 0)
            copy.normals = original.normals;

        if (original.tangents != null && original.tangents.Length > 0)
            copy.tangents = original.tangents;

        copy.subMeshCount = original.subMeshCount;
        for (int i = 0; i < original.subMeshCount; i++)
        {
            copy.SetTriangles(original.GetTriangles(i), i);
        }

        copy.name = "Copy_" + original.name;
        return copy;
    }

    static public float getTerrianHeightPosition(GameObject gameObject, AlgorithmHeightSorting typesorting)
    {
        Vector3 curCenter = gameObject.transform.position;
        float startHeight = GetHeightFromAllTerrains(gameObject.transform.position);
        List<Vector3> points = GenerateCheckPoints(gameObject);

        try
        {
            if (typesorting == AlgorithmHeightSorting.CenterHeight || points.Count == 0)
            {
                return startHeight;
            }
            else
            {
                float minHeight = startHeight;
                float maxHeight = startHeight;
                int countheights = 0;
                float summheights = 0;

                if (startHeight != 0.0f)
                {
                    summheights += startHeight;
                    countheights++;
                }

                for (int i = 0; i < points.Count; i++)
                {
                    Vector3 point = new Vector3(points[i].x, 0.0f, points[i].z);
                    float tHeight = GetHeightFromAllTerrains(point);

                    if (tHeight != 0.0f)
                    {
                        summheights += tHeight;
                        countheights++;

                        if (minHeight > tHeight) minHeight = tHeight;
                        if (maxHeight < tHeight) maxHeight = tHeight;
                    }
                }

                if (typesorting == AlgorithmHeightSorting.MinimumHeight) return minHeight;
                if (typesorting == AlgorithmHeightSorting.MaximumHeight) return maxHeight;

                return countheights > 0 ? summheights / countheights : startHeight;
            }
        }
        finally
        {
            ReturnVector3List(points);
        }
    }

    public static Vector3 getHeightPosition(Vector3 point)
    {
        point.y = 10000;
        if (Physics.Raycast(point, Vector3.down, out RaycastHit downHit, 10000f))
        {
            return downHit.point;
        }
        return new Vector3(point.x, 0, point.z);
    }

    public static bool IsClockwise(List<Vector3> points)
    {
        float sum = 0;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 current = points[i];
            Vector3 next = points[(i + 1) % points.Count];
            sum += (next.x - current.x) * (next.z + current.z);
        }
        return sum > 0;
    }

    private static bool IsClosed(List<Vector3> corners)
    {
        return Vector3.Distance(corners[0], corners[^1]) < 0.01f;
    }

    // ... остальные методы (AddQuad, AddEndCap, CreateMeshLineWithWidthAndHeight, и т.д.)
    // остаются без изменений для экономии места

    private static void AddQuad(MeshData data, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
    {
        int baseIndex = data.Vertices.Count;
        data.Vertices.AddRange(new[] { a, b, c, d });
        for (int i = 0; i < 4; i++) data.Normals.Add(normal);
        data.UV.AddRange(new[]
        {
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(0, 1), new Vector2(1, 1)
        });

        data.Indices.Add(baseIndex + 3);
        data.Indices.Add(baseIndex + 1);
        data.Indices.Add(baseIndex + 2);
        data.Indices.Add(baseIndex + 2);
        data.Indices.Add(baseIndex + 1);
        data.Indices.Add(baseIndex);
    }

    private static void AddEndCap(MeshData data, Vector3 bottomA, Vector3 bottomB,
        Vector3 topA, Vector3 topB, Vector3 direction, bool isReverse = true)
    {
        int baseIndex = data.Vertices.Count;
        data.Vertices.AddRange(new[] { bottomA, topA, bottomB, topB });

        Vector3 normal = new Vector3(-direction.z, 0, direction.x).normalized;
        for (int i = 0; i < 4; i++) data.Normals.Add(normal);

        data.UV.AddRange(new[]
        {
            new Vector2(0, 0), new Vector2(0, 1),
            new Vector2(1, 0), new Vector2(1, 1)
        });

        if (isReverse)
        {
            data.Indices.Add(baseIndex + 3);
            data.Indices.Add(baseIndex + 1);
            data.Indices.Add(baseIndex + 2);
            data.Indices.Add(baseIndex + 2);
            data.Indices.Add(baseIndex + 1);
            data.Indices.Add(baseIndex);
        }
        else
        {
            data.Indices.Add(baseIndex);
            data.Indices.Add(baseIndex + 1);
            data.Indices.Add(baseIndex + 2);
            data.Indices.Add(baseIndex + 2);
            data.Indices.Add(baseIndex + 1);
            data.Indices.Add(baseIndex + 3);
        }
    }

    public static void CreateMeshLineWithWidthAndHeight(List<Vector3> corners, float height, float min_height, float width, MeshData data)
    {
        if (corners == null || corners.Count < 2) return;

        bool isClosed = IsClosed(corners);
        List<Vector3> offsets = new List<Vector3>();

        for (int i = 0; i < corners.Count; i++)
        {
            Vector3 prevDir, nextDir;

            if (isClosed)
            {
                prevDir = (corners[i] - corners[(i - 1 + corners.Count) % corners.Count]).normalized;
                nextDir = (corners[(i + 1) % corners.Count] - corners[i]).normalized;
            }
            else
            {
                if (i == 0)
                {
                    prevDir = Vector3.zero;
                    nextDir = (corners[i + 1] - corners[i]).normalized;
                }
                else if (i == corners.Count - 1)
                {
                    prevDir = (corners[i] - corners[i - 1]).normalized;
                    nextDir = Vector3.zero;
                }
                else
                {
                    prevDir = (corners[i] - corners[i - 1]).normalized;
                    nextDir = (corners[i + 1] - corners[i]).normalized;
                }
            }

            Vector3 bisector = prevDir + nextDir;
            if (bisector == Vector3.zero) bisector = prevDir;
            bisector.Normalize();

            Vector3 normal = new Vector3(-bisector.z, 0, bisector.x).normalized;

            if (!isClosed)
            {
                if (i == 0) normal = new Vector3(-nextDir.z, 0, nextDir.x).normalized;
                else if (i == corners.Count - 1) normal = new Vector3(-prevDir.z, 0, prevDir.x).normalized;
            }

            offsets.Add(normal * width * 0.5f);
        }

        for (int i = 0; i < corners.Count; i++)
        {
            int nextI = (i + 1) % corners.Count;
            if (!isClosed && i == corners.Count - 1) break;

            Vector3 start = corners[i];
            Vector3 end = corners[nextI];

            Vector3 leftOffsetStart = -offsets[i];
            Vector3 rightOffsetStart = offsets[i];
            Vector3 leftOffsetEnd = -offsets[nextI];
            Vector3 rightOffsetEnd = offsets[nextI];

            Vector3 sl = start + leftOffsetStart;
            Vector3 sr = start + rightOffsetStart;
            Vector3 el = end + leftOffsetEnd;
            Vector3 er = end + rightOffsetEnd;

            sl.y = min_height;
            sr.y = min_height;
            el.y = min_height;
            er.y = min_height;

            Vector3 sul = new Vector3(sl.x, height, sl.z);
            Vector3 sur = new Vector3(sr.x, height, sr.z);
            Vector3 eul = new Vector3(el.x, height, el.z);
            Vector3 eur = new Vector3(er.x, height, er.z);

            AddSmoothQuad(data, sl, el, sul, eul, leftOffsetStart.normalized, leftOffsetEnd.normalized, true);
            AddSmoothQuad(data, sr, er, sur, eur, rightOffsetStart.normalized, rightOffsetEnd.normalized, false);
            AddQuad(data, sul, eul, sur, eur, Vector3.up);
            AddQuad(data, el, sl, er, sr, Vector3.down);

            if (!isClosed)
            {
                if (i == 0) AddEndCap(data, sl, sr, sul, sur, (end - start).normalized);
                if (i == corners.Count - 2) AddEndCap(data, el, er, eul, eur, (end - start).normalized, false);
            }
        }
    }

    private static void AddSmoothQuad(MeshData data, Vector3 aBottom, Vector3 bBottom, Vector3 aTop, Vector3 bTop, Vector3 normalA, Vector3 normalB, bool isReverse)
    {
        int baseIndex = data.Vertices.Count;
        data.Vertices.AddRange(new[] { aBottom, aTop, bBottom, bTop });

        data.Normals.Add(normalA);
        data.Normals.Add(normalA);
        data.Normals.Add(normalB);
        data.Normals.Add(normalB);

        float length = Vector3.Distance(aBottom, bBottom);

        data.UV.AddRange(new[]
        {
            new Vector2(0, 0), new Vector2(0, 1),
            new Vector2(length, 0), new Vector2(length, 1)
        });

        if (isReverse)
        {
            data.Indices.Add(baseIndex + 2);
            data.Indices.Add(baseIndex + 1);
            data.Indices.Add(baseIndex + 3);
            data.Indices.Add(baseIndex + 2);
            data.Indices.Add(baseIndex + 0);
            data.Indices.Add(baseIndex + 1);
        }
        else
        {
            data.Indices.Add(baseIndex + 1);
            data.Indices.Add(baseIndex + 0);
            data.Indices.Add(baseIndex + 2);
            data.Indices.Add(baseIndex + 3);
            data.Indices.Add(baseIndex + 1);
            data.Indices.Add(baseIndex + 2);
        }
    }

    public static void CreateMeshLineWithWidth(List<Vector3> corners, float width, MeshData data)
    {
        if (corners.Count < 2) return;

        float[] cumulativeLengths = new float[corners.Count];
        float totalLength = 0f;

        for (int i = 1; i < corners.Count; i++)
        {
            float segmentLength = Vector3.Distance(corners[i - 1], corners[i]);
            totalLength += segmentLength;
            cumulativeLengths[i] = totalLength;
        }

        List<Vector3> leftPoints = new List<Vector3>();
        List<Vector3> rightPoints = new List<Vector3>();

        for (int i = 0; i < corners.Count; i++)
        {
            Vector3 dirPrev, dirNext;
            Vector3 cross;

            if (i == 0)
            {
                dirNext = (corners[i + 1] - corners[i]).normalized;
                cross = Vector3.Cross(dirNext, Vector3.up) * width;
            }
            else if (i == corners.Count - 1)
            {
                dirPrev = (corners[i] - corners[i - 1]).normalized;
                cross = Vector3.Cross(dirPrev, Vector3.up) * width;
            }
            else
            {
                dirPrev = (corners[i] - corners[i - 1]).normalized;
                dirNext = (corners[i + 1] - corners[i]).normalized;

                Vector3 bisectorDir = dirPrev + dirNext;
                if (bisectorDir.magnitude < 0.001f)
                {
                    cross = Vector3.Cross(dirPrev, Vector3.up) * width;
                }
                else
                {
                    bisectorDir.Normalize();
                    cross = Vector3.Cross(bisectorDir, Vector3.up) * width;
                }
            }

            leftPoints.Add(corners[i] + cross);
            rightPoints.Add(corners[i] - cross);
        }

        for (int i = 0; i < corners.Count; i++)
        {
            data.Vertices.Add(leftPoints[i]);
            data.Vertices.Add(rightPoints[i]);

            float uvV = cumulativeLengths[i] / totalLength;
            data.UV.Add(new Vector2(0f, uvV));
            data.UV.Add(new Vector2(1f, uvV));

            data.Normals.Add(-Vector3.up);
            data.Normals.Add(-Vector3.up);
        }

        for (int i = 0; i < corners.Count - 1; i++)
        {
            int leftCurrent = i * 2;
            int rightCurrent = i * 2 + 1;
            int leftNext = (i + 1) * 2;
            int rightNext = (i + 1) * 2 + 1;

            data.Indices.Add(leftCurrent);
            data.Indices.Add(leftNext);
            data.Indices.Add(rightCurrent);

            data.Indices.Add(rightCurrent);
            data.Indices.Add(leftNext);
            data.Indices.Add(rightNext);
        }
    }

    public static void CreateMeshWithHeight(List<Vector3> corners, float minHeight, float height, MeshData data,
        List<List<Vector3>> holes = null, bool flatUV = false, bool reverseUV = false, bool isFloorDown = false)
    {
        if (IsClockwise(corners))
        {
            corners.Reverse();
        }

        var polygon = new Polygon();
        var exteriorContour = new Contour(corners.Select(v => new Vertex(v.x, v.z)).ToList());
        polygon.Add(exteriorContour);

        if (holes != null && holes.Count > 0)
        {
            foreach (var hole in holes.Where(h => h.Count > 0))
            {
                var holeContour = new Contour(hole.Select(v => new Vertex(v.x, v.z)).ToList());
                polygon.Add(holeContour, true);
            }
        }

        IMesh mesh;
        try
        {
            mesh = polygon.Triangulate(new ConstraintOptions { ConformingDelaunay = false },
                new QualityOptions { MinimumAngle = 0 });
        }
        catch
        {
            Debug.LogError("Triangulation failed");
            return;
        }

        var vertices2D = mesh.Vertices.ToList();
        var triangles2D = mesh.Triangles;

        var allVertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var allTriangles = new List<int>();

        var upperVertices = vertices2D.Select(v => new Vector3((float)v.X, height, (float)v.Y)).ToList();
        allVertices.AddRange(upperVertices);
        GenerateSurfaceUV(upperVertices, flatUV, reverseUV, uv);

        var lowerVertices = vertices2D.Select(v => new Vector3((float)v.X, minHeight, (float)v.Y)).ToList();
        allVertices.AddRange(lowerVertices);
        GenerateSurfaceUV(lowerVertices, flatUV, reverseUV, uv);

        var sideVertices = new List<Vector3>();
        var sideUV = new List<Vector2>();
        GenerateSideVerticesAndUV(corners, holes, vertices2D, minHeight, height, sideVertices, sideUV, reverseUV);
        allVertices.AddRange(sideVertices);
        uv.AddRange(sideUV);

        var upperTriangles = triangles2D
            .SelectMany(t => new[] { t.GetVertexID(2), t.GetVertexID(1), t.GetVertexID(0) }).ToList();
        allTriangles.AddRange(upperTriangles);

        var lowerTriangles = triangles2D
            .SelectMany(t => isFloorDown ? new[]
            {
            t.GetVertexID(0) + upperVertices.Count,
            t.GetVertexID(2) + upperVertices.Count,
            t.GetVertexID(1) + upperVertices.Count
            } : new[]
            {
            t.GetVertexID(2) + upperVertices.Count,
            t.GetVertexID(0) + upperVertices.Count,
            t.GetVertexID(1) + upperVertices.Count
            }).ToList();
        allTriangles.AddRange(lowerTriangles);

        GenerateSideFaces(allContours: GetAllContours(corners, holes),
            sideVerticesStart: upperVertices.Count + lowerVertices.Count,
            triangles: allTriangles,
            verticesPerSegment: 4);

        var normals = CalculateNormals(allVertices, allTriangles);

        if (!isFloorDown)
        {
            int lowerStart = upperVertices.Count;
            int lowerEnd = lowerStart + lowerVertices.Count;
            for (int i = lowerStart; i < lowerEnd; i++)
            {
                normals[i] = -normals[i];
            }
        }

        data.Vertices = allVertices;
        data.Indices = allTriangles;
        data.Normals = normals;
        data.UV = uv;
    }

    private static List<List<Vector3>> GetAllContours(List<Vector3> mainContour, List<List<Vector3>> holes)
    {
        var all = new List<List<Vector3>> { mainContour };
        if (holes != null) all.AddRange(holes.Where(h => h.Count > 0));
        return all;
    }

    private static void GenerateSurfaceUV(List<Vector3> vertices, bool flatUV, bool reverseUV, List<Vector2> uv)
    {
        if (flatUV)
        {
            float minX = vertices.Min(v => v.x);
            float maxX = vertices.Max(v => v.x);
            float minZ = vertices.Min(v => v.z);
            float maxZ = vertices.Max(v => v.z);
            float width = maxX - minX;
            float depth = maxZ - minZ;

            width = Mathf.Abs(width) < 0.001f ? 1f : width;
            depth = Mathf.Abs(depth) < 0.001f ? 1f : depth;

            foreach (var v in vertices)
            {
                if (reverseUV)
                {
                    uv.Add(new Vector2((v.z - minX) / width, (v.x - minZ) / depth));
                }
                else
                {
                    uv.Add(new Vector2((v.x - minX) / width, (v.z - minZ) / depth));
                }
            }
        }
        else
        {
            foreach (var v in vertices)
            {
                if (reverseUV)
                {
                    uv.Add(new Vector2(v.z, v.x));
                }
                else
                {
                    uv.Add(new Vector2(v.x, v.z));
                }
            }
        }
    }

    private static void GenerateSideVerticesAndUV(
        List<Vector3> mainContour,
        List<List<Vector3>> holes,
        List<Vertex> vertices2D,
        float minHeight,
        float maxHeight,
        List<Vector3> sideVertices,
        List<Vector2> sideUV,
        bool reverseUV)
    {
        var vertexMap = CreateVertexMap(vertices2D);
        var allContours = GetAllContours(mainContour, holes);

        foreach (var contour in allContours)
        {
            if (contour.Count < 2) continue;

            float totalLength = CalculateContourLength(contour);
            if (totalLength <= 0) continue;

            float accumulatedLength = 0f;
            var segmentLengths = CalculateSegmentLengths(contour);

            for (int i = 0; i < contour.Count; i++)
            {
                var current = contour[i];
                var next = contour[(i + 1) % contour.Count];

                CreateSegmentVertices(current, next, maxHeight, minHeight,
                    out Vector3 upperCurrent, out Vector3 upperNext,
                    out Vector3 lowerCurrent, out Vector3 lowerNext);

                sideVertices.Add(upperCurrent);
                sideVertices.Add(upperNext);
                sideVertices.Add(lowerCurrent);
                sideVertices.Add(lowerNext);

                float uCurrent = accumulatedLength / totalLength;
                float uNext = (accumulatedLength + segmentLengths[i]) / totalLength;

                if (reverseUV)
                {
                    sideUV.Add(new Vector2(uNext, 1f));
                    sideUV.Add(new Vector2(uCurrent, 1f));
                    sideUV.Add(new Vector2(uNext, 0f));
                    sideUV.Add(new Vector2(uCurrent, 0f));
                }
                else
                {
                    sideUV.Add(new Vector2(uCurrent, 1f));
                    sideUV.Add(new Vector2(uNext, 1f));
                    sideUV.Add(new Vector2(uCurrent, 0f));
                    sideUV.Add(new Vector2(uNext, 0f));
                }

                accumulatedLength += segmentLengths[i];
            }
        }
    }

    private static Dictionary<Vector2, int> CreateVertexMap(List<Vertex> vertices2D)
    {
        var map = new Dictionary<Vector2, int>();
        for (int i = 0; i < vertices2D.Count; i++)
        {
            var key = new Vector2((float)vertices2D[i].X, (float)vertices2D[i].Y);
            if (!map.ContainsKey(key)) map.Add(key, i);
        }
        return map;
    }

    private static float CalculateContourLength(List<Vector3> contour)
    {
        return contour.Select((t, i) => Vector3.Distance(t, contour[(i + 1) % contour.Count])).Sum();
    }

    private static List<float> CalculateSegmentLengths(List<Vector3> contour)
    {
        return contour.Select((t, i) => Vector3.Distance(t, contour[(i + 1) % contour.Count])).ToList();
    }

    private static void CreateSegmentVertices(
        Vector3 current, Vector3 next,
        float maxY, float minY,
        out Vector3 upperCurrent, out Vector3 upperNext,
        out Vector3 lowerCurrent, out Vector3 lowerNext)
    {
        upperCurrent = new Vector3(current.x, maxY, current.z);
        upperNext = new Vector3(next.x, maxY, next.z);
        lowerCurrent = new Vector3(current.x, minY, current.z);
        lowerNext = new Vector3(next.x, minY, next.z);
    }

    private static void GenerateSideFaces(List<List<Vector3>> allContours, int sideVerticesStart,
        List<int> triangles, int verticesPerSegment)
    {
        int vertexOffset = sideVerticesStart;
        foreach (var contour in allContours)
        {
            if (contour.Count < 2) continue;

            for (int i = 0; i < contour.Count; i++)
            {
                int baseIndex = vertexOffset + i * verticesPerSegment;

                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);

                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 3);
                triangles.Add(baseIndex + 2);
            }
            vertexOffset += contour.Count * verticesPerSegment;
        }
    }

    private static List<Vector3> CalculateNormals(List<Vector3> vertices, List<int> triangles)
    {
        Vector3[] normals = new Vector3[vertices.Count];

        for (int i = 0; i < triangles.Count; i += 3)
        {
            int i0 = triangles[i];
            int i1 = triangles[i + 1];
            int i2 = triangles[i + 2];

            Vector3 v0 = vertices[i0];
            Vector3 v1 = vertices[i1];
            Vector3 v2 = vertices[i2];

            Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;

            normals[i0] += normal;
            normals[i1] += normal;
            normals[i2] += normal;
        }

        return normals.Select(n => n.normalized).ToList();
    }

    public static Color SetOSMRoofColour(BaseOsm geo)
    {
        if (geo.HasField("roof:colour"))
        {
            return hexToColor(geo.GetValueStringByKey("roof:colour"));
        }

        if (geo.HasField("roof:color"))
        {
            return hexToColor(geo.GetValueStringByKey("roof:color"));
        }

        if (geo.HasField("roof: colour"))
        {
            return hexToColor(geo.GetValueStringByKey("roof: colour"));
        }

        return new Color(1.0f, 1.0f, 1.0f, 1.0f);
    }

    public static Color SetOSMColour(BaseOsm geo)
    {
        if (geo.HasField("building:colour"))
        {
            return hexToColor(geo.GetValueStringByKey("building:colour"));
        }

        if (geo.HasField("building:color"))
        {
            return hexToColor(geo.GetValueStringByKey("building:color"));
        }

        if (geo.HasField("building:facade:colour"))
        {
            return hexToColor(geo.GetValueStringByKey("building:facade:colour"));
        }

        if (geo.HasField("color"))
        {
            return hexToColor(geo.GetValueStringByKey("color"));
        }

        if (geo.HasField("colour"))
        {
            return hexToColor(geo.GetValueStringByKey("colour"));
        }

        return new Color(1.0f, 1.0f, 1.0f, 1.0f);
    }

    public static Color hexToColor(string hex)
    {
        hex = hex.ToLower();

        if (hex.Contains("#"))
        {
            hex = hex.Replace("0x", "");
            hex = hex.Replace("#", "");
            byte a = 255;

            byte r = 255;
            byte g = 255;
            byte b = 255;

            try
            {
                r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);

                if (hex.Length == 8)
                {
                    a = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Exeption:" + e.Message + " color: " + hex);
            }

            return new Color32(r, g, b, a);
        }
        else
        {
            GameContentSelector contentselector = GameObject.FindObjectOfType<GameContentSelector>();
            return contentselector.colorByName.GetColorByName(hex);
        }
    }
}
