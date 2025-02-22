using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

public class BaseOsm
{
    public ulong ID { get; set; }

    public bool Visible { get; set; }

    public List<ulong> NodeIDs { get; set; }

    public bool IsClosedPolygon { get; set; }

    public enum ObjectType
    {
        Undefined = 0,
        Building,
        Road,
        Landuse,
        Water,
        Barrier,
        Detail,
        Other
    };

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
        else if (key == "man_made" && IsClosedPolygon == true)
        {
            objectType = ObjectType.Building;
        }
        else if (key == "highway")
        {
            objectType = ObjectType.Road;
        }
        else if (key == "barrier")
        {
            objectType = ObjectType.Barrier;
        }
        else if (key == "water" || (key == "natural" && value == "water") || (key == "leisure" && value == "swimming_pool"))
        {
            objectType = ObjectType.Water;
        }
        else if (key == "leisure")
        {
            if (value == "park")
            {
                objectType = ObjectType.Landuse;
            }
        }
        else if (key == "natural" && value != "water")
        {
            objectType = ObjectType.Landuse;
        }
        else if (key == "landuse")
        {
            if (value == "grass")
            {
                objectType = ObjectType.Landuse;
            }
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

    public float GetValueFloatByKey(string sKey, float vDefault = 0.0f)
    {
        string res = GetValueStringByKey(sKey);

        if (res == null)
        {
            return vDefault;
        }

        res = res.Replace(".", ",");

        float result = vDefault;

        float.TryParse(res, out result);

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
        string res = GetValueStringByKey(sKey);

        if (res == null)
        {
            return vDefault;
        }

        int result = vDefault;

        int.TryParse(res, out result);

        return result;
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
        
        string strValue = attributes[attrName].Value;

        strValue = strValue.Replace(".", ",");

        return (T)Convert.ChangeType(strValue, typeof(T));
    }
}