using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BaseDataObject : MonoBehaviour
{
    public string Id;
    public string Name;
    public string Kind;
    public string Source;

    public float height;
    public float min_height;
    public float width;

    public string material;

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
}
