using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct Item
{
    public string key;
    public string value;
}

public static class GR
{
    public static void CreateMeshWithHeight(List<Vector3> corners, float min_height, float height, MeshData data, Vector2 min, Vector2 size)
    {
        // Create bottom face
        for (int i = 0; i < corners.Count; i++)
        {
            data.Vertices.Add(corners[i] + new Vector3(0, min_height, 0));
            data.Normals.Add(-Vector3.forward);
        }
        for (int i = 2; i < corners.Count; i++)
        {
            data.Indices.Add(0);
            data.Indices.Add(i - 1);
            data.Indices.Add(i);
        }

        for (int i = 1; i < corners.Count; i++)
        {
            Vector3 p1 = corners[i - 1];
            Vector3 p2 = corners[i];

            Vector3 v1 = p1 + new Vector3(0, min_height, 0);
            Vector3 v2 = p2 + new Vector3(0, min_height, 0);
            Vector3 v3 = p1 + new Vector3(0, min_height + height, 0);
            Vector3 v4 = p2 + new Vector3(0, min_height + height, 0);

            data.Vertices.Add(v3);
            data.Vertices.Add(v4);

            data.Normals.Add(-Vector3.forward);
            data.Normals.Add(-Vector3.forward);

            // index values
            int idx1 = i - 1;
            int idx2 = i;
            int idx3 = data.Vertices.Count - 2;
            int idx4 = data.Vertices.Count - 1;

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

        // Create top face
        int topOffset = data.Vertices.Count;
        for (int i = 0; i < corners.Count; i++)
        {
            data.Vertices.Add(corners[i] + new Vector3(0, min_height + height, 0));
            data.Normals.Add(-Vector3.forward);
        }

        for (int i = 2; i < corners.Count; i++)
        {
            data.Indices.Add(topOffset + 0);
            data.Indices.Add(topOffset + i);
            data.Indices.Add(topOffset + i - 1); // Обратный порядок индексов для верхней грани
        }

        for (int i = 2; i < corners.Count; i++) //fix for backfaces
        {
            data.Indices.Add(topOffset + i - 1); // Обратный порядок индексов для верхней грани
            data.Indices.Add(topOffset + i);
            data.Indices.Add(topOffset + 0);
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
            return new Color(1.0f, 1.0f, 1.0f, 1.0f);
        }
    }
}