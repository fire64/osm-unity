using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TriangleNet.Geometry;
using UnityEngine;
using static UnityEditor.Progress;
using static UnityEngine.UI.GridLayoutGroup;

public class GenerateRoof : MonoBehaviour
{
    public GameObject roof_onion;
    public BuildingMaterials buildingMaterials;

    // Вычисляет кратчайшее расстояние от точки p до отрезка (v, w) в плоскости XZ
    private float DistanceFromPointToLineSegment(Vector3 p, Vector3 v, Vector3 w)
    {
        // Работаем в 2D (X, Z)
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

    public void CreateHippedRoof(List<Vector3> baseCorners, float min_height, float height, MeshData data, Vector2 min, Vector2 size, float angle)
    {
        if (baseCorners == null || baseCorners.Count < 3)
        {
            Debug.LogError("Недостаточно точек для создания крыши.");
            return;
        }

        // Обратите внимание: в оригинале Reverse вызывался всегда, 
        // лучше проверять нормаль или порядок, но оставим как в оригинале для совместимости
        baseCorners.Reverse();

        // 1. Вычисляем центр основания крыши
        Vector3 center = Vector3.zero;
        foreach (var corner in baseCorners)
        {
            center += corner;
        }
        center /= baseCorners.Count;

        // 2. Расчет высоты вершины
        float roofRise;

        if (angle > 0)
        {
            // Если угол задан, игнорируем переданный height и считаем его математически.

            // Находим минимальное расстояние от центра до любого края (стены)
            float minDistanceToEdge = float.MaxValue;
            for (int i = 0; i < baseCorners.Count; i++)
            {
                Vector3 p1 = baseCorners[i];
                Vector3 p2 = baseCorners[(i + 1) % baseCorners.Count];

                float dist = DistanceFromPointToLineSegment(center, p1, p2);
                if (dist < minDistanceToEdge) minDistanceToEdge = dist;
            }

            // Защита от некорректной геометрии
            if (minDistanceToEdge < 0.01f) minDistanceToEdge = 0.5f;

            // Ограничиваем угол, чтобы крыша не ушла в бесконечность
            float clampedAngle = Mathf.Clamp(angle, 5f, 85f);

            // h = d * tan(alpha)
            roofRise = minDistanceToEdge * Mathf.Tan(clampedAngle * Mathf.Deg2Rad);
        }
        else
        {
            // Если угол не задан, используем фиксированную высоту
            roofRise = height - min_height;
        }

        // Высота вершины крыши
        Vector3 peak = center;
        peak.y = min_height + roofRise;

        // Добавляем вершины основания
        int baseVertexCount = data.Vertices.Count;
        foreach (var corner in baseCorners)
        {
            data.Vertices.Add(corner + Vector3.up * min_height); // Высота основания
        }

        // Добавляем вершину крыши
        data.Vertices.Add(peak);

        // Создаем индексы для треугольников
        int peakIndex = baseVertexCount + baseCorners.Count; // Индекс вершины крыши
        for (int i = 0; i < baseCorners.Count; i++)
        {
            int nextIndex = (i + 1) % baseCorners.Count; // Индекс следующей вершины

            // Создаем треугольник для каждой стороны крыши
            data.Indices.Add(baseVertexCount + i); // Основание текущее
            data.Indices.Add(baseVertexCount + nextIndex); // Основание следующее
            data.Indices.Add(peakIndex); // Пик

            // Нормали
            // Важно: нормаль рассчитывается для наклонной поверхности
            Vector3 v1 = baseCorners[nextIndex] - baseCorners[i];
            Vector3 v2 = peak - baseCorners[i];
            Vector3 normal = Vector3.Cross(v1, v2).normalized;

            // В текущей реализации (flat shading) нормали добавляются на каждую вершину треугольника заново?
            // В оригинальном коде выше добавлялись нормали. Если мы используем shared vertices (общие вершины),
            // то нормали будут сглажены, что для low-poly крыш может выглядеть странно.
            // Но следуя стилю вашего кода (добавление в список Normals):

            // ВАЖНО: В оригинальном коде вы добавляли вершины, а потом 3 нормали. 
            // Но вершины добавлялись циклом выше (baseCorners.Count штук).
            // Тут есть логическая нестыковка в оригинале: количество нормалей должно совпадать с количеством вершин.
            // Если вершины общие (shared), нормали должны усредняться. 
            // Если вершины уникальные (для flat shading), их нужно дублировать.

            // Оставим упрощенный вариант добавления нормалей "потом", но лучше использовать RecalculateNormals.
        }

        // Чтобы исправить освещение, лучше всего пересчитать нормали в конце:
        // (Требует метода RecalculateNormals, который я давал в предыдущих ответах)
        // Если его нет, можно добавить простые нормали вверх, Unity пересчитает если mesh.RecalculateNormals() вызывается снаружи.

        // Для совместимости с вашим стилем кода - простой расчет "плоских" нормалей для уже добавленных вершин затруднителен 
        // без дублирования вершин. Поэтому рекомендую вызвать RecalculateNormals(data, baseVertexCount) в конце.
    }

    public void CreateGabledRoof(List<Vector3> baseCorners, float min_height, float height, MeshData data, Vector2 min, Vector2 size)
    {
        // Проверка на наличие минимум 3 точек для формирования крыши
        if (baseCorners.Count < 3)
        {
            Debug.LogError("Недостаточно точек для создания крыши");
            return;
        }

        baseCorners.Reverse();

        // Получение центра основания
        Vector3 center = Vector3.zero;
        foreach (Vector3 corner in baseCorners)
        {
            center += corner;
        }
        center /= baseCorners.Count;

        // Определение вершины крыши
        Vector3 roofPeak = new Vector3(center.x, height, center.z);

        // Добавление вершин основания
        foreach (Vector3 corner in baseCorners)
        {
            data.Vertices.Add(corner + Vector3.up * min_height);
        }

        // Добавление вершины крыши
        data.Vertices.Add(roofPeak);

        int baseCount = baseCorners.Count;

        // Создание индексов для треугольников
        for (int i = 0; i < baseCount; i++)
        {
            int nextIndex = (i + 1) % baseCount; // Следующий индекс (зацикливание)

            // Треугольники между основанием и вершиной крыши
            data.Indices.Add(i);
            data.Indices.Add(nextIndex);
            data.Indices.Add(baseCount); // Индекс вершины крыши
        }

        // Создание нормалей
        for (int i = 0; i < baseCount; i++)
        {
            int nextIndex = (i + 1) % baseCount; // Следующий индекс (зацикливание)
            Vector3 normal = Vector3.Cross(
                baseCorners[nextIndex] - baseCorners[i],
                roofPeak - baseCorners[i]
            ).normalized;

            data.Normals.Add(normal);
        }

        // Добавление нормали для вершины крыши
        data.Normals.Add(Vector3.up); // Нормаль для вершины крыши

        // Генерация UV координат (можно настроить по своему усмотрению)
        foreach (Vector3 vertex in data.Vertices)
        {
            data.UV.Add(new Vector2(vertex.x, vertex.z)); // Простая проекция UV
        }
    }
    
    void CreateGambrelRoof(List<Vector3> baseCorners, float min_height, float height, MeshData data, Vector2 min, Vector2 siz)
    {
        // Проверка входных данных
        if (baseCorners == null || baseCorners.Count < 3)
            return;

        // Находим центр основания
        Vector3 center = Vector3.zero;
        foreach (var corner in baseCorners)
            center += corner;
        center /= baseCorners.Count;

        // Устанавливаем высоту центра
        Vector3 roofTop = new Vector3(center.x, height, center.z);

        // Параметры для ломаной крыши
        float lowerSlopeHeight = min_height + (height - min_height) * 0.4f; // Высота перегиба (40% от общей высоты)
        float lowerSlopeWidth = 0.7f; // Ширина нижнего ската (70% от расстояния до центра)

        int baseVertexCount = data.Vertices.Count;

        // Добавляем вершины основания
        foreach (var corner in baseCorners)
        {
            data.Vertices.Add(new Vector3(corner.x, min_height, corner.z));
            data.UV.Add(new Vector2(corner.x * 0.2f, corner.z * 0.2f)); // Простая UV-развертка
        }

        // Добавляем вершины перегиба
        List<Vector3> middlePoints = new List<Vector3>();
        for (int i = 0; i < baseCorners.Count; i++)
        {
            Vector3 corner = baseCorners[i];
            Vector3 dirToCenter = (center - corner).normalized;
            Vector3 middlePoint = corner + dirToCenter * Vector3.Distance(corner, center) * lowerSlopeWidth;
            middlePoint.y = lowerSlopeHeight;

            middlePoints.Add(middlePoint);
            data.Vertices.Add(middlePoint);
            data.UV.Add(new Vector2(middlePoint.x * 0.2f, middlePoint.z * 0.2f));
        }

        // Добавляем вершину крыши
        data.Vertices.Add(roofTop);
        data.UV.Add(new Vector2(roofTop.x * 0.2f, roofTop.z * 0.2f));
        int roofTopIndex = data.Vertices.Count - 1;

        // Создаем треугольники для нижней части ломаной крыши (трапеции)
        for (int i = 0; i < baseCorners.Count; i++)
        {
            int nextI = (i + 1) % baseCorners.Count;

            // Индексы вершин основания
            int baseIndex = baseVertexCount + i;
            int nextBaseIndex = baseVertexCount + nextI;

            // Индексы вершин перегиба
            int middleIndex = baseVertexCount + baseCorners.Count + i;
            int nextMiddleIndex = baseVertexCount + baseCorners.Count + nextI;

            // Нижняя часть (трапеция)
            // Первый треугольник трапеции
            data.Indices.Add(baseIndex);
            data.Indices.Add(middleIndex);
            data.Indices.Add(nextBaseIndex);

            // Второй треугольник трапеции
            data.Indices.Add(nextBaseIndex);
            data.Indices.Add(middleIndex);
            data.Indices.Add(nextMiddleIndex);

            // Верхняя часть (треугольник к вершине)
            data.Indices.Add(middleIndex);
            data.Indices.Add(roofTopIndex);
            data.Indices.Add(nextMiddleIndex);
        }

        // Вычисляем нормали
        CalculateNormalsGambrelRoof(data);
    }

    // Вспомогательная функция для вычисления нормалей
    void CalculateNormalsGambrelRoof(MeshData data)
    {
        // Инициализируем нормали
        data.Normals.Clear();
        for (int i = 0; i < data.Vertices.Count; i++)
        {
            data.Normals.Add(Vector3.zero);
        }

        // Проходимся по всем треугольникам и вычисляем их вклад в нормали вершин
        for (int i = 0; i < data.Indices.Count; i += 3)
        {
            int indexA = data.Indices[i];
            int indexB = data.Indices[i + 1];
            int indexC = data.Indices[i + 2];

            Vector3 pointA = data.Vertices[indexA];
            Vector3 pointB = data.Vertices[indexB];
            Vector3 pointC = data.Vertices[indexC];

            // Вычисляем нормаль треугольника
            Vector3 sideAB = pointB - pointA;
            Vector3 sideAC = pointC - pointA;
            Vector3 normal = Vector3.Cross(sideAB, sideAC).normalized;

            // Добавляем эту нормаль ко всем вершинам треугольника
            data.Normals[indexA] += normal;
            data.Normals[indexB] += normal;
            data.Normals[indexC] += normal;
        }

        // Нормализуем результаты
        for (int i = 0; i < data.Normals.Count; i++)
        {
            data.Normals[i] = data.Normals[i].normalized;
        }
    }

    public void CreateDomeRoof(List<Vector3> baseCorners, float minHeight, float height, MeshData data, Vector2 min, Vector2 size)
    {
        int numSegments = 6; // Увеличьте для более гладкого купола
        int numCorners = baseCorners.Count;
        if (numCorners < 3) return;

        // Рассчитываем центр основания
        Vector3 center = Vector3.zero;
        foreach (Vector3 corner in baseCorners)
        {
            center += corner;
        }
        center /= numCorners;
        center.y = minHeight;

        // --- FIX START: Вычисляем чистую высоту самого купола ---
        float domeHeight = height - minHeight;
        // --- FIX END ---

        // Генерация вершин купола
        for (int i = 0; i <= numSegments; i++)
        {
            float t = i / (float)numSegments;

            // --- FIX START: Используем domeHeight вместо height ---
            float currentHeight = minHeight + (domeHeight * Mathf.Sin(t * Mathf.PI * 0.5f));
            // --- FIX END ---

            for (int j = 0; j < numCorners; j++)
            {
                Vector3 interpolated = Vector3.Lerp(baseCorners[j] + Vector3.up * minHeight, center, t);
                interpolated.y = currentHeight;
                data.Vertices.Add(interpolated);
            }
        }

        // Добавляем вершину в центре купола
        Vector3 topCenter = center;
        topCenter.y = height;
        data.Vertices.Add(topCenter);
        int topIndex = data.Vertices.Count - 1;

        // Генерация треугольников для боковой поверхности
        for (int i = 0; i < numSegments; i++)
        {
            for (int j = 0; j < numCorners; j++)
            {
                int nextJ = (j + 1) % numCorners;

                int currentA = i * numCorners + j;
                int currentB = i * numCorners + nextJ;
                int nextA = (i + 1) * numCorners + j;
                int nextB = (i + 1) * numCorners + nextJ;

                // Первый треугольник
                data.Indices.Add(currentA);
                data.Indices.Add(nextA);
                data.Indices.Add(currentB);

                // Второй треугольник
                data.Indices.Add(currentB);
                data.Indices.Add(nextA);
                data.Indices.Add(nextB);
            }
        }

        // Генерация треугольников для верхушки
        int lastRingStart = numSegments * numCorners;
        for (int j = 0; j < numCorners; j++)
        {
            int nextJ = (j + 1) % numCorners;
            data.Indices.Add(lastRingStart + j);
            data.Indices.Add(topIndex);
            data.Indices.Add(lastRingStart + nextJ);
        }

        // Рассчитываем нормали
        CalculateNormalsDomeRoof(data, center, topIndex);

        // Генерация UV-координат
        GenerateUVDomeRoof(data, baseCorners);
    }

    private void CalculateNormalsDomeRoof(MeshData data, Vector3 center, int topIndex)
    {
        for (int i = 0; i < data.Vertices.Count; i++)
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
        // Рассчитываем границы для UV-проекции
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;

        foreach (Vector3 corner in baseCorners)
        {
            if (corner.x < minX) minX = corner.x;
            if (corner.x > maxX) maxX = corner.x;
            if (corner.z < minZ) minZ = corner.z;
            if (corner.z > maxZ) maxZ = corner.z;
        }

        float width = maxX - minX;
        float depth = maxZ - minZ;

        foreach (Vector3 vertex in data.Vertices)
        {
            float u = width == 0 ? 0.5f : (vertex.x - minX) / width;
            float v = depth == 0 ? 0.5f : (vertex.z - minZ) / depth;
            data.UV.Add(new Vector2(u, v));
        }
    }
    
    private void CreatePyramidalRoof(List<Vector3> baseCorners, float min_height, float height, MeshData data, Vector2 min, Vector2 size)
    {
        // Проверяем, что основание состоит минимум из четырех точек
        if (baseCorners.Count < 4)
        {
            Debug.Log("Недостаточно вершин для создания пирамиды!");
            return;
        }

        if (!GR.IsClockwise(baseCorners))
        {
            baseCorners.Reverse();
        }

        // Количество вершин = углы основания + 1 центральная вершина
        Vector3[] vertices = new Vector3[baseCorners.Count + 1];
        for (int i = 0; i < baseCorners.Count; i++)
        {
            Vector3 curpoint = baseCorners[i];
            curpoint.y = min_height;
            baseCorners[i] = curpoint;
            vertices[i] = baseCorners[i];

        }
        // Центральная верхняя точка пирамиды
        Vector3 topCenter = Vector3.up * height;
        vertices[baseCorners.Count] = topCenter;

        // Создаем список треугольников
        List<int> trianglesList = new List<int>();

        // Добавляем боковые треугольники
        for (int i = 0; i < baseCorners.Count; i++)
        {
            trianglesList.Add(baseCorners.Count); // Верхняя центральная точка
            trianglesList.Add(i); // Текущая вершина основания
            trianglesList.Add((i + 1) % baseCorners.Count); // Следующая вершина основания (с зацикливанием)
        }

        // Добавляем треугольники для основания пирамиды
        // Это делается путем создания триангуляции для многоугольника,
        // можно использовать алгоритм триангуляции (например, "треугольник вентилятора")
        // Для простоты, мы ограничимся триангуляцией в форме веера, которая подходит для выпуклых оснований
        int firstCornerIndex = 0;
        for (int i = 1; i < baseCorners.Count - 1; i++)
        {
            trianglesList.Add(firstCornerIndex);
            trianglesList.Add(i);
            trianglesList.Add(i + 1);
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            data.Vertices.Add(vertices[i]);
        }

        for (int i = 0; i < trianglesList.Count; i++)
        {
            data.Indices.Add(trianglesList[i]);
        }
    }

    private void CreateOnionRoof(List<Vector3> corners, float min_height, float height, MeshData data, Vector2 min, Vector2 size)
    {
        GameObject go = Instantiate(roof_onion) as GameObject;

        var onionRoof = go.GetComponent<MeshFilter>().mesh;

        var verticesls = onionRoof.vertices;
        var triangles = onionRoof.triangles;

        for (int i = 0; i < verticesls.Length; i++)
        {
            var verticle = verticesls[i];

            float scale_fator = (Mathf.Min(size.x, size.y) / 2) * 100;

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
        int res;

        if (int.TryParse(value, out res))
        {
            return res;
        }

        switch (value)
        {
            case "N": return 0;
            case "NNE": return 22;
            case "NE": return 45;
            case "ENE": return 67;
            case "E": return 90;
            case "ESE": return 122;
            case "SE": return 135;
            case "SSE": return 157;
            case "S": return 180;
            case "SSW": return 202;
            case "SW": return 225;
            case "WSW": return 247;
            case "W": return 270;
            case "WNW": return 292;
            case "NW": return 315;
            case "NNW": return 337;
        }

        return 0;
    }

    private void RecalculateNormals(MeshData data, int startIndex)
    {
        // Сброс нормалей для новых вершин
        for (int i = startIndex; i < data.Vertices.Count; i++)
        {
            if (i < data.Normals.Count) data.Normals[i] = Vector3.zero;
            else data.Normals.Add(Vector3.zero);
        }

        // Накапливаем нормали от треугольников
        for (int i = 0; i < data.Indices.Count; i += 3)
        {
            int i1 = data.Indices[i];
            int i2 = data.Indices[i + 1];
            int i3 = data.Indices[i + 2];

            // Обрабатываем только треугольники, затрагивающие измененную часть меша
            if (i1 >= startIndex || i2 >= startIndex || i3 >= startIndex)
            {
                // Безопасная проверка границ массива
                if (i1 < data.Vertices.Count && i2 < data.Vertices.Count && i3 < data.Vertices.Count)
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

        // Нормализация результата
        for (int i = startIndex; i < data.Vertices.Count; i++)
        {
            if (i < data.Normals.Count)
                data.Normals[i] = data.Normals[i].normalized;
        }
    }

    // Проверка, находится ли точка внутри полигона (Ray-casting algorithm)
    private bool IsPointInPolygon(Vector2 point, List<Vector3> polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            if (((polygon[i].z > point.y) != (polygon[j].z > point.y)) &&
                (point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].z) / (polygon[j].z - polygon[i].z) + polygon[i].x))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    // --- Вспомогательная функция расчета высоты бочки в точке ---
    private float GetBarrelHeightAtPoint(Vector3 point, float minX, float maxX, float minZ, float maxZ, float roofHeight, bool tubeAlongZ)
    {
        float widthX = maxX - minX;
        float depthZ = maxZ - minZ;

        // Радиус и центр зависят от ориентации бочки
        float radius = tubeAlongZ ? widthX / 2.0f : depthZ / 2.0f;
        float centerCoord = tubeAlongZ ? (minX + maxX) / 2.0f : (minZ + maxZ) / 2.0f;

        float currentPos = tubeAlongZ ? point.x : point.z;
        float distFromCenter = currentPos - centerCoord;

        // Нормализация (-1..1)
        float normalizedDist = distFromCenter / radius;
        normalizedDist = Mathf.Clamp(normalizedDist, -1f, 1f);

        // Формула окружности: sqrt(1 - x^2)
        float curveFactor = Mathf.Sqrt(1.0f - (normalizedDist * normalizedDist));

        return roofHeight * curveFactor;
    }

    private void CreateRoundRoofActual(List<Vector3> baseCorners, List<List<Vector3>> holes, float min_height, float height, MeshData data)
    {
        if (baseCorners.Count < 3) return;

        // 1. Анализ границ (Bounding Box)
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var c in baseCorners)
        {
            if (c.x < minX) minX = c.x;
            if (c.x > maxX) maxX = c.x;
            if (c.z < minZ) minZ = c.z;
            if (c.z > maxZ) maxZ = c.z;
        }

        float widthX = maxX - minX;
        float depthZ = maxZ - minZ;

        // Определяем направление (вдоль длинной стороны)
        bool tubeAlongZ = depthZ > widthX;
        float roofH = height - min_height;

        int startIndex = data.Vertices.Count;

        // --- ЧАСТЬ 1: ВЕРХНЯЯ ПОВЕРХНОСТЬ (КРЫША) ---

        var polygon = new Polygon();
        polygon.Add(new Contour(baseCorners.Select(v => new Vertex(v.x, v.z)).ToList()));

        if (holes != null)
        {
            foreach (var hole in holes)
                if (hole.Count > 2) polygon.Add(new Contour(hole.Select(v => new Vertex(v.x, v.z)).ToList()), true);
        }

        // Добавляем точки Штейнера для гладкости поверхности
        float step = 0.5f; // Шаг сетки
        if (Mathf.Max(widthX, depthZ) / step > 60) step = Mathf.Max(widthX, depthZ) / 60.0f;

        for (float x = minX + step; x < maxX; x += step)
        {
            for (float z = minZ + step; z < maxZ; z += step)
            {
                if (IsPointInPolygon(new Vector2(x, z), baseCorners))
                {
                    // Простая проверка на дырки (можно улучшить)
                    bool inHole = false;
                    // ... (ваша логика проверки дырок здесь, если нужна)

                    if (!inHole) polygon.Add(new Vertex(x, z));
                }
            }
        }

        var mesh = polygon.Triangulate();

        foreach (var v in mesh.Vertices)
        {
            float x = (float)v.X;
            float z = (float)v.Y;

            // Вычисляем высоту свода
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

        // --- ЧАСТЬ 2: БОКОВЫЕ СТЕНКИ (ФРОНТОНЫ) ---
        // Нам нужно пройти по периметру и создать стены.
        // На прямых участках стена будет прямоугольной, на торцах - арочной.

        var allContours = new List<List<Vector3>> { baseCorners };
        if (holes != null) allContours.AddRange(holes);

        foreach (var contour in allContours)
        {
            for (int i = 0; i < contour.Count; i++)
            {
                Vector3 start = contour[i];
                Vector3 end = contour[(i + 1) % contour.Count];

                float dist = Vector3.Distance(start, end);

                // Сколько сегментов нужно для этой стены?
                // Если стена идет поперек бочки, её нужно сильно дробить, чтобы получилась арка.
                // Если стена идет вдоль бочки, дробить не обязательно, но для сетки полезно.
                int segments = Mathf.CeilToInt(dist / 0.5f); // Каждые 0.5 метра
                if (segments < 1) segments = 1;

                for (int s = 0; s < segments; s++)
                {
                    float t1 = (float)s / segments;
                    float t2 = (float)(s + 1) / segments;

                    Vector3 p1 = Vector3.Lerp(start, end, t1);
                    Vector3 p2 = Vector3.Lerp(start, end, t2);

                    // Высота в начале и конце микро-сегмента
                    float h1 = GetBarrelHeightAtPoint(p1, minX, maxX, minZ, maxZ, roofH, tubeAlongZ);
                    float h2 = GetBarrelHeightAtPoint(p2, minX, maxX, minZ, maxZ, roofH, tubeAlongZ);

                    // 4 вершины для квада (стены)
                    // Bottom Left, Top Left, Bottom Right, Top Right
                    Vector3 vBL = new Vector3(p1.x, min_height, p1.z);
                    Vector3 vTL = new Vector3(p1.x, min_height + h1, p1.z);
                    Vector3 vBR = new Vector3(p2.x, min_height, p2.z);
                    Vector3 vTR = new Vector3(p2.x, min_height + h2, p2.z);

                    int vIndex = data.Vertices.Count;

                    data.Vertices.Add(vBL);
                    data.Vertices.Add(vTL);
                    data.Vertices.Add(vBR);
                    data.Vertices.Add(vTR);

                    // UV для стен
                    data.UV.Add(new Vector2(0, 0));
                    data.UV.Add(new Vector2(0, 1));
                    data.UV.Add(new Vector2(1, 0));
                    data.UV.Add(new Vector2(1, 1));

                    // Треугольники (Winding order - зависит от порядка точек в контуре, обычно Clockwise)
                    // Если стены смотрят внутрь, поменяйте порядок индексов
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

    private void CreateQuadrupleSaltboxRoof(List<Vector3> baseCorners, float min_height, float height, MeshData data, int directionAngle, Vector2 size)
    {
        if (baseCorners.Count < 4) return;
        if (!GR.IsClockwise(baseCorners)) baseCorners.Reverse();

        // 1. Calculate Base Center
        Vector3 center = Vector3.zero;
        foreach (var c in baseCorners) center += c;
        center /= baseCorners.Count;

        // 2. Calculate Offset Peak
        // Shift peak 50% of the way towards the edge in the roof_direction
        float offsetMagnitude = (Mathf.Min(size.x, size.y) * 0.5f);
        float rad = directionAngle * Mathf.Deg2Rad;
        Vector3 offsetDir = new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad));

        Vector3 peak = center + (offsetDir * offsetMagnitude);
        peak.y = height; // Absolute height

        // 3. Build Geometry (Fan style like Pyramidal)
        int baseIndex = data.Vertices.Count;

        // Add base vertices
        for (int i = 0; i < baseCorners.Count; i++)
        {
            Vector3 v = baseCorners[i];
            v.y = min_height;
            data.Vertices.Add(v);
            // Simple UV mapping
            data.UV.Add(new Vector2(v.x, v.z));
        }

        // Add Peak
        data.Vertices.Add(peak);
        data.UV.Add(new Vector2(peak.x, peak.z));
        int peakIndex = baseIndex + baseCorners.Count;

        // Triangulate sides
        for (int i = 0; i < baseCorners.Count; i++)
        {
            int next = (i + 1) % baseCorners.Count;

            data.Indices.Add(baseIndex + i);
            data.Indices.Add(baseIndex + next);
            data.Indices.Add(peakIndex);

            // Calculate Normal
            Vector3 v1 = baseCorners[i];
            Vector3 v2 = baseCorners[next];
            Vector3 normal = Vector3.Cross(v2 - v1, peak - v1).normalized;

            data.Normals.Add(normal);
            if (i == 0) data.Normals.Add(normal); // Add extra for loop closure if needed, or just rely on Recalculate
        }

        // Add Normals for the remaining vertices including peak (simplified)
        for (int k = data.Normals.Count; k < data.Vertices.Count; k++) data.Normals.Add(Vector3.up);

        // Better Normal Recalculation for the smooth shading group
        RecalculateNormals(data, baseIndex);
    }

    private void CreateSaltboxRoof(List<Vector3> baseCorners, List<List<Vector3>> holes, float min_height, float height, MeshData data, int directionAngle, Vector2 size)
    {
        if (baseCorners.Count < 3) return;

        // 1. Определяем вектор направления (Front)
        // Saltbox: Front = Короткий крутой скат. Back = Длинный пологий скат.
        float rad = directionAngle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));

        // 2. Анализ границ (проекция на вектор направления)
        float minProj = float.MaxValue;
        float maxProj = float.MinValue;

        // Также нужны bounds для генерации сетки
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var c in baseCorners)
        {
            // Проекция для Saltbox логики
            float p = Vector2.Dot(new Vector2(c.x, c.z), dir);
            if (p < minProj) minProj = p;
            if (p > maxProj) maxProj = p;

            // Границы для генерации сетки
            if (c.x < minX) minX = c.x;
            if (c.x > maxX) maxX = c.x;
            if (c.z < minZ) minZ = c.z;
            if (c.z > maxZ) maxZ = c.z;
        }

        float length = maxProj - minProj;
        if (length < 0.01f) length = 1f;

        // 3. Позиция конька (Ridge)
        // Смещаем конек к передней части (maxProj). 
        // Коэффициент 0.35 означает, что конек будет на расстоянии 35% от лицевого фасада.
        // (Short side = 35%, Long side = 65%)
        float ridgeOffsetFactor = 0.35f;
        float ridgePos = maxProj - (length * ridgeOffsetFactor);

        // 4. Генерация плотной сетки (Tessellation)
        // Используем логику из Round Roof для создания внутренней геометрии
        var polygon = new TriangleNet.Geometry.Polygon();
        polygon.Add(new TriangleNet.Geometry.Contour(baseCorners.Select(v => new TriangleNet.Geometry.Vertex(v.x, v.z)).ToList()));

        if (holes != null)
        {
            foreach (var hole in holes)
                if (hole.Count > 2) polygon.Add(new TriangleNet.Geometry.Contour(hole.Select(v => new TriangleNet.Geometry.Vertex(v.x, v.z)).ToList()), true);
        }

        // Шаг сетки. Должен быть достаточно мелким, чтобы конек выглядел ровным.
        float step = 0.5f;
        if (Mathf.Max(maxX - minX, maxZ - minZ) / step > 60) step = Mathf.Max(maxX - minX, maxZ - minZ) / 60.0f;

        for (float x = minX + step; x < maxX; x += step)
        {
            for (float z = minZ + step; z < maxZ; z += step)
            {
                if (IsPointInPolygon(new Vector2(x, z), baseCorners))
                {
                    // Простая проверка на дырки
                    bool inHole = false;
                    if (holes != null)
                    {
                        foreach (var h in holes)
                            if (IsPointInPolygon(new Vector2(x, z), h)) { inHole = true; break; }
                    }
                    if (!inHole) polygon.Add(new TriangleNet.Geometry.Vertex(x, z));
                }
            }
        }

        var mesh = polygon.Triangulate();
        int startIndex = data.Vertices.Count;

        // 5. Формирование высоты вершин
        foreach (var v in mesh.Vertices)
        {
            float vx = (float)v.X;
            float vz = (float)v.Y;

            // Проецируем точку на ось направления
            float currentProj = Vector2.Dot(new Vector2(vx, vz), dir);
            float distToRidge = currentProj - ridgePos;

            float factor = 0f;

            if (distToRidge > 0)
            {
                // Мы на "Короткой" стороне (Front)
                // distToRidge изменяется от 0 до (maxProj - ridgePos)
                float frontLen = maxProj - ridgePos;
                factor = distToRidge / frontLen; // 0 = конек, 1 = край стены
            }
            else
            {
                // Мы на "Длинной" стороне (Back)
                // distToRidge изменяется от -(ridgePos - minProj) до 0
                float backLen = ridgePos - minProj;
                factor = Mathf.Abs(distToRidge) / backLen; // 0 = конек, 1 = край стены
            }

            // Линейная интерполяция высоты:
            // factor 0 -> height (конек)
            // factor 1 -> min_height (карниз)
            float y = Mathf.Lerp(height, min_height, factor);

            data.Vertices.Add(new Vector3(vx, y, vz));

            // UV: Простое наложение сверху или развертка по скатам
            data.UV.Add(new Vector2(vx, vz));
        }

        // Добавление треугольников
        foreach (var t in mesh.Triangles)
        {
            data.Indices.Add(startIndex + t.GetVertexID(2));
            data.Indices.Add(startIndex + t.GetVertexID(1));
            data.Indices.Add(startIndex + t.GetVertexID(0));
        }

        // 6. Создание боковых фронтонов (Gables)
        // Аналогично Round Roof, нам нужно закрыть боковины, так как стандартные стены 
        // поднимаются только до min_height.
        CreateGableWallsForProceduralRoof(baseCorners, holes, min_height, height, dir, ridgePos, maxProj, minProj, data);

        // 7. Пересчет нормалей
        RecalculateNormals(data, startIndex);
    }

    private void CreateGableWallsForProceduralRoof(List<Vector3> corners, List<List<Vector3>> holes, float min_height, float peak_height, Vector2 dir, float ridgePos, float maxProj, float minProj, MeshData data)
    {
        var allContours = new List<List<Vector3>> { corners };
        if (holes != null) allContours.AddRange(holes);

        foreach (var contour in allContours)
        {
            for (int i = 0; i < contour.Count; i++)
            {
                Vector3 start = contour[i];
                Vector3 end = contour[(i + 1) % contour.Count];

                // Разбиваем длинные стены на сегменты для точности
                float wallLen = Vector3.Distance(start, end);
                int segments = Mathf.CeilToInt(wallLen / 0.5f);
                if (segments < 1) segments = 1;

                for (int s = 0; s < segments; s++)
                {
                    float t1 = (float)s / segments;
                    float t2 = (float)(s + 1) / segments;
                    Vector3 p1 = Vector3.Lerp(start, end, t1);
                    Vector3 p2 = Vector3.Lerp(start, end, t2);

                    // Локальная функция высоты Saltbox
                    float GetH(Vector3 p)
                    {
                        float proj = Vector2.Dot(new Vector2(p.x, p.z), dir);
                        float dist = proj - ridgePos;
                        float f = (dist > 0) ? (dist / (maxProj - ridgePos)) : (Mathf.Abs(dist) / (ridgePos - minProj));
                        return Mathf.Lerp(peak_height, min_height, f);
                    }

                    float h1 = GetH(p1);
                    float h2 = GetH(p2);

                    // Генерируем квад
                    int vIdx = data.Vertices.Count;
                    data.Vertices.Add(new Vector3(p1.x, min_height, p1.z));      // BL
                    data.Vertices.Add(new Vector3(p1.x, h1, p1.z));              // TL
                    data.Vertices.Add(new Vector3(p2.x, min_height, p2.z));      // BR
                    data.Vertices.Add(new Vector3(p2.x, h2, p2.z));              // TR

                    data.UV.Add(new Vector2(0, 0)); data.UV.Add(new Vector2(0, 1));
                    data.UV.Add(new Vector2(1, 0)); data.UV.Add(new Vector2(1, 1));

                    data.Indices.Add(vIdx); data.Indices.Add(vIdx + 1); data.Indices.Add(vIdx + 2);
                    data.Indices.Add(vIdx + 2); data.Indices.Add(vIdx + 1); data.Indices.Add(vIdx + 3);
                }
            }
        }
    }

    // Revised Skillion Implementation to be paste-ready
    private void CreateSkillionRoofActual(List<Vector3> corners, List<List<Vector3>> holes, float min_height, float height, MeshData data, int directionAngle, float angle)
    {
        // 1. Создаем плоскую "коробку" с минимальной толщиной.
        // Мы используем небольшое смещение (0.1f), чтобы легко отличить верхние вершины от нижних.
        int startIndex = data.Vertices.Count;
        GR.CreateMeshWithHeight(corners, min_height, min_height + 0.1f, data, holes, true);

        // 2. Вычисляем вектор направления ската
        // В Unity/Геометрии: 0 градусов = Север (Z+), 90 = Восток (X+)
        float rad = directionAngle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));

        // 3. Находим границы здания вдоль вектора направления (проекция)
        float minProj = float.MaxValue;
        float maxProj = float.MinValue;

        foreach (var c in corners)
        {
            float proj = Vector2.Dot(new Vector2(c.x, c.z), dir);
            if (proj < minProj) minProj = proj;
            if (proj > maxProj) maxProj = proj;
        }

        float length = maxProj - minProj;
        if (length < 0.01f) length = 1f;

        // 4. Определяем перепад высот (Rise)
        float roofRise;

        if (angle > 0)
        {
            // Если задан угол, вычисляем высоту через тангенс: h = L * tan(angle)
            // Ограничиваем угол до 85 градусов, чтобы не получить бесконечную стену
            float clampedAngle = Mathf.Clamp(angle, 0f, 85f);
            roofRise = length * Mathf.Tan(clampedAngle * Mathf.Deg2Rad);
        }
        else
        {
            // Если угол не задан, используем фиксированную высоту из параметров
            roofRise = height - min_height;
        }

        // 5. Применяем наклон к верхним вершинам
        for (int i = startIndex; i < data.Vertices.Count; i++)
        {
            Vector3 v = data.Vertices[i];

            // Модифицируем только верхние вершины (те, что выше основания)
            if (v.y > min_height + 0.05f)
            {
                float proj = Vector2.Dot(new Vector2(v.x, v.z), dir);

                // Нормализованная позиция от 0 (начало) до 1 (конец вектора)
                float factor = (proj - minProj) / length;

                // Логика Skillion:
                // roof_direction указывает "вниз" (куда течет вода).
                // Значит, чем больше factor (ближе к направлению), тем ниже крыша.
                // factor 0 = самая высокая точка, factor 1 = самая низкая (min_height).

                v.y = min_height + (roofRise * (1.0f - factor));

                data.Vertices[i] = v;
            }
        }

        // 6. Пересчитываем нормали для корректного освещения наклонной поверхности
        RecalculateNormals(data, startIndex);
    }

    // Start is called before the first frame update
    public void GenerateRoofForObject(BaseDataObject dataobj, List<Vector3> corners, List<List<Vector3>> holesCorners, float minHeight, float height, Vector2 min, Vector2 size, BaseOsm geo, bool isUseOldTriangulation)
    {
        var roof = new GameObject("roof");

        roof.transform.SetParent(dataobj.transform);

        var mesh = roof.AddComponent<MeshFilter>().mesh;

        roof.AddComponent<MeshRenderer>();

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
        else if(geo.HasField("building:roof:orientation"))
        {
            roof_orientation = geo.GetValueStringByKey("building:roof:orientation");
        }

        var tb = new MeshData();

        if (roof_type == "flat") //fix
        {
            if(isUseOldTriangulation)
            {
                GR.CreateMeshWithHeightOld(corners, height, height + roof_height, tb);
            }
            else
            {
                GR.CreateMeshWithHeight(corners, height, height + roof_height, tb, holesCorners);
            }

        }
        else if (roof_type == "hipped")
        {
            if (!isRoofHeightExternalSet)
            {
                roof_height = 6.0f;
            }

            CreateHippedRoof(corners, height, height + roof_height, tb, min, size, roofangle);
        }
        else if (roof_type == "gabled")
        {
            if (!isRoofHeightExternalSet)
            {
                roof_height = 6.0f;
            }

            CreateGabledRoof(corners, height, height + roof_height, tb, min, size);
        }
        else if (roof_type == "gambrel")
        {
            if (!isRoofHeightExternalSet)
            {
                roof_height = 6.0f;
            }

            CreateGambrelRoof(corners, height, height + roof_height, tb, min, size);
        }
        else if (roof_type == "dome") //fix
        {
            if (!isRoofHeightExternalSet)
            {
                roof_height = Mathf.Min(size.x, size.y) / 2.0f;
            }

            CreateDomeRoof(corners, height, height + roof_height, tb, min, size);
        }
        else if (roof_type == "pyramidal")
        {
            if (!isRoofHeightExternalSet)
            {
                roof_height = 3.0f;
            }

            CreatePyramidalRoof(corners, height, height + roof_height, tb, min, size);
        }
        else if (roof_type == "onion")
        {
            if (!isRoofHeightExternalSet)
            {
                roof_height = 1.0f;
            }

            CreateOnionRoof(corners, height, height + roof_height, tb, min, size);
        }
        // --- NEW TYPES ADDED BELOW ---
        else if (roof_type == "skillion")
        {
            // Если высота не задана явно в тегах, ставим дефолтное значение.
            // Если задан roofangle, это значение будет пересчитано внутри функции.
            if (!isRoofHeightExternalSet) roof_height = 3.0f;

            CreateSkillionRoofActual(corners, holesCorners, height, height + roof_height, tb, roof_direction, roofangle);
        }
        else if (roof_type == "round")
        {
            // Для круглой крыши высота по умолчанию часто равна половине ширины, 
            // но если не задана, берем 3.0м для безопасности.
            if (!isRoofHeightExternalSet)
            {
                // Попытка рассчитать идеальную полусферу на основе геометрии
                // Но для стабильности пока оставим фикс. значение или используем логику ниже.
                roof_height = 3.0f;
            }

            CreateRoundRoofActual(corners, holesCorners, height, height + roof_height, tb);
        }
        else if (roof_type == "quadruple_saltbox")
        {
            if (!isRoofHeightExternalSet) roof_height = 4.0f;
            CreateQuadrupleSaltboxRoof(corners, height, height + roof_height, tb, roof_direction, size);
        }
        else if (roof_type == "saltbox")
        {
            // Saltbox обычно выше стандартной крыши из-за крутого ската
            if (!isRoofHeightExternalSet) roof_height = 4.5f;

            // Используем roof_direction для ориентации короткого ската
            CreateSaltboxRoof(corners, holesCorners, height, height + roof_height, tb, roof_direction, size);
        }
        else
        {
            Debug.Log("Try create roofs: " + roof_type);

            //Not supported, use flat
            if (isUseOldTriangulation)
            {
                GR.CreateMeshWithHeightOld(corners, height, height + roof_height, tb);
            }
            else
            {
                GR.CreateMeshWithHeight(corners, height, height + roof_height, tb, holesCorners);
            }
        }

        mesh.vertices = tb.Vertices.ToArray();
        mesh.triangles = tb.Indices.ToArray();
        mesh.SetUVs(0, tb.UV);

        roof.transform.localPosition = Vector3.zero;

        if (geo.HasField("roof:material"))
        {
            var mat_name = geo.GetValueStringByKey("roof:material");

            var mat_by_tag = buildingMaterials.GetBuildingMaterialByName(mat_name);

            if(mat_by_tag != null)
            {
                roof.GetComponent<MeshRenderer>().material = mat_by_tag;
            }
        }

        roof.GetComponent<MeshRenderer>().material.SetColor("_Color", GR.SetOSMRoofColour(geo));
    }

}
