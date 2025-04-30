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

    public void DeleteUnused()
    {
        ColorsReplaceList.RemoveAll(item => item.color == Color.white);
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
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
        if (string.IsNullOrEmpty(curColorName))
        {
            return Color.white;
        }

        // Разделяем строку на две части по первому вхождению ';'
        string[] parts = curColorName.Split(new[] { ';' }, 2);

        // Берем первую часть и удаляем пробелы по краям
        string colorName = parts[0].Trim();

        int countitems = ColorsReplaceList.Count;

        for (int i = 0; i < countitems; i++)
        {
            ColorReplace item = ColorsReplaceList[i];

            if (item.colorname.Equals(colorName))
            {
                return item.color;
            }
        }

        AddNewColorName(colorName);

        return Color.white;
    }
}
