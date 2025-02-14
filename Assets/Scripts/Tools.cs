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

    public static void CreateMeshLineWithWidth(List<Vector3> corners, float width, MeshData data)
    {
        for (int i = 1; i < corners.Count; i++)
        {
            Vector3 s1 = corners[i - 1];
            Vector3 s2 = corners[i];

            Vector3 diff = (s2 - s1).normalized;
            var cross = Vector3.Cross(diff, Vector3.up) * width; //width of lane

            Vector3 v1 = s1 + cross;
            Vector3 v2 = s1 - cross;
            Vector3 v3 = s2 + cross;
            Vector3 v4 = s2 - cross;

            data.Vertices.Add(v1);
            data.Vertices.Add(v2);
            data.Vertices.Add(v3);
            data.Vertices.Add(v4);

            data.UV.Add(new Vector2(0, 0));
            data.UV.Add(new Vector2(1, 0));
            data.UV.Add(new Vector3(0, 1));
            data.UV.Add(new Vector3(1, 1));

            data.Normals.Add(-Vector3.up);
            data.Normals.Add(-Vector3.up);
            data.Normals.Add(-Vector3.up);
            data.Normals.Add(-Vector3.up);

            // index values
            int idx1, idx2, idx3, idx4;
            idx4 = data.Vertices.Count - 1;
            idx3 = data.Vertices.Count - 2;
            idx2 = data.Vertices.Count - 3;
            idx1 = data.Vertices.Count - 4;

            // first triangle v1, v3, v2
            data.Indices.Add(idx1);
            data.Indices.Add(idx3);
            data.Indices.Add(idx2);

            // second triangle v3, v4, v2
            data.Indices.Add(idx3);
            data.Indices.Add(idx4);
            data.Indices.Add(idx2);

            // third triangle v2, v3, v1
            data.Indices.Add(idx2);
            data.Indices.Add(idx3);
            data.Indices.Add(idx1);

            // fourth triangle v2, v4, v3
            data.Indices.Add(idx2);
            data.Indices.Add(idx4);
            data.Indices.Add(idx3);
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
            Vector3 v3 = p1 + new Vector3(0, min_height + height, 0);
            Vector3 v4 = p2 + new Vector3(0, min_height + height, 0);

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
            data.Vertices.Add(corners[i] + new Vector3(0, min_height + height, 0));
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