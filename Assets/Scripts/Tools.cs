using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public struct Item
{
    public string key;
    public string value;
}

public static class GR
{
    public static Vector3 getHeightPosition(Vector3 point)
    {
        point.y = 10000;
        RaycastHit downHit;
        if (Physics.Raycast(point, Vector3.down, out downHit, 10000f))
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

        //Reverse
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

        if(isReverse)
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

    public static void CreateMeshLineWithWidthAndHeight(List<Vector3> corners, float height, float width, MeshData data)
    {
        if (corners == null || corners.Count < 2) return;

        bool isClosed = IsClosed(corners);
        List<Vector3> offsets = new List<Vector3>();

        // Предварительно вычисляем смещения для каждой точки контура
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

            // Корректировка направления для начальных/конечных точек
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

            Vector3 sul = sl + Vector3.up * height;
            Vector3 sur = sr + Vector3.up * height;
            Vector3 eul = el + Vector3.up * height;
            Vector3 eur = er + Vector3.up * height;

            // Добавление боковых граней с плавными нормалями
            AddSmoothQuad(data, sl, el, sul, eul, leftOffsetStart.normalized, leftOffsetEnd.normalized, true); // Левая сторона
            AddSmoothQuad(data, sr, er, sur, eur, rightOffsetStart.normalized, rightOffsetEnd.normalized, false); // Правая сторона

            // Верхняя часть
            AddQuad(data, sul, eul, sur, eur, Vector3.up);

            // Нижняя часть
            AddQuad(data, el, sl, er, sr, Vector3.down);

            // Торцы
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

        if(isReverse)
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
        if (corners.Count < 2)
            return;

        List<Vector3> leftPoints = new List<Vector3>();
        List<Vector3> rightPoints = new List<Vector3>();

        // Генерируем точки слева и справа с учётом соседних сегментов
        for (int i = 0; i < corners.Count; i++)
        {
            Vector3 dirPrev, dirNext;
            Vector3 cross;

            if (i == 0)
            {
                // Первая точка: используем следующий сегмент
                dirNext = (corners[i + 1] - corners[i]).normalized;
                cross = Vector3.Cross(dirNext, Vector3.up) * width;
            }
            else if (i == corners.Count - 1)
            {
                // Последняя точка: используем предыдущий сегмент
                dirPrev = (corners[i] - corners[i - 1]).normalized;
                cross = Vector3.Cross(dirPrev, Vector3.up) * width;
            }
            else
            {
                // Внутренние точки: вычисляем биссектрису направлений
                dirPrev = (corners[i] - corners[i - 1]).normalized;
                dirNext = (corners[i + 1] - corners[i]).normalized;

                Vector3 bisectorDir = dirPrev + dirNext;
                if (bisectorDir.magnitude < 0.001f)
                {
                    // Направления противоположны - используем перпендикуляр к dirPrev
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

        // Добавляем вершины, UV и нормали
        for (int i = 0; i < corners.Count; i++)
        {
            data.Vertices.Add(leftPoints[i]);
            data.Vertices.Add(rightPoints[i]);

            // UV: растягиваем текстуру вдоль линии
            float uvProgress = i / (float)(corners.Count - 1);
            data.UV.Add(new Vector2(uvProgress, 0f));
            data.UV.Add(new Vector2(uvProgress, 1f));

            data.Normals.Add(-Vector3.up);
            data.Normals.Add(-Vector3.up);
        }

        // Создаём треугольники для полосы
        for (int i = 0; i < corners.Count - 1; i++)
        {
            int leftCurrent = i * 2;
            int rightCurrent = i * 2 + 1;
            int leftNext = (i + 1) * 2;
            int rightNext = (i + 1) * 2 + 1;

            // Первый треугольник: leftCurrent, leftNext, rightCurrent
            data.Indices.Add(leftCurrent);
            data.Indices.Add(leftNext);
            data.Indices.Add(rightCurrent);

            // Второй треугольник: rightCurrent, leftNext, rightNext
            data.Indices.Add(rightCurrent);
            data.Indices.Add(leftNext);
            data.Indices.Add(rightNext);
        }
    }

    public static void CreateMeshPlane(List<Vector3> corners, MeshData data)
    {
        if (IsClockwise(corners))
        {
            corners.Reverse();
        }

        // Рассчитываем границы для проекции UV
        float minX = corners.Min(c => c.x);
        float maxX = corners.Max(c => c.x);
        float minZ = corners.Min(c => c.z);
        float maxZ = corners.Max(c => c.z);

        // Избегаем деления на ноль
        if (Mathf.Approximately(maxX, minX)) maxX = minX + 1e-6f;
        if (Mathf.Approximately(maxZ, minZ)) maxZ = minZ + 1e-6f;

        // Создаем верхнюю грань
        int topOffset = data.Vertices.Count;
        for (int i = 0; i < corners.Count; i++)
        {
            data.Vertices.Add(corners[i] + new Vector3(0, 0.0001f, 0));
            data.Normals.Add(Vector3.up); // Нормаль вверх для верхней грани

            // UV аналогично нижней грани
            float u = (corners[i].x - minX) / (maxX - minX);
            float v = (corners[i].z - minZ) / (maxZ - minZ);
            data.UV.Add(new Vector2(u, v));
        }

        // Триангуляция верхней грани
        for (int i = 2; i < corners.Count; i++)
        {
            data.Indices.Add(topOffset + 0);
            data.Indices.Add(topOffset + i);
            data.Indices.Add(topOffset + i - 1);
        }
    }

    public static void CreateMeshWithHeight(List<Vector3> corners, float min_height, float height, MeshData data)
    {
        if(IsClockwise(corners))
        {
            corners.Reverse();
        }

        // Рассчитываем границы для проекции UV
        float minX = corners.Min(c => c.x);
        float maxX = corners.Max(c => c.x);
        float minZ = corners.Min(c => c.z);
        float maxZ = corners.Max(c => c.z);

        // Избегаем деления на ноль
        if (Mathf.Approximately(maxX, minX)) maxX = minX + 1e-6f;
        if (Mathf.Approximately(maxZ, minZ)) maxZ = minZ + 1e-6f;

        // Создаем нижнюю грань
        for (int i = 0; i < corners.Count; i++)
        {
            data.Vertices.Add(corners[i] + new Vector3(0, min_height, 0));
            data.Normals.Add(Vector3.down); // Нормаль вниз для нижней грани

            // UV: проекция XZ на текстурные координаты
            float u = (corners[i].x - minX) / (maxX - minX);
            float v = (corners[i].z - minZ) / (maxZ - minZ);
            data.UV.Add(new Vector2(u, v));
        }
        for (int i = 2; i < corners.Count; i++)
        {
            data.Indices.Add(0);
            data.Indices.Add(i - 1);
            data.Indices.Add(i);
        }

        // Создаем боковые грани
        for (int i = 1; i < corners.Count; i++)
        {
            Vector3 p1 = corners[i - 1];
            Vector3 p2 = corners[i];

            // Вершины для боковой грани
            Vector3 v1 = p1 + new Vector3(0, min_height, 0);
            Vector3 v2 = p2 + new Vector3(0, min_height, 0);
            Vector3 v3 = p1 + new Vector3(0, height, 0);
            Vector3 v4 = p2 + new Vector3(0, height, 0);

            // Добавляем вершины и UV
            int startIndex = data.Vertices.Count;
            data.Vertices.Add(v1);
            data.Vertices.Add(v2);
            data.Vertices.Add(v3);
            data.Vertices.Add(v4);

            // Нормаль для боковой грани (перпендикуляр к направлению стороны)
            Vector3 sideDir = (p2 - p1).normalized;
            Vector3 normal = new Vector3(-sideDir.z, 0, sideDir.x).normalized;
            for (int j = 0; j < 4; j++) data.Normals.Add(normal);

            // UV: U - вдоль стороны, V - высота
            data.UV.Add(new Vector2(0, 0)); // v1 низ
            data.UV.Add(new Vector2(1, 0)); // v2 низ
            data.UV.Add(new Vector2(0, 1)); // v3 верх
            data.UV.Add(new Vector2(1, 1)); // v4 верх

            // Треугольники для квада
            data.Indices.Add(startIndex);
            data.Indices.Add(startIndex + 2);
            data.Indices.Add(startIndex + 1);

            data.Indices.Add(startIndex + 1);
            data.Indices.Add(startIndex + 2);
            data.Indices.Add(startIndex + 3);
        }

        // Создаем верхнюю грань
        int topOffset = data.Vertices.Count;
        for (int i = 0; i < corners.Count; i++)
        {
            data.Vertices.Add(corners[i] + new Vector3(0, height, 0));
            data.Normals.Add(Vector3.up); // Нормаль вверх для верхней грани

            // UV аналогично нижней грани
            float u = (corners[i].x - minX) / (maxX - minX);
            float v = (corners[i].z - minZ) / (maxZ - minZ);
            data.UV.Add(new Vector2(u, v));
        }

        // Триангуляция верхней грани
        for (int i = 2; i < corners.Count; i++)
        {
            data.Indices.Add(topOffset + 0);
            data.Indices.Add(topOffset + i);
            data.Indices.Add(topOffset + i - 1);
        }
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
            hex = hex.Replace("0x", "");//in case the string is formatted 0xFFFFFF
            hex = hex.Replace("#", "");//in case the string is formatted #FFFFFF
            byte a = 255;//assume fully visible unless specified in hex

            byte r = 255;
            byte g = 255;
            byte b = 255;

            try
            {
                //  Block of code to try
                r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);

                //Only use alpha if the string has enough characters
                if (hex.Length == 8)
                {
                    a = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                }
            }
            catch (Exception e)
            {
                //  Block of code to handle errors
                Debug.LogError( "Exeption:" + e.Message);

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