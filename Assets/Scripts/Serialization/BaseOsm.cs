using System;
using System.Xml;

public class BaseOsm
{
    public Item[] itemlist;

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
            return 0.0f;
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
            return 0.0f;
        }

        res = res.Replace(".", ",");

        double result = vDefault;

        double.TryParse(res, out result);

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