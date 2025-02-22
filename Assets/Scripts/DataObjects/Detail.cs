using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Detail : MonoBehaviour
{
    public string Id;
    public string Name;
    public string Kind;
    public string Source;

    public string Description;
    public string Type;

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
}
