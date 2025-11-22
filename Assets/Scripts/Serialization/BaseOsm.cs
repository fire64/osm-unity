using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml;
using Unity.VisualScripting;
using UnityEngine.Rendering.LookDev;

public enum OSMTypes
{
    Node,
    Way,
    Relation
}

public class BaseOsm
{
    public ulong ID { get; set; }

    //Temporary, may be changed to membersinfo_t variant???
    public List<ulong> NodeIDs { get; set; }

    public List<List<ulong>> HolesNodeListsIDs { get; set; }

    //current only for relations, leter global use...
    public struct membersinfo_t
    {
        public string role;
        public List<ulong> NodeIDs { get; set; }

        public Item[] itemlist;

        public bool isClosed;
    };

    public List<membersinfo_t> membersinfo { get; set; }
    public List<membersinfo_t> outerploygons { get; set; }
    public List<membersinfo_t> innerploygons { get; set; }

    public bool IsClosedPolygon { get; set; }

    public enum ObjectType
    {
        Undefined = 0,
        Building,
        ManMade,
        Road,
        Route,
        Landuse,
        Water,
        Barrier,
        Detail,
        Other
    };

    public bool IsBarrier { get; set; }

    public ObjectType objectType { get; set; }

    public Item[] itemlist;

    public void DetectObjectType(XmlNode tag)
    {
        string key = GetAttribute<string>("k", tag.Attributes);
        string value = GetAttribute<string>("v", tag.Attributes);

        if (key == "building" || key == "building:part" || key == "building:levels" || key == "building:min_level")
        {
            objectType = ObjectType.Building;
        }
        else if (key == "man_made")
        {
            objectType = ObjectType.ManMade;
        }
        else if (key == "highway" || key == "railway" || key == "area:highway")
        {
            objectType = ObjectType.Road;
        }
        else if (key == "water" || key == "waterway" || (key == "natural" && value == "water") || (key == "leisure" && value == "swimming_pool"))
        {
            objectType = ObjectType.Water;
        }
        else if (key == "natural" && value != "water")
        {
            objectType = ObjectType.Landuse;
        }
        else if (key == "landuse" || key == "leisure" || key == "amenity" || key == "boundary" || key == "fire_boundary")
        {
            objectType = ObjectType.Landuse;
        }
        else if (key == "route")
        {
            objectType = ObjectType.Route;
        }
        else if (key == "barrier")
        {
            objectType = ObjectType.Barrier;
        }

        if (key == "barrier")
        {
            IsBarrier = true;
        }
    }

    public bool HasField(string sKey)
    {
        foreach (Item item in itemlist)
        {
            if (item.key == sKey)
            {
                return true;
            }
        }
        return false;
    }

    public void AddField(string sKey, string sValue, bool bReplaceIfFound = true)
    {
        // Check if the key already exists in the item list
        for (int i = 0; i < itemlist.Length; i++)
        {
            if (itemlist[i].key == sKey)
            {
                // If the key is found and bReplaceIfFound is true, replace the value
                if (bReplaceIfFound)
                {
                    itemlist[i].value = sValue;
                    return;
                }
                else
                {
                    // If bReplaceIfFound is false, do nothing and return
                    return;
                }
            }
        }

        // If the key is not found, add a new item to the list
        Item newItem = new Item { key = sKey, value = sValue };
        itemlist = itemlist.Concat(new[] { newItem }).ToArray();
    }

    public string GetValueStringByKey(string sKey)
    {
        foreach (Item item in itemlist)
        {
            if (item.key == sKey)
            {
                return item.value;
            }
        }

        return null;
    }

    public string GetValueStringByKey(string sKey, string sDefault)
    {
        foreach (Item item in itemlist)
        {
            if (item.key == sKey)
            {
                return item.value;
            }
        }

        return sDefault;
    }

    public float GetValueFloatByKey(string sKey, float vDefault = 0.0f)
    {
        string res = GetValueStringByKey(sKey);

        if (res == null)
        {
            return vDefault;
        }

        res = res.Replace(" ", "");
        res = res.Replace("m.", "");
        res = res.Replace("m", "");
        res = res.Replace(".", ",");

        float result = vDefault;

        bool isOk = float.TryParse(res, out result);

        if (!isOk)
        {
       //     UnityEngine.Debug.LogError("Can't float parse from: \"" + res + "\"");
        }

        return result;
    }

    public double GetValueDoubleByKey(string sKey, double vDefault = 0.0f)
    {
        string res = GetValueStringByKey(sKey);

        if (res == null)
        {
            return vDefault;
        }

        res = res.Replace(".", ",");

        double result = vDefault;

        double.TryParse(res, out result);

        return result;
    }

    public int GetValueIntByKey(string sKey, int vDefault = 0)
    {
        if (!HasField(sKey))
            return vDefault;

        string value = GetValueStringByKey(sKey);

        if (int.TryParse(value, out int result))
        {
            return result;
        }

        // ƒополнительна€ обработка дл€ текстовых значений
        switch (value.ToLower())
        {
            case "yes":
            case "true":
                return 1;
            case "no":
            case "false":
                return 0;
            default:
                return vDefault;
        }
    }

    /** 
    **GetAttributes function 
    * gets the attributes of type string in the osm xml(txt) file
    * and converts and returns the type specified by the function call 
    * 'protected' makes this function available to child classes
    */
    protected T GetAttribute<T>(string attrName, XmlAttributeCollection attributes)
    {
        // TODO: We are going to assume 'attrName' exists in the collection

        string strValue = "True";

        try
        {
            //  Block of code to try
            strValue = attributes[attrName].Value;
        }
        catch (Exception e)
        {
            //  Block of code to handle errors
            UnityEngine.Debug.Log("Error parsing atr: " + attrName + " error: " + e.Message);
        }

        strValue = strValue.Replace(".", ",");

        return (T)Convert.ChangeType(strValue, typeof(T));
    }
}