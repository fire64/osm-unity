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