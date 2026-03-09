using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Улучшенный генератор двускатной (gabled) крыши
/// </summary>
public class GabledRoofGenerator
{
    /// <summary>
    /// Создаёт двускатную крышу с правильной геометрией
    /// 
    /// Gabled крыша (двускатная):
    /// - Конёк (ridge) идёт вдоль длинной стороны здания
    /// - Два ската опускаются к противоположным сторонам
    /// - Торцы образуют вертикальные треугольники
    /// </summary>
    public static void CreateGabledRoof(
        List<Vector3> baseCorners,
        float baseHeight,
        float roofHeight,
        MeshData data,
        float angle = 0f,
        int directionAngle = -1,
        string orientation = null)
    {
        if (baseCorners == null || baseCorners.Count < 3) return;

        int cornerCount = baseCorners.Count;

        // ============================================
        // 1. Вычисляем bounding box и центр
        // ============================================
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        Vector3 center = Vector3.zero;

        for (int i = 0; i < cornerCount; i++)
        {
            Vector3 c = baseCorners[i];
            if (c.x < minX) minX = c.x;
            if (c.x > maxX) maxX = c.x;
            if (c.z < minZ) minZ = c.z;
            if (c.z > maxZ) maxZ = c.z;
            center += c;
        }
        center /= cornerCount;

        float sizeX = maxX - minX;
        float sizeZ = maxZ - minZ;

        // ============================================
        // 2. Определяем направление конька (ridge direction)
        // ============================================
        bool ridgeAlongX = DetermineRidgeDirection(directionAngle, orientation, sizeX, sizeZ);

        Vector3 ridgeDir = ridgeAlongX ? Vector3.right : Vector3.forward;
        Vector3 perpDir = Vector3.Cross(ridgeDir, Vector3.up).normalized;

        float ridgeLength = ridgeAlongX ? sizeX : sizeZ;
        float perpWidth = ridgeAlongX ? sizeZ : sizeX;

        // ============================================
        // 3. Вычисляем высоту крыши
        // ============================================
        float roofRise;
        if (angle > 0)
        {
            float run = perpWidth / 2.0f;
            float clampedAngle = Mathf.Clamp(angle, 5f, 85f);
            roofRise = run * Mathf.Tan(clampedAngle * Mathf.Deg2Rad);
        }
        else
        {
            roofRise = roofHeight - baseHeight;
        }

        float peakY = baseHeight + roofRise;

        // ============================================
        // 4. Находим точки конька (ridge endpoints)
        // ============================================
        // Конёк идёт от одного конца здания до другого по длинной стороне
        Vector3 ridgeCenter = center;
        ridgeCenter.y = peakY;

        Vector3 ridgeStart = ridgeCenter - (ridgeDir * (ridgeLength / 2.0f));
        Vector3 ridgeEnd = ridgeCenter + (ridgeDir * (ridgeLength / 2.0f));

        // ============================================
        // 5. Разделяем углы на две стороны относительно конька
        // ============================================
        List<int> sideA = new List<int>(); // Одна сторона (proj >= 0)
        List<int> sideB = new List<int>(); // Другая сторона (proj < 0)

        for (int i = 0; i < cornerCount; i++)
        {
            float proj = Vector3.Dot(baseCorners[i] - center, perpDir);
            if (proj >= 0)
                sideA.Add(i);
            else
                sideB.Add(i);
        }

        // ============================================
        // 6. Создаём вершины
        // ============================================
        int baseOffset = data.Vertices.Count;

        // Вершины основания (на высоте baseHeight)
        for (int i = 0; i < cornerCount; i++)
        {
            Vector3 v = baseCorners[i];
            data.Vertices.Add(new Vector3(v.x, baseHeight, v.z));
            data.UV.Add(new Vector2(v.x, v.z));
        }

        // Вершины конька
        int ridgeStartIndex = data.Vertices.Count;
        data.Vertices.Add(ridgeStart);
        data.UV.Add(new Vector2(ridgeStart.x, ridgeStart.z));

        int ridgeEndIndex = data.Vertices.Count;
        data.Vertices.Add(ridgeEnd);
        data.UV.Add(new Vector2(ridgeEnd.x, ridgeEnd.z));

        // ============================================
        // 7. Создаём треугольники скатов крыши
        // ============================================

        // Для каждого ребра основания определяем, к какому скату оно относится
        for (int i = 0; i < cornerCount; i++)
        {
            int next = (i + 1) % cornerCount;

            Vector3 v1 = baseCorners[i];
            Vector3 v2 = baseCorners[next];

            float proj1 = Vector3.Dot(v1 - center, perpDir);
            float proj2 = Vector3.Dot(v2 - center, perpDir);

            int baseIdx1 = baseOffset + i;
            int baseIdx2 = baseOffset + next;

            // Проверяем, пересекает ли ребро линию конька
            if ((proj1 >= 0 && proj2 >= 0) || (proj1 < 0 && proj2 < 0))
            {
                // Ребро целиком на одной стороне - создаём треугольник к соответствующему концу конька
                // Но нужно определить, какой конец конька ближе

                float projAlongRidge1 = Vector3.Dot(v1 - center, ridgeDir);
                float projAlongRidge2 = Vector3.Dot(v2 - center, ridgeDir);

                // Определяем к какому концу конька ближе
                // Используем среднюю точку ребра
                float avgProj = (projAlongRidge1 + projAlongRidge2) / 2.0f;
                int ridgeIdx = (avgProj < 0) ? ridgeStartIndex : ridgeEndIndex;

                // Создаём треугольник ската
                // Порядок вершин зависит от стороны (для правильных нормалей)
                if (proj1 >= 0)
                {
                    // Сторона A - нормали смотрят в положительном направлении perpDir
                    data.Indices.Add(baseIdx1);
                    data.Indices.Add(ridgeIdx);
                    data.Indices.Add(baseIdx2);
                }
                else
                {
                    // Сторона B - нормали смотрят в отрицательном направлении perpDir
                    data.Indices.Add(baseIdx1);
                    data.Indices.Add(baseIdx2);
                    data.Indices.Add(ridgeIdx);
                }
            }
            else
            {
                // Ребро пересекает линию конька - это торец крыши
                // Нужно создать два треугольника

                // Находим точку пересечения с линией конька
                float t = proj1 / (proj1 - proj2);
                Vector3 intersection = Vector3.Lerp(v1, v2, t);

                // Определяем, какой конец конька
                float projAlongRidge = Vector3.Dot(intersection - center, ridgeDir);
                int ridgeIdx = (projAlongRidge < 0) ? ridgeStartIndex : ridgeEndIndex;

                // Добавляем вершину пересечения на высоте конька
                int intersectionIdx = data.Vertices.Count;
                data.Vertices.Add(new Vector3(intersection.x, peakY, intersection.z));
                data.UV.Add(new Vector2(intersection.x, intersection.z));

                // Создаём два треугольника торца
                if (proj1 >= 0)
                {
                    // v1 на стороне A, v2 на стороне B
                    data.Indices.Add(baseIdx1);
                    data.Indices.Add(intersectionIdx);
                    data.Indices.Add(baseIdx2);

                    data.Indices.Add(intersectionIdx);
                    data.Indices.Add(ridgeIdx);
                    data.Indices.Add(baseIdx2);
                }
                else
                {
                    // v1 на стороне B, v2 на стороне A
                    data.Indices.Add(baseIdx1);
                    data.Indices.Add(baseIdx2);
                    data.Indices.Add(intersectionIdx);

                    data.Indices.Add(intersectionIdx);
                    data.Indices.Add(baseIdx2);
                    data.Indices.Add(ridgeIdx);
                }
            }
        }

        // ============================================
        // 8. Создаём треугольники торцов (gable ends)
        // ============================================
        CreateGableEnds(data, baseCorners, baseOffset, ridgeStartIndex, ridgeEndIndex,
                        ridgeStart, ridgeEnd, baseHeight, center, ridgeDir);

        // ============================================
        // 9. Пересчитываем нормали
        // ============================================
        RecalculateNormals(data, baseOffset);
    }

    /// <summary>
    /// Определяет направление конька на основе параметров
    /// </summary>
    private static bool DetermineRidgeDirection(int directionAngle, string orientation, float sizeX, float sizeZ)
    {
        bool ridgeAlongX;

        if (directionAngle >= 0)
        {
            // Угол направления: 0 = N (вдоль Z), 90 = E (вдоль X)
            float normalized = directionAngle % 180;
            // Конёк перпендикулярен направлению ската
            ridgeAlongX = !(normalized > 45 && normalized < 135);
        }
        else if (!string.IsNullOrEmpty(orientation))
        {
            bool isLongX = sizeX > sizeZ;
            // "along" = конёк вдоль длинной стороны
            ridgeAlongX = (orientation == "along") ? isLongX : !isLongX;
        }
        else
        {
            // По умолчанию - конёк вдоль длинной стороны
            ridgeAlongX = sizeX >= sizeZ;
        }

        return ridgeAlongX;
    }

    /// <summary>
    /// Создаёт треугольники торцов (вертикальные треугольники под коньком)
    /// </summary>
    private static void CreateGableEnds(
        MeshData data,
        List<Vector3> baseCorners,
        int baseOffset,
        int ridgeStartIndex,
        int ridgeEndIndex,
        Vector3 ridgeStart,
        Vector3 ridgeEnd,
        float baseHeight,
        Vector3 center,
        Vector3 ridgeDir)
    {
        int cornerCount = baseCorners.Count;

        // Находим углы, ближайшие к каждому концу конька
        int nearestToStartIdx = -1;
        int nearestToEndIdx = -1;
        float minDistToStart = float.MaxValue;
        float minDistToEnd = float.MaxValue;

        for (int i = 0; i < cornerCount; i++)
        {
            float proj = Vector3.Dot(baseCorners[i] - center, ridgeDir);
            float distToStart = Mathf.Abs(proj + (ridgeEnd - ridgeStart).magnitude / 2.0f);
            float distToEnd = Mathf.Abs(proj - (ridgeEnd - ridgeStart).magnitude / 2.0f);

            if (distToStart < minDistToStart)
            {
                minDistToStart = distToStart;
                nearestToStartIdx = i;
            }
            if (distToEnd < minDistToEnd)
            {
                minDistToEnd = distToEnd;
                nearestToEndIdx = i;
            }
        }

        // Создаём вершины торцов на высоте конька (для треугольников)
        // Торец в начале конька
        if (nearestToStartIdx >= 0)
        {
            int prevIdx = (nearestToStartIdx - 1 + cornerCount) % cornerCount;
            int nextIdx = (nearestToStartIdx + 1) % cornerCount;

            // Создаём треугольник торца если нужно
            Vector3 baseV = baseCorners[nearestToStartIdx];

            // Проверяем, есть ли соседние вершины рядом
            // Если да - создаём треугольник торца
            float distPrev = Vector3.Distance(baseCorners[prevIdx], baseV);
            float distNext = Vector3.Distance(baseCorners[nextIdx], baseV);

            // Торец - треугольник от основания до конька
            // Это уже создано в основном цикле при пересечении ребра
        }
    }

    /// <summary>
    /// Пересчитывает нормали для добавленной геометрии
    /// </summary>
    private static void RecalculateNormals(MeshData data, int startIndex)
    {
        int vertexCount = data.Vertices.Count;

        // Инициализируем нормали
        for (int i = startIndex; i < vertexCount; i++)
        {
            if (i < data.Normals.Count)
                data.Normals[i] = Vector3.zero;
            else
                data.Normals.Add(Vector3.zero);
        }

        // Суммируем нормали от каждого треугольника
        int indexCount = data.Indices.Count;
        for (int i = 0; i < indexCount; i += 3)
        {
            int i1 = data.Indices[i];
            int i2 = data.Indices[i + 1];
            int i3 = data.Indices[i + 2];

            if (i1 >= startIndex && i1 < vertexCount &&
                i2 >= startIndex && i2 < vertexCount &&
                i3 >= startIndex && i3 < vertexCount)
            {
                Vector3 v1 = data.Vertices[i1];
                Vector3 v2 = data.Vertices[i2];
                Vector3 v3 = data.Vertices[i3];

                Vector3 normal = Vector3.Cross(v2 - v1, v3 - v1).normalized;

                data.Normals[i1] += normal;
                data.Normals[i2] += normal;
                data.Normals[i3] += normal;
            }
        }

        // Нормализуем
        for (int i = startIndex; i < vertexCount; i++)
        {
            if (i < data.Normals.Count)
                data.Normals[i] = data.Normals[i].normalized;
        }
    }
}
