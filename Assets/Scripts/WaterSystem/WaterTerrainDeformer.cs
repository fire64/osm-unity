using System.Collections.Generic;
using UnityEngine;

public class WaterTerrainDeformer : MonoBehaviour
{
    private MeshFilter _meshFilter;

    void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
    }

    public void ModifyTerrains()
    {
        if (_meshFilter == null || _meshFilter.mesh == null)
        {
            Debug.LogError("Water object does not have a valid MeshFilter component.");
            return;
        }

        Mesh waterMesh = _meshFilter.mesh;
        Terrain[] allTerrains = Terrain.activeTerrains;

        if (allTerrains.Length == 0)
        {
            Debug.LogError("No terrains found.");
            return;
        }

        // Получаем все треугольники меша воды в мировых координатах (XZ проекция)
        List<Vector2[]> waterTriangles = GetWaterMeshTriangles(waterMesh);
        float minY = GetWaterMinHeight(waterMesh);

        foreach (Terrain terrain in allTerrains)
        {
            ProcessTerrain(terrain, waterTriangles, minY);
        }
    }

    List<Vector2[]> GetWaterMeshTriangles(Mesh mesh)
    {
        List<Vector2[]> triangles = new List<Vector2[]>();
        Vector3[] vertices = mesh.vertices;
        int[] meshTriangles = mesh.triangles;

        for (int i = 0; i < meshTriangles.Length; i += 3)
        {
            Vector3 v1 = transform.TransformPoint(vertices[meshTriangles[i]]);
            Vector3 v2 = transform.TransformPoint(vertices[meshTriangles[i + 1]]);
            Vector3 v3 = transform.TransformPoint(vertices[meshTriangles[i + 2]]);

            triangles.Add(new Vector2[] {
                new Vector2(v1.x, v1.z),
                new Vector2(v2.x, v2.z),
                new Vector2(v3.x, v3.z)
            });
        }

        return triangles;
    }

    float GetWaterMinHeight(Mesh mesh)
    {
        float minY = float.MaxValue;
        foreach (Vector3 vertex in mesh.vertices)
        {
            float worldY = transform.TransformPoint(vertex).y;
            if (worldY < minY) minY = worldY;
        }
        return minY;
    }

    void ProcessTerrain(Terrain terrain, List<Vector2[]> waterTriangles, float minY)
    {
        TerrainData terrainData = terrain.terrainData;
        if (terrainData == null) return;

        int width = terrainData.heightmapResolution;
        int height = terrainData.heightmapResolution;

        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = terrainData.size;

        // Вычисляем границы террейна и воды
        Bounds terrainBounds = new Bounds(
            terrainPos + terrainSize * 0.5f,
            terrainSize
        );

        Bounds waterBounds = _meshFilter.mesh.bounds;
        waterBounds.center = transform.TransformPoint(waterBounds.center);
        waterBounds.size = transform.TransformVector(waterBounds.size);

        if (!terrainBounds.Intersects(waterBounds)) return;

        // Работаем с высотной картой
        float[,] heights = terrainData.GetHeights(0, 0, width, height);

        // Оптимизация: вычисляем общие границы
        Rect waterRect = GetWaterBoundsRect(waterTriangles);
        Rect terrainRect = new Rect(
            terrainPos.x,
            terrainPos.z,
            terrainSize.x,
            terrainSize.z
        );

        Rect intersection = RectIntersection(waterRect, terrainRect);
        if (intersection.width == 0 || intersection.height == 0) return;

        // Вычисляем диапазон обработки
        int startX = (int)((intersection.xMin - terrainPos.x) / terrainSize.x * (width - 1));
        int endX = (int)((intersection.xMax - terrainPos.x) / terrainSize.x * (width - 1)) + 1;
        int startZ = (int)((intersection.yMin - terrainPos.z) / terrainSize.z * (height - 1));
        int endZ = (int)((intersection.yMax - terrainPos.z) / terrainSize.z * (height - 1)) + 1;

        startX = Mathf.Clamp(startX, 0, width - 1);
        endX = Mathf.Clamp(endX, 0, width);
        startZ = Mathf.Clamp(startZ, 0, height - 1);
        endZ = Mathf.Clamp(endZ, 0, height);

        // Основной цикл обработки
        for (int x = startX; x < endX; x++)
        {
            for (int z = startZ; z < endZ; z++)
            {
                Vector2 point = new Vector2(
                    terrainPos.x + (x / (float)(width - 1)) * terrainSize.x,
                    terrainPos.z + (z / (float)(height - 1)) * terrainSize.z
                );

                if (IsPointUnderWater(point, waterTriangles))
                {
                    float currentHeight = heights[z, x];
                    float worldY = terrainPos.y + currentHeight * terrainSize.y;

                    if (worldY > minY)
                    {
                        float newHeight = (minY - terrainPos.y) / terrainSize.y;
                        newHeight = Mathf.Clamp(newHeight, 0, currentHeight);
                        heights[z, x] = newHeight;
                    }
                }
            }
        }

        terrainData.SetHeightsDelayLOD(0, 0, heights);
        terrain.ApplyDelayedHeightmapModification();
    }

    bool IsPointUnderWater(Vector2 point, List<Vector2[]> triangles)
    {
        foreach (Vector2[] triangle in triangles)
        {
            if (IsPointInTriangle(point, triangle[0], triangle[1], triangle[2]))
            {
                return true;
            }
        }
        return false;
    }

    bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 v0 = b - a;
        Vector2 v1 = c - a;
        Vector2 v2 = p - a;

        float dot00 = Vector2.Dot(v0, v0);
        float dot01 = Vector2.Dot(v0, v1);
        float dot02 = Vector2.Dot(v0, v2);
        float dot11 = Vector2.Dot(v1, v1);
        float dot12 = Vector2.Dot(v1, v2);

        float invDenom = 1.0f / (dot00 * dot11 - dot01 * dot01);
        float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
        float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

        return (u >= 0) && (v >= 0) && (u + v <= 1);
    }

    Rect GetWaterBoundsRect(List<Vector2[]> triangles)
    {
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;

        foreach (Vector2[] triangle in triangles)
        {
            foreach (Vector2 point in triangle)
            {
                if (point.x < minX) minX = point.x;
                if (point.x > maxX) maxX = point.x;
                if (point.y < minZ) minZ = point.y;
                if (point.y > maxZ) maxZ = point.y;
            }
        }

        return new Rect(minX, minZ, maxX - minX, maxZ - minZ);
    }

    Rect RectIntersection(Rect a, Rect b)
    {
        float xMin = Mathf.Max(a.x, b.x);
        float xMax = Mathf.Min(a.x + a.width, b.x + b.width);
        float yMin = Mathf.Max(a.y, b.y);
        float yMax = Mathf.Min(a.y + a.height, b.y + b.height);

        if (xMax >= xMin && yMax >= yMin)
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);

        return new Rect(0, 0, 0, 0);
    }
}