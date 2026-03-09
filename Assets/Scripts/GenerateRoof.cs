using System.Collections.Generic;
using System.Linq;
using TriangleNet.Geometry;
using UnityEngine;

public class GenerateRoof : MonoBehaviour
{
    public GameObject roof_onion;
    public BuildingMaterials buildingMaterials;

    // ============================================
    // ОПТИМИЗАЦИЯ: Кэшированные данные
    // ============================================
    private Dictionary<string, int> directionCache = new Dictionary<string, int>(16)
    {
        {"N", 0}, {"NNE", 22}, {"NE", 45}, {"ENE", 67},
        {"E", 90}, {"ESE", 122}, {"SE", 135}, {"SSE", 157},
        {"S", 180}, {"SSW", 202}, {"SW", 225}, {"WSW", 247},
        {"W", 270}, {"WNW", 292}, {"NW", 315}, {"NNW", 337}
    };

    // Предварительно выделенные списки для частых операций
    private List<Vector3> tempCorners = new List<Vector3>(16);
    private List<Vector3> innerCorners = new List<Vector3>(16);

    // Кэш материалов крыш
    private Dictionary<string, Material> roofMaterialCache = new Dictionary<string, Material>();

    private float DistanceFromPointToLineSegment(Vector3 p, Vector3 v, Vector3 w)
    {
        Vector2 p2 = new Vector2(p.x, p.z);
        Vector2 v2 = new Vector2(v.x, v.z);
        Vector2 w2 = new Vector2(w.x, w.z);

        float l2 = Vector2.SqrMagnitude(v2 - w2);
        if (l2 == 0) return Vector2.Distance(p2, v2);

        float t = Vector2.Dot(p2 - v2, w2 - v2) / l2;
        t = Mathf.Clamp01(t);

        Vector2 projection = v2 + t * (w2 - v2);
        return Vector2.Distance(p2, projection);
    }

    private void GetRoofRidgeAxis(List<Vector3> corners, int directionAngle, string orientation, out Vector3 ridgeDir, out float perpWidth)
    {
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        int cornerCount = corners.Count;
        for (int i = 0; i < cornerCount; i++)
        {
            var c = corners[i];
            if (c.x < minX) minX = c.x;
            if (c.x > maxX) maxX = c.x;
            if (c.z < minZ) minZ = c.z;
            if (c.z > maxZ) maxZ = c.z;
        }

        float sizeX = maxX - minX;
        float sizeZ = maxZ - minZ;

        bool ridgeIsX = true;

        if (directionAngle >= 0)
        {
            float normalized = directionAngle % 180;
            ridgeIsX = !(normalized > 45 && normalized < 135);
        }
        else if (!string.IsNullOrEmpty(orientation))
        {
            bool isLongX = sizeX > sizeZ;
            ridgeIsX = orientation == "along" ? isLongX : !isLongX;
        }
        else
        {
            ridgeIsX = sizeX > sizeZ;
        }

        if (ridgeIsX)
        {
            ridgeDir = Vector3.right;
            perpWidth = sizeZ;
        }
        else
        {
            ridgeDir = Vector3.forward;
            perpWidth = sizeX;
        }
    }

    public void CreateHippedRoof(List<Vector3> baseCorners, float min_height, float height, MeshData data, float angle, int directionAngle, string orientation)
    {
        if (baseCorners.Count < 3) return;
        baseCorners.Reverse();

        Vector3 ridgeDir;
        float perpWidth;
        GetRoofRidgeAxis(baseCorners, directionAngle, orientation, out ridgeDir, out perpWidth);

        float roofRise;
        if (angle > 0)
        {
            float run = perpWidth / 2.0f;
            float clampedAngle = Mathf.Clamp(angle, 5f, 85f);
            roofRise = run * Mathf.Tan(clampedAngle * Mathf.Deg2Rad);
        }
        else
        {
            roofRise = height - min_height;
        }
        float peakY = min_height + roofRise;

        Vector3 center = Vector3.zero;
        int cornerCount = baseCorners.Count;
        for (int i = 0; i < cornerCount; i++) center += baseCorners[i];
        center /= cornerCount;

        float minP = float.MaxValue, maxP = float.MinValue;
        for (int i = 0; i < cornerCount; i++)
        {
            float p = Vector3.Dot(baseCorners[i] - center, ridgeDir);
            if (p < minP) minP = p;
            if (p > maxP) maxP = p;
        }

        float lengthAlongRidge = maxP - minP;
        float ridgeLength = Mathf.Max(0, lengthAlongRidge - perpWidth);

        Vector3 ridgeStart = center - (ridgeDir * (ridgeLength * 0.5f));
        Vector3 ridgeEnd = center + (ridgeDir * (ridgeLength * 0.5f));

        ridgeStart.y = peakY;
        ridgeEnd.y = peakY;

        int baseOffset = data.Vertices.Count;

        for (int i = 0; i < cornerCount; i++)
            data.Vertices.Add(baseCorners[i] + Vector3.up * min_height);

        int ridgeStartIndex = data.Vertices.Count;
        data.Vertices.Add(ridgeStart);
        int ridgeEndIndex = data.Vertices.Count;
        data.Vertices.Add(ridgeEnd);

        for (int i = 0; i < cornerCount; i++)
        {
            int next = (i + 1) % cornerCount;

            Vector3 v1 = baseCorners[i];
            Vector3 v2 = baseCorners[next];

            float p1 = Vector3.Dot(v1 - center, ridgeDir);
            float p2 = Vector3.Dot(v2 - center, ridgeDir);

            int idxRidge1 = (p1 < 0) ? ridgeStartIndex : ridgeEndIndex;
            int idxRidge2 = (p2 < 0) ? ridgeStartIndex : ridgeEndIndex;

            if (ridgeLength <= 0.01f) { idxRidge1 = ridgeStartIndex; idxRidge2 = ridgeStartIndex; }

            int idxBase1 = baseOffset + i;
            int idxBase2 = baseOffset + next;

            if (idxRidge1 == idxRidge2)
            {
                data.Indices.Add(idxBase1);
                data.Indices.Add(idxBase2);
                data.Indices.Add(idxRidge1);
            }
            else
            {
                data.Indices.Add(idxBase1);
                data.Indices.Add(idxBase2);
                data.Indices.Add(idxRidge2);

                data.Indices.Add(idxBase1);
                data.Indices.Add(idxRidge2);
                data.Indices.Add(idxRidge1);
            }

            data.UV.Add(new Vector2(v1.x, v1.z));
        }

        RecalculateNormals(data, baseOffset);
    }

    /// <summary>
    /// Создаёт двускатную (gabled) крышу с правильными нормалями НАРУЖУ
    /// </summary>
    public void CreateGabledRoof(List<Vector3> baseCorners, float baseheight, float roofheight, MeshData data, float angle, int directionAngle, string orientation)
    {
        if (baseCorners == null || baseCorners.Count < 3) return;

        // 1. Очистка и подготовка точек (убираем дубликат последней точки, если контур замкнут)
        List<Vector3> points = new List<Vector3>(baseCorners);
        if (Vector3.Distance(points[0], points[points.Count - 1]) < 0.001f)
        {
            points.RemoveAt(points.Count - 1);
        }

        // Приводим контур к направлению по часовой стрелке (CW) для корректных нормалей
        if (!IsClockwise(points))
        {
            points.Reverse();
        }

        // 2. Ищем самую длинную грань, чтобы определить направление конька крыши
        Vector3 longestEdgeDir = Vector3.forward;
        float maxLength = 0;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 p1 = points[i];
            Vector3 p2 = points[(i + 1) % points.Count];
            float len = Vector2.Distance(new Vector2(p1.x, p1.z), new Vector2(p2.x, p2.z));
            if (len > maxLength)
            {
                maxLength = len;
                longestEdgeDir = new Vector3(p2.x - p1.x, 0, p2.z - p1.z).normalized;
            }
        }

        // Ось ската крыши (перпендикулярно коньку)
        Vector3 roofSlopeAxis = new Vector3(longestEdgeDir.z, 0, -longestEdgeDir.x);

        // 3. Находим границы здания вдоль оси ската
        float minSlope = float.MaxValue;
        float maxSlope = float.MinValue;
        foreach (var p in points)
        {
            float proj = Vector3.Dot(p, roofSlopeAxis);
            if (proj < minSlope) minSlope = proj;
            if (proj > maxSlope) maxSlope = proj;
        }

        float ridgeCenter = (minSlope + maxSlope) * 0.5f;
        float halfWidth = (maxSlope - minSlope) * 0.5f;
        if (halfWidth < 0.001f) halfWidth = 0.001f; // Защита от деления на ноль

        // Локальная функция для расчета высоты крыши в любой точке XZ
        float GetRoofHeight(Vector3 p)
        {
            float proj = Vector3.Dot(p, roofSlopeAxis);
            float distFromRidge = Mathf.Abs(proj - ridgeCenter);
            return baseheight + roofheight * (1f - distFromRidge / halfWidth);
        }

        // 4. Разделение крыши на левый и правый скаты и построение стен (фронтонов)
        List<Vector3> leftPoly = new List<Vector3>();
        List<Vector3> rightPoly = new List<Vector3>();

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 A = points[i];
            Vector3 B = points[(i + 1) % points.Count];

            float projA = Vector3.Dot(A, roofSlopeAxis) - ridgeCenter;
            float projB = Vector3.Dot(B, roofSlopeAxis) - ridgeCenter;

            // Распределяем точки по скатам
            if (projA <= 0.001f) leftPoly.Add(A);
            if (projA >= -0.001f) rightPoly.Add(A);

            // Если грань пересекает центральный конек, ее нужно разрезать
            if (projA * projB < 0)
            {
                float t = Mathf.Abs(projA) / (Mathf.Abs(projA) + Mathf.Abs(projB));
                Vector3 intersection = Vector3.Lerp(A, B, t);

                leftPoly.Add(intersection);
                rightPoly.Add(intersection);

                // Создаем стены для обеих половин разрезанной грани
                AddWall(A, intersection, data, baseheight, GetRoofHeight(A), GetRoofHeight(intersection));
                AddWall(intersection, B, data, baseheight, GetRoofHeight(intersection), GetRoofHeight(B));
            }
            else
            {
                // Создаем цельную стену
                AddWall(A, B, data, baseheight, GetRoofHeight(A), GetRoofHeight(B));
            }
        }

        // 5. Создаем верхние поверхности (скаты) крыши
        AddRoofCap(leftPoly, data, GetRoofHeight);
        AddRoofCap(rightPoly, data, GetRoofHeight);
    }

    // --- Вспомогательные методы ---

    private void AddWall(Vector3 p1, Vector3 p2, MeshData data, float bottomH, float topH1, float topH2)
    {
        if (Vector3.Distance(p1, p2) < 0.001f) return;

        int start = data.Vertices.Count;

        Vector3 v0 = new Vector3(p1.x, bottomH, p1.z); // Нижняя левая
        Vector3 v1 = new Vector3(p1.x, topH1, p1.z);   // Верхняя левая
        Vector3 v2 = new Vector3(p2.x, topH2, p2.z);   // Верхняя правая
        Vector3 v3 = new Vector3(p2.x, bottomH, p2.z); // Нижняя правая

        data.Vertices.AddRange(new[] { v0, v1, v2, v3 });

        float len = Vector3.Distance(p1, p2);
        data.UV.AddRange(new[] {
            new Vector2(0, 0), new Vector2(0, topH1 - bottomH),
            new Vector2(len, topH2 - bottomH), new Vector2(len, 0)
        });

        Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
        for (int i = 0; i < 4; i++) data.Normals.Add(normal);

        data.Indices.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
    }

    private void AddRoofCap(List<Vector3> poly2D, MeshData data, System.Func<Vector3, float> heightFunc)
    {
        if (poly2D.Count < 3) return;

        // Создаем 3D полигон с учетом высоты
        List<Vector3> poly3D = new List<Vector3>();
        foreach (var p in poly2D)
        {
            poly3D.Add(new Vector3(p.x, heightFunc(p), p.z));
        }

        // Триангуляция
        List<int> tris = TriangulateEarClipping(poly2D);
        if (tris.Count == 0) return;

        int start = data.Vertices.Count;

        // Вычисляем нормаль по первому треугольнику
        Vector3 normal = Vector3.Cross(poly3D[tris[1]] - poly3D[tris[0]], poly3D[tris[2]] - poly3D[tris[0]]).normalized;
        if (normal.y < 0) normal = -normal; // Нормаль крыши всегда смотрит преимущественно вверх

        foreach (var p in poly3D)
        {
            data.Vertices.Add(p);
            data.Normals.Add(normal);
            data.UV.Add(new Vector2(p.x, p.z));
        }

        foreach (var t in tris)
        {
            data.Indices.Add(start + t);
        }
    }

    // Простая триангуляция (Ear Clipping) для сложных невыпуклых форм
    private List<int> TriangulateEarClipping(List<Vector3> poly)
    {
        List<int> indices = new List<int>();
        int n = poly.Count;
        if (n < 3) return indices;

        List<int> V = new List<int>();
        for (int i = 0; i < n; i++) V.Add(i);

        int maxIterations = 2 * n;
        while (V.Count > 2 && maxIterations-- > 0)
        {
            for (int i = 0; i < V.Count; i++)
            {
                int prev = V[(i - 1 + V.Count) % V.Count];
                int curr = V[i];
                int next = V[(i + 1) % V.Count];

                Vector2 a = new Vector2(poly[prev].x, poly[prev].z);
                Vector2 b = new Vector2(poly[curr].x, poly[curr].z);
                Vector2 c = new Vector2(poly[next].x, poly[next].z);

                // Если угол вогнутый (Cross > 0 для направления по часовой стрелке) - пропускаем
                if ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) > 0.001f) continue;

                // Проверка: нет ли других точек внутри этого треугольника
                bool isEar = true;
                for (int j = 0; j < V.Count; j++)
                {
                    int pIdx = V[j];
                    if (pIdx == prev || pIdx == curr || pIdx == next) continue;
                    if (IsPointInTriangle(new Vector2(poly[pIdx].x, poly[pIdx].z), a, b, c))
                    {
                        isEar = false;
                        break;
                    }
                }

                if (isEar)
                {
                    indices.AddRange(new[] { prev, curr, next });
                    V.RemoveAt(i);
                    break;
                }
            }
        }
        return indices;
    }

    private bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float Cross(Vector2 v1, Vector2 v2) => v1.x * v2.y - v1.y * v2.x;
        bool b1 = Cross(b - a, p - a) < 0.0f;
        bool b2 = Cross(c - b, p - b) < 0.0f;
        bool b3 = Cross(a - c, p - c) < 0.0f;
        return ((b1 == b2) && (b2 == b3));
    }

    private bool IsClockwise(List<Vector3> poly)
    {
        float sum = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            Vector3 a = poly[i];
            Vector3 b = poly[(i + 1) % poly.Count];
            sum += (b.x - a.x) * (b.z + a.z);
        }
        return sum > 0;
    }

    void CreateGambrelRoof(List<Vector3> baseCorners, float min_height, float height, MeshData data, int directionAngle, string orientation)
    {
        if (baseCorners == null || baseCorners.Count < 3) return;

        Vector3 ridgeDir;
        float perpWidth;
        GetRoofRidgeAxis(baseCorners, directionAngle, orientation, out ridgeDir, out perpWidth);

        Vector3 center = Vector3.zero;
        int cornerCount = baseCorners.Count;
        for (int i = 0; i < cornerCount; i++) center += baseCorners[i];
        center /= cornerCount;

        float totalH = height - min_height;
        float midH = min_height + (totalH * 0.6f);
        float topH = height;

        int baseOffset = data.Vertices.Count;

        for (int i = 0; i < cornerCount; i++)
        {
            Vector3 baseP = baseCorners[i];

            Vector3 toP = baseP - center;
            float proj = Vector3.Dot(toP, ridgeDir);
            Vector3 centerOnAxis = center + (ridgeDir * proj);

            Vector3 vecOut = baseP - centerOnAxis;

            Vector3 midP = centerOnAxis + (vecOut * 0.7f);
            midP.y = midH;

            Vector3 topP = centerOnAxis;
            topP.y = topH;

            data.Vertices.Add(new Vector3(baseP.x, min_height, baseP.z));
            data.Vertices.Add(midP);
            data.Vertices.Add(topP);

            data.UV.Add(new Vector2(baseP.x, baseP.z));
            data.UV.Add(new Vector2(midP.x, midP.z));
            data.UV.Add(new Vector2(topP.x, topP.z));
        }

        for (int i = 0; i < cornerCount; i++)
        {
            int next = (i + 1) % cornerCount;

            int currentStrip = baseOffset + (i * 3);
            int nextStrip = baseOffset + (next * 3);

            int b1 = currentStrip, m1 = currentStrip + 1, t1 = currentStrip + 2;
            int b2 = nextStrip, m2 = nextStrip + 1, t2 = nextStrip + 2;

            data.Indices.Add(b1); data.Indices.Add(m1); data.Indices.Add(b2);
            data.Indices.Add(b2); data.Indices.Add(m1); data.Indices.Add(m2);

            data.Indices.Add(m1); data.Indices.Add(t1); data.Indices.Add(m2);
            data.Indices.Add(m2); data.Indices.Add(t1); data.Indices.Add(t2);
        }

        RecalculateNormals(data, baseOffset);
    }

    public void CreateDomeRoof(List<Vector3> baseCorners, float minHeight, float height, MeshData data, Vector2 min, Vector2 size)
    {
        const int numSegments = 6;
        int numCorners = baseCorners.Count;
        if (numCorners < 3) return;

        Vector3 center = Vector3.zero;
        for (int i = 0; i < numCorners; i++) center += baseCorners[i];
        center /= numCorners;
        center.y = minHeight;

        float domeHeight = height - minHeight;

        for (int i = 0; i <= numSegments; i++)
        {
            float t = i / (float)numSegments;
            float currentHeight = minHeight + (domeHeight * Mathf.Sin(t * Mathf.PI * 0.5f));

            for (int j = 0; j < numCorners; j++)
            {
                Vector3 interpolated = Vector3.Lerp(baseCorners[j] + Vector3.up * minHeight, center, t);
                interpolated.y = currentHeight;
                data.Vertices.Add(interpolated);
            }
        }

        Vector3 topCenter = center;
        topCenter.y = height;
        data.Vertices.Add(topCenter);
        int topIndex = data.Vertices.Count - 1;

        for (int i = 0; i < numSegments; i++)
        {
            for (int j = 0; j < numCorners; j++)
            {
                int nextJ = (j + 1) % numCorners;

                int currentA = i * numCorners + j;
                int currentB = i * numCorners + nextJ;
                int nextA = (i + 1) * numCorners + j;
                int nextB = (i + 1) * numCorners + nextJ;

                data.Indices.Add(currentA);
                data.Indices.Add(nextA);
                data.Indices.Add(currentB);

                data.Indices.Add(currentB);
                data.Indices.Add(nextA);
                data.Indices.Add(nextB);
            }
        }

        int lastRingStart = numSegments * numCorners;
        for (int j = 0; j < numCorners; j++)
        {
            int nextJ = (j + 1) % numCorners;
            data.Indices.Add(lastRingStart + j);
            data.Indices.Add(topIndex);
            data.Indices.Add(lastRingStart + nextJ);
        }

        CalculateNormalsDomeRoof(data, center, topIndex);
        GenerateUVDomeRoof(data, baseCorners);
    }

    private void CalculateNormalsDomeRoof(MeshData data, Vector3 center, int topIndex)
    {
        int vertexCount = data.Vertices.Count;
        for (int i = data.Normals.Count; i < vertexCount; i++)
        {
            if (i == topIndex)
            {
                data.Normals.Add(Vector3.up);
            }
            else
            {
                Vector3 normal = (data.Vertices[i] - center).normalized;
                data.Normals.Add(normal);
            }
        }
    }

    private void GenerateUVDomeRoof(MeshData data, List<Vector3> baseCorners)
    {
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        int cornerCount = baseCorners.Count;
        for (int i = 0; i < cornerCount; i++)
        {
            Vector3 corner = baseCorners[i];
            if (corner.x < minX) minX = corner.x;
            if (corner.x > maxX) maxX = corner.x;
            if (corner.z < minZ) minZ = corner.z;
            if (corner.z > maxZ) maxZ = corner.z;
        }

        float width = maxX - minX;
        float depth = maxZ - minZ;

        int vertexCount = data.Vertices.Count;
        for (int i = data.UV.Count; i < vertexCount; i++)
        {
            Vector3 vertex = data.Vertices[i];
            float u = width == 0 ? 0.5f : (vertex.x - minX) / width;
            float v = depth == 0 ? 0.5f : (vertex.z - minZ) / depth;
            data.UV.Add(new Vector2(u, v));
        }
    }

    private void CreatePyramidalRoof(List<Vector3> baseCorners, float min_height, float height, MeshData data, Vector2 min, Vector2 size)
    {
        if (baseCorners.Count < 4)
        {
            Debug.Log("Недостаточно вершин для создания пирамиды!");
            return;
        }

        if (!GR.IsClockwise(baseCorners))
        {
            baseCorners.Reverse();
        }

        int cornerCount = baseCorners.Count;
        for (int i = 0; i < cornerCount; i++)
        {
            Vector3 v = baseCorners[i];
            v.y = min_height;
            baseCorners[i] = v;
            data.Vertices.Add(v);
        }

        Vector3 topCenter = Vector3.up * height;
        data.Vertices.Add(topCenter);

        for (int i = 0; i < cornerCount; i++)
        {
            data.Indices.Add(cornerCount);
            data.Indices.Add(i);
            data.Indices.Add((i + 1) % cornerCount);
        }

        int firstCornerIndex = 0;
        for (int i = 1; i < cornerCount - 1; i++)
        {
            data.Indices.Add(firstCornerIndex);
            data.Indices.Add(i);
            data.Indices.Add(i + 1);
        }
    }

    private void CreateOnionRoof(List<Vector3> corners, float min_height, float height, MeshData data, Vector2 min, Vector2 size)
    {
        GameObject go = Instantiate(roof_onion) as GameObject;

        var onionRoof = go.GetComponent<MeshFilter>().mesh;

        var verticesls = onionRoof.vertices;
        var triangles = onionRoof.triangles;

        float scale_fator = (Mathf.Min(size.x, size.y) / 2) * 100;

        for (int i = 0; i < verticesls.Length; i++)
        {
            var verticle = verticesls[i];
            data.Vertices.Add(new Vector3(verticle.x * scale_fator, (verticle.y * scale_fator) + min_height, verticle.z * scale_fator));
        }

        for (int i = 0; i < triangles.Length; i++)
        {
            data.Indices.Add(triangles[i]);
        }

        Destroy(go);
    }

    private int ParseDirection(string value)
    {
        if (int.TryParse(value, out int res))
        {
            return res;
        }

        if (directionCache.TryGetValue(value, out int cached))
        {
            return cached;
        }

        return 0;
    }

    private void RecalculateNormals(MeshData data, int startIndex)
    {
        int vertexCount = data.Vertices.Count;

        for (int i = startIndex; i < vertexCount; i++)
        {
            if (i < data.Normals.Count) data.Normals[i] = Vector3.zero;
            else data.Normals.Add(Vector3.zero);
        }

        int indexCount = data.Indices.Count;
        for (int i = 0; i < indexCount; i += 3)
        {
            int i1 = data.Indices[i];
            int i2 = data.Indices[i + 1];
            int i3 = data.Indices[i + 2];

            if (i1 >= startIndex || i2 >= startIndex || i3 >= startIndex)
            {
                if (i1 < vertexCount && i2 < vertexCount && i3 < vertexCount)
                {
                    Vector3 v1 = data.Vertices[i1];
                    Vector3 v2 = data.Vertices[i2];
                    Vector3 v3 = data.Vertices[i3];

                    Vector3 normal = Vector3.Cross(v2 - v1, v3 - v1).normalized;

                    if (i1 < data.Normals.Count) data.Normals[i1] += normal;
                    if (i2 < data.Normals.Count) data.Normals[i2] += normal;
                    if (i3 < data.Normals.Count) data.Normals[i3] += normal;
                }
            }
        }

        for (int i = startIndex; i < vertexCount; i++)
        {
            if (i < data.Normals.Count)
                data.Normals[i] = data.Normals[i].normalized;
        }
    }

    private bool IsPointInPolygon(Vector2 point, List<Vector3> polygon)
    {
        bool inside = false;
        int polygonCount = polygon.Count;
        for (int i = 0, j = polygonCount - 1; i < polygonCount; j = i++)
        {
            if (((polygon[i].z > point.y) != (polygon[j].z > point.y)) &&
                (point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].z) / (polygon[j].z - polygon[i].z) + polygon[i].x))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private float GetBarrelHeightAtPoint(Vector3 point, float minX, float maxX, float minZ, float maxZ, float roofHeight, bool tubeAlongZ)
    {
        float radius = tubeAlongZ ? (maxX - minX) / 2.0f : (maxZ - minZ) / 2.0f;
        float centerCoord = tubeAlongZ ? (minX + maxX) / 2.0f : (minZ + maxZ) / 2.0f;

        float currentPos = tubeAlongZ ? point.x : point.z;
        float distFromCenter = currentPos - centerCoord;

        float normalizedDist = Mathf.Clamp(distFromCenter / radius, -1f, 1f);
        float curveFactor = Mathf.Sqrt(1.0f - (normalizedDist * normalizedDist));

        return roofHeight * curveFactor;
    }

    private void CreateRoundRoofActual(List<Vector3> baseCorners, List<List<Vector3>> holes, float min_height, float height, MeshData data)
    {
        if (baseCorners.Count < 3) return;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        int cornerCount = baseCorners.Count;
        for (int i = 0; i < cornerCount; i++)
        {
            var c = baseCorners[i];
            if (c.x < minX) minX = c.x;
            if (c.x > maxX) maxX = c.x;
            if (c.z < minZ) minZ = c.z;
            if (c.z > maxZ) maxZ = c.z;
        }

        float widthX = maxX - minX;
        float depthZ = maxZ - minZ;

        bool tubeAlongZ = depthZ > widthX;
        float roofH = height - min_height;

        int startIndex = data.Vertices.Count;

        var polygon = new Polygon();
        polygon.Add(new Contour(baseCorners.Select(v => new Vertex(v.x, v.z)).ToList()));

        if (holes != null)
        {
            int holesCount = holes.Count;
            for (int h = 0; h < holesCount; h++)
            {
                var hole = holes[h];
                if (hole.Count > 2) polygon.Add(new Contour(hole.Select(v => new Vertex(v.x, v.z)).ToList()), true);
            }
        }

        float step = 0.5f;
        if (Mathf.Max(widthX, depthZ) / step > 60) step = Mathf.Max(widthX, depthZ) / 60.0f;

        for (float x = minX + step; x < maxX; x += step)
        {
            for (float z = minZ + step; z < maxZ; z += step)
            {
                if (IsPointInPolygon(new Vector2(x, z), baseCorners))
                {
                    polygon.Add(new Vertex(x, z));
                }
            }
        }

        var mesh = polygon.Triangulate();

        foreach (var v in mesh.Vertices)
        {
            float x = (float)v.X;
            float z = (float)v.Y;

            float h = GetBarrelHeightAtPoint(new Vector3(x, 0, z), minX, maxX, minZ, maxZ, roofH, tubeAlongZ);

            data.Vertices.Add(new Vector3(x, min_height + h, z));
            data.UV.Add(new Vector2(x, z));
        }

        foreach (var t in mesh.Triangles)
        {
            data.Indices.Add(startIndex + t.GetVertexID(2));
            data.Indices.Add(startIndex + t.GetVertexID(1));
            data.Indices.Add(startIndex + t.GetVertexID(0));
        }

        var allContours = new List<List<Vector3>> { baseCorners };
        if (holes != null) allContours.AddRange(holes);

        foreach (var contour in allContours)
        {
            int contourCount = contour.Count;
            for (int i = 0; i < contourCount; i++)
            {
                Vector3 start = contour[i];
                Vector3 end = contour[(i + 1) % contourCount];

                float dist = Vector3.Distance(start, end);
                int segments = Mathf.Max(1, Mathf.CeilToInt(dist / 0.5f));

                for (int s = 0; s < segments; s++)
                {
                    float t1 = (float)s / segments;
                    float t2 = (float)(s + 1) / segments;

                    Vector3 p1 = Vector3.Lerp(start, end, t1);
                    Vector3 p2 = Vector3.Lerp(start, end, t2);

                    float h1 = GetBarrelHeightAtPoint(p1, minX, maxX, minZ, maxZ, roofH, tubeAlongZ);
                    float h2 = GetBarrelHeightAtPoint(p2, minX, maxX, minZ, maxZ, roofH, tubeAlongZ);

                    int vIndex = data.Vertices.Count;

                    data.Vertices.Add(new Vector3(p1.x, min_height, p1.z));
                    data.Vertices.Add(new Vector3(p1.x, min_height + h1, p1.z));
                    data.Vertices.Add(new Vector3(p2.x, min_height, p2.z));
                    data.Vertices.Add(new Vector3(p2.x, min_height + h2, p2.z));

                    data.UV.Add(new Vector2(0, 0));
                    data.UV.Add(new Vector2(0, 1));
                    data.UV.Add(new Vector2(1, 0));
                    data.UV.Add(new Vector2(1, 1));

                    data.Indices.Add(vIndex);
                    data.Indices.Add(vIndex + 1);
                    data.Indices.Add(vIndex + 2);

                    data.Indices.Add(vIndex + 2);
                    data.Indices.Add(vIndex + 1);
                    data.Indices.Add(vIndex + 3);
                }
            }
        }

        RecalculateNormals(data, startIndex);
    }

    private void CreateSkillionRoofActual(List<Vector3> corners, List<List<Vector3>> holes, float min_height, float height, MeshData data, int directionAngle, float angle)
    {
        int startIndex = data.Vertices.Count;
        GR.CreateMeshWithHeight(corners, min_height, min_height + 0.1f, data, holes, true);

        float rad = directionAngle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));

        float minProj = float.MaxValue;
        float maxProj = float.MinValue;

        int cornerCount = corners.Count;
        for (int i = 0; i < cornerCount; i++)
        {
            float proj = Vector2.Dot(new Vector2(corners[i].x, corners[i].z), dir);
            if (proj < minProj) minProj = proj;
            if (proj > maxProj) maxProj = proj;
        }

        float length = maxProj - minProj;
        if (length < 0.01f) length = 1f;

        float roofRise;

        if (angle > 0)
        {
            float clampedAngle = Mathf.Clamp(angle, 0f, 85f);
            roofRise = length * Mathf.Tan(clampedAngle * Mathf.Deg2Rad);
        }
        else
        {
            roofRise = height - min_height;
        }

        int vertexCount = data.Vertices.Count;
        for (int i = startIndex; i < vertexCount; i++)
        {
            Vector3 v = data.Vertices[i];

            if (v.y > min_height + 0.05f)
            {
                float proj = Vector2.Dot(new Vector2(v.x, v.z), dir);
                float factor = (proj - minProj) / length;

                v.y = min_height + (roofRise * (1.0f - factor));

                data.Vertices[i] = v;
            }
        }

        RecalculateNormals(data, startIndex);
    }

    private void CreateMansardRoof(List<Vector3> baseCorners, float min_height, float height, MeshData data, int directionAngle, string orientation)
    {
        if (baseCorners.Count < 3) return;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        Vector3 center = Vector3.zero;

        int cornerCount = baseCorners.Count;
        for (int i = 0; i < cornerCount; i++)
        {
            var c = baseCorners[i];
            if (c.x < minX) minX = c.x;
            if (c.x > maxX) maxX = c.x;
            if (c.z < minZ) minZ = c.z;
            if (c.z > maxZ) maxZ = c.z;
            center += c;
        }
        center /= cornerCount;

        float sizeX = maxX - minX;
        float sizeZ = maxZ - minZ;
        float minDimension = Mathf.Min(sizeX, sizeZ);

        float totalRoofHeight = height - min_height;
        float breakHeightLocal = totalRoofHeight * 0.65f;
        float breakHeight = min_height + breakHeightLocal;

        float insetAmount = 2.0f;
        if (insetAmount * 2 >= minDimension) insetAmount = minDimension * 0.2f;

        innerCorners.Clear();
        for (int i = 0; i < cornerCount; i++)
        {
            Vector3 current = baseCorners[i];
            Vector3 dirToCenter = (center - current).normalized;
            Vector3 innerPoint = current + (dirToCenter * insetAmount);
            innerCorners.Add(innerPoint);
        }

        int baseVertOffset = data.Vertices.Count;

        for (int i = 0; i < cornerCount; i++)
        {
            int next = (i + 1) % cornerCount;

            Vector3 b1 = baseCorners[i];
            Vector3 b2 = baseCorners[next];
            Vector3 t1 = innerCorners[i];
            Vector3 t2 = innerCorners[next];

            Vector3 t1_h = new Vector3(t1.x, breakHeight, t1.z);
            Vector3 t2_h = new Vector3(t2.x, breakHeight, t2.z);

            Vector3 b1_h = new Vector3(b1.x, min_height, b1.z);
            Vector3 b2_h = new Vector3(b2.x, min_height, b2.z);

            data.Vertices.Add(b1_h);
            data.Vertices.Add(t1_h);
            data.Vertices.Add(b2_h);
            data.Vertices.Add(t2_h);

            data.UV.Add(new Vector2(b1.x, b1.z));
            data.UV.Add(new Vector2(t1.x, t1.z));
            data.UV.Add(new Vector2(b2.x, b2.z));
            data.UV.Add(new Vector2(t2.x, t2.z));

            int currentIdx = baseVertOffset + (i * 4);

            data.Indices.Add(currentIdx);
            data.Indices.Add(currentIdx + 1);
            data.Indices.Add(currentIdx + 2);

            data.Indices.Add(currentIdx + 2);
            data.Indices.Add(currentIdx + 1);
            data.Indices.Add(currentIdx + 3);

            Vector3 normal = Vector3.Cross(t1_h - b1_h, b2_h - b1_h).normalized;
            data.Normals.Add(normal);
            data.Normals.Add(normal);
            data.Normals.Add(normal);
            data.Normals.Add(normal);
        }

        tempCorners.Clear();
        tempCorners.AddRange(innerCorners);

        CreateHippedRoof(tempCorners, breakHeight, height, data, 0f, directionAngle, orientation);
    }

    private void CreateConeRoof(List<Vector3> baseCorners, float min_height, float height, MeshData data)
    {
        if (baseCorners.Count < 3) return;

        baseCorners.Reverse();

        Vector3 center = Vector3.zero;
        int cornerCount = baseCorners.Count;
        for (int i = 0; i < cornerCount; i++) center += baseCorners[i];
        center /= cornerCount;

        Vector3 peak = new Vector3(center.x, height, center.z);

        int baseOffset = data.Vertices.Count;

        for (int i = 0; i < cornerCount; i++)
        {
            Vector3 v = baseCorners[i];
            data.Vertices.Add(new Vector3(v.x, min_height, v.z));
            data.UV.Add(new Vector2(v.x, v.z));
        }

        data.Vertices.Add(peak);
        data.UV.Add(new Vector2(peak.x, peak.z));
        int peakIndex = baseOffset + cornerCount;

        for (int i = 0; i < cornerCount; i++)
        {
            int next = (i + 1) % cornerCount;

            data.Indices.Add(baseOffset + i);
            data.Indices.Add(baseOffset + next);
            data.Indices.Add(peakIndex);
        }

        RecalculateNormals(data, baseOffset);
    }

    public void GenerateRoofForObject(BaseDataObject dataobj, List<Vector3> corners, List<List<Vector3>> holesCorners, float minHeight, float height, Vector2 min, Vector2 size, BaseOsm geo, bool isUseOldTriangulation)
    {
        var roof = new GameObject("roof");

        roof.transform.SetParent(dataobj.transform);

        var mesh = roof.AddComponent<MeshFilter>().mesh;

        var meshRenderer = roof.AddComponent<MeshRenderer>();

        bool isRoofHeightExternalSet = false;

        float roof_height = 0.01f;

        if (geo.HasField("roof:height"))
        {
            roof_height = geo.GetValueFloatByKey("roof:height");
            isRoofHeightExternalSet = true;
        }
        else if (geo.HasField("roof:levels"))
        {
            roof_height = geo.GetValueFloatByKey("roof:levels") * 3.0f;
            isRoofHeightExternalSet = true;
        }

        var roof_type = "flat";

        if (geo.HasField("roof:shape"))
        {
            roof_type = geo.GetValueStringByKey("roof:shape");
        }

        float roofangle = 0.0f;

        if (geo.HasField("roof:angle"))
        {
            roofangle = geo.GetValueFloatByKey("roof:angle");
        }
        else if (geo.HasField("building:roof:angle"))
        {
            roofangle = geo.GetValueFloatByKey("building:roof:angle");
        }

        int roof_direction = 0;

        if (geo.HasField("roof:direction"))
        {
            var roof_direction_str = geo.GetValueStringByKey("roof:direction");
            roof_direction = ParseDirection(roof_direction_str);
        }

        string roof_orientation = null;

        if (geo.HasField("roof:orientation"))
        {
            roof_orientation = geo.GetValueStringByKey("roof:orientation");
        }
        else if (geo.HasField("building:roof:orientation"))
        {
            roof_orientation = geo.GetValueStringByKey("building:roof:orientation");
        }

        var tb = new MeshData();

        if (roof_type == "flat")
        {
            GR.CreateMeshWithHeight(corners, height, height + roof_height, tb, holesCorners);
        }
        else if (roof_type == "hipped")
        {
            if (!isRoofHeightExternalSet) roof_height = 6.0f;
            CreateHippedRoof(corners, height, height + roof_height, tb, roofangle, roof_direction, roof_orientation);
        }
        else if (roof_type == "gabled")
        {
            if (!isRoofHeightExternalSet) roof_height = 6.0f;
            CreateGabledRoof(corners, height, height + roof_height, tb, roofangle, roof_direction, roof_orientation);
        }
        else if (roof_type == "gambrel")
        {
            if (!isRoofHeightExternalSet) roof_height = 6.0f;
            CreateGambrelRoof(corners, height, height + roof_height, tb, roof_direction, roof_orientation);
        }
        else if (roof_type == "dome")
        {
            if (!isRoofHeightExternalSet) roof_height = Mathf.Min(size.x, size.y) / 2.0f;
            CreateDomeRoof(corners, height, height + roof_height, tb, min, size);
        }
        else if (roof_type == "pyramidal")
        {
            if (!isRoofHeightExternalSet) roof_height = 3.0f;
            CreatePyramidalRoof(corners, height, height + roof_height, tb, min, size);
        }
        else if (roof_type == "onion")
        {
            if (!isRoofHeightExternalSet) roof_height = 1.0f;
            CreateOnionRoof(corners, height, height + roof_height, tb, min, size);
        }
        else if (roof_type == "skillion")
        {
            if (!isRoofHeightExternalSet) roof_height = 3.0f;
            CreateSkillionRoofActual(corners, holesCorners, height, height + roof_height, tb, roof_direction, roofangle);
        }
        else if (roof_type == "round")
        {
            if (!isRoofHeightExternalSet) roof_height = 3.0f;
            CreateRoundRoofActual(corners, holesCorners, height, height + roof_height, tb);
        }
        else if (roof_type == "quadruple_saltbox")
        {
            if (!isRoofHeightExternalSet) roof_height = 4.0f;
            CreateQuadrupleSaltboxRoof(corners, height, height + roof_height, tb, roof_direction, size);
        }
        else if (roof_type == "saltbox")
        {
            if (!isRoofHeightExternalSet) roof_height = 4.5f;
            CreateSaltboxRoof(corners, holesCorners, height, height + roof_height, tb, roof_direction, size);
        }
        else if (roof_type == "mansard")
        {
            if (!isRoofHeightExternalSet) roof_height = 4.0f;
            CreateMansardRoof(corners, height, height + roof_height, tb, roof_direction, roof_orientation);
        }
        else if (roof_type == "cone")
        {
            if (!isRoofHeightExternalSet)
            {
                float radius = Mathf.Min(size.x, size.y) / 2.0f;
                roof_height = radius > 0.1f ? radius : 3.0f;
            }
            CreateConeRoof(corners, minHeight, minHeight + roof_height, tb);
        }
        else
        {
            Debug.Log("Try create roofs: " + roof_type);
            GR.CreateMeshWithHeight(corners, height, height + roof_height, tb, holesCorners);
        }

        mesh.vertices = tb.Vertices.ToArray();
        mesh.triangles = tb.Indices.ToArray();
        mesh.SetUVs(0, tb.UV);

        roof.transform.localPosition = Vector3.zero;

        if (geo.HasField("roof:material"))
        {
            var mat_name = geo.GetValueStringByKey("roof:material");
            var mat_by_tag = buildingMaterials.GetBuildingMaterialByName(mat_name);

            if (mat_by_tag != null)
            {
                meshRenderer.material = mat_by_tag;
            }
        }

        Color rouf_color = GR.SetOSMRoofColour(geo);

        meshRenderer.material.SetColor("_Color", rouf_color);
        meshRenderer.material.SetColor("_BaseColor", rouf_color);

        //Very bad hack ((
        meshRenderer.material.EnableKeyword("_DOUBLESIDED_ON");
        meshRenderer.material.SetFloat("_CullMode", 0f);

    }

    private void CreateQuadrupleSaltboxRoof(List<Vector3> baseCorners, float min_height, float height, MeshData data, int directionAngle, Vector2 size)
    {
        if (baseCorners.Count < 4) return;
        if (!GR.IsClockwise(baseCorners)) baseCorners.Reverse();

        Vector3 center = Vector3.zero;
        int cornerCount = baseCorners.Count;
        for (int i = 0; i < cornerCount; i++) center += baseCorners[i];
        center /= cornerCount;

        float offsetMagnitude = (Mathf.Min(size.x, size.y) * 0.5f);
        float rad = directionAngle * Mathf.Deg2Rad;
        Vector3 offsetDir = new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad));

        Vector3 peak = center + (offsetDir * offsetMagnitude);
        peak.y = height;

        int baseIndex = data.Vertices.Count;

        for (int i = 0; i < cornerCount; i++)
        {
            Vector3 v = baseCorners[i];
            v.y = min_height;
            data.Vertices.Add(v);
            data.UV.Add(new Vector2(v.x, v.z));
        }

        data.Vertices.Add(peak);
        data.UV.Add(new Vector2(peak.x, peak.z));
        int peakIndex = baseIndex + cornerCount;

        for (int i = 0; i < cornerCount; i++)
        {
            int next = (i + 1) % cornerCount;

            data.Indices.Add(baseIndex + i);
            data.Indices.Add(baseIndex + next);
            data.Indices.Add(peakIndex);
        }

        RecalculateNormals(data, baseIndex);
    }

    private void CreateSaltboxRoof(List<Vector3> baseCorners, List<List<Vector3>> holes, float min_height, float height, MeshData data, int directionAngle, Vector2 size)
    {
        if (baseCorners.Count < 3) return;

        float rad = directionAngle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));

        float minProj = float.MaxValue;
        float maxProj = float.MinValue;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        int cornerCount = baseCorners.Count;
        for (int i = 0; i < cornerCount; i++)
        {
            var c = baseCorners[i];
            float p = Vector2.Dot(new Vector2(c.x, c.z), dir);
            if (p < minProj) minProj = p;
            if (p > maxProj) maxProj = p;

            if (c.x < minX) minX = c.x;
            if (c.x > maxX) maxX = c.x;
            if (c.z < minZ) minZ = c.z;
            if (c.z > maxZ) maxZ = c.z;
        }

        float length = maxProj - minProj;
        if (length < 0.01f) length = 1f;

        float ridgeOffsetFactor = 0.35f;
        float ridgePos = maxProj - (length * ridgeOffsetFactor);

        var polygon = new TriangleNet.Geometry.Polygon();
        polygon.Add(new TriangleNet.Geometry.Contour(baseCorners.Select(v => new TriangleNet.Geometry.Vertex(v.x, v.z)).ToList()));

        if (holes != null)
        {
            int holesCount = holes.Count;
            for (int h = 0; h < holesCount; h++)
            {
                var hole = holes[h];
                if (hole.Count > 2) polygon.Add(new TriangleNet.Geometry.Contour(hole.Select(v => new TriangleNet.Geometry.Vertex(v.x, v.z)).ToList()), true);
            }
        }

        float step = 0.5f;
        if (Mathf.Max(maxX - minX, maxZ - minZ) / step > 60) step = Mathf.Max(maxX - minX, maxZ - minZ) / 60.0f;

        for (float x = minX + step; x < maxX; x += step)
        {
            for (float z = minZ + step; z < maxZ; z += step)
            {
                if (IsPointInPolygon(new Vector2(x, z), baseCorners))
                {
                    bool inHole = false;
                    if (holes != null)
                    {
                        int holesCount = holes.Count;
                        for (int h = 0; h < holesCount; h++)
                        {
                            if (IsPointInPolygon(new Vector2(x, z), holes[h])) { inHole = true; break; }
                        }
                    }
                    if (!inHole) polygon.Add(new TriangleNet.Geometry.Vertex(x, z));
                }
            }
        }

        var mesh = polygon.Triangulate();
        int startIndex = data.Vertices.Count;

        foreach (var v in mesh.Vertices)
        {
            float vx = (float)v.X;
            float vz = (float)v.Y;

            float currentProj = Vector2.Dot(new Vector2(vx, vz), dir);
            float distToRidge = currentProj - ridgePos;

            float factor = 0f;

            if (distToRidge > 0)
            {
                float frontLen = maxProj - ridgePos;
                factor = distToRidge / frontLen;
            }
            else
            {
                float backLen = ridgePos - minProj;
                factor = Mathf.Abs(distToRidge) / backLen;
            }

            float y = Mathf.Lerp(height, min_height, factor);

            data.Vertices.Add(new Vector3(vx, y, vz));
            data.UV.Add(new Vector2(vx, vz));
        }

        foreach (var t in mesh.Triangles)
        {
            data.Indices.Add(startIndex + t.GetVertexID(2));
            data.Indices.Add(startIndex + t.GetVertexID(1));
            data.Indices.Add(startIndex + t.GetVertexID(0));
        }

        CreateGableWallsForProceduralRoof(baseCorners, holes, min_height, height, dir, ridgePos, maxProj, minProj, data);

        RecalculateNormals(data, startIndex);
    }

    private void CreateGableWallsForProceduralRoof(List<Vector3> corners, List<List<Vector3>> holes, float min_height, float peak_height, Vector2 dir, float ridgePos, float maxProj, float minProj, MeshData data)
    {
        var allContours = new List<List<Vector3>> { corners };
        if (holes != null) allContours.AddRange(holes);

        foreach (var contour in allContours)
        {
            int contourCount = contour.Count;
            for (int i = 0; i < contourCount; i++)
            {
                Vector3 start = contour[i];
                Vector3 end = contour[(i + 1) % contourCount];

                float wallLen = Vector3.Distance(start, end);
                int segments = Mathf.Max(1, Mathf.CeilToInt(wallLen / 0.5f));

                for (int s = 0; s < segments; s++)
                {
                    float t1 = (float)s / segments;
                    float t2 = (float)(s + 1) / segments;
                    Vector3 p1 = Vector3.Lerp(start, end, t1);
                    Vector3 p2 = Vector3.Lerp(start, end, t2);

                    float GetH(Vector3 p)
                    {
                        float proj = Vector2.Dot(new Vector2(p.x, p.z), dir);
                        float dist = proj - ridgePos;
                        float f = (dist > 0) ? (dist / (maxProj - ridgePos)) : (Mathf.Abs(dist) / (ridgePos - minProj));
                        return Mathf.Lerp(peak_height, min_height, f);
                    }

                    float h1 = GetH(p1);
                    float h2 = GetH(p2);

                    int vIdx = data.Vertices.Count;
                    data.Vertices.Add(new Vector3(p1.x, min_height, p1.z));
                    data.Vertices.Add(new Vector3(p1.x, h1, p1.z));
                    data.Vertices.Add(new Vector3(p2.x, min_height, p2.z));
                    data.Vertices.Add(new Vector3(p2.x, h2, p2.z));

                    data.UV.Add(new Vector2(0, 0)); data.UV.Add(new Vector2(0, 1));
                    data.UV.Add(new Vector2(1, 0)); data.UV.Add(new Vector2(1, 1));

                    data.Indices.Add(vIdx); data.Indices.Add(vIdx + 1); data.Indices.Add(vIdx + 2);
                    data.Indices.Add(vIdx + 2); data.Indices.Add(vIdx + 1); data.Indices.Add(vIdx + 3);
                }
            }
        }
    }
}