using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "ColorByName", menuName = "GeoRender/ColorByName", order = 1)]

public class ColorByName : ScriptableObject
{
    // Start is called before the first frame update
    [System.Serializable]
    public struct ColorReplace
    {
        [SerializeField]
        public string colorname;
        [SerializeField]
        public Color color;
    }

    [SerializeField]
    List<ColorReplace> ColorsReplaceList;

    public bool CheckForFoundColorName(string curColorName)
    {
        int countitems = ColorsReplaceList.Count;

        ColorsReplaceList.Sort((p1, p2) => p1.colorname.CompareTo(p2.colorname));

        bool isFound = false;

        for (int i = 0; i < countitems; i++)
        {
            ColorReplace item = ColorsReplaceList[i];

            if (item.colorname.Equals(curColorName))
            {
                isFound = true;
                break;
            }
        }

        return isFound;
    }

    public void AddNewColorName(string curColorName)
    {
        ColorReplace item = new ColorReplace();
        item.colorname = curColorName;
        item.color = Color.white;
        ColorsReplaceList.Add(item);

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }
    public Color GetColorByName(string curColorName)
    {
        int countitems = ColorsReplaceList.Count;

        for (int i = 0; i < countitems; i++)
        {
            ColorReplace item = ColorsReplaceList[i];

            if (item.colorname.Equals(curColorName))
            {
                return item.color;
            }
        }

        AddNewColorName(curColorName);

        return Color.white;
    }
}
