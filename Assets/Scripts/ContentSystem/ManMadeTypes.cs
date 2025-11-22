using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "ManMadeTypes", menuName = "GeoRender/ManMadeTypes", order = 1)]

public class ManMadeTypes : ScriptableObject
{
    // Start is called before the first frame update
    [System.Serializable]
    public struct TypeItem
    {
        [SerializeField]
        public string type;
        [SerializeField]
        public string defaultDescription;
        [SerializeField]
        public Material defaultMaterial;
        [SerializeField]
        public float defaultHeight;
    }

    [SerializeField]
    List<TypeItem> TypeInfoReplacesList;

    public bool CheckForFoundTypeInfo(string curTypeName)
    {
        int countitems = TypeInfoReplacesList.Count;

        TypeInfoReplacesList.Sort((p1, p2) => p1.type.CompareTo(p2.type));

        bool isFound = false;

        for (int i = 0; i < countitems; i++)
        {
            TypeItem item = TypeInfoReplacesList[i];

            if (item.type.Equals(curTypeName))
            {
                isFound = true;
                break;
            }
        }

        return isFound;
    }

    public void AddNewTypeInfo(string curTypeName)
    {
        TypeItem item = new TypeItem();
        item.type = curTypeName;
        item.defaultDescription = null;
        item.defaultMaterial = null;
        item.defaultHeight = 1.5f;
        TypeInfoReplacesList.Add(item);

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public TypeItem GetTypeInfoByName(string curTypeName)
    {
        int countitems = TypeInfoReplacesList.Count;

        for (int i = 0; i < countitems; i++)
        {
            TypeItem item = TypeInfoReplacesList[i];

            if (item.type.Equals(curTypeName))
            {
                return item;
            }
        }

        AddNewTypeInfo(curTypeName);

        int lastIndex = TypeInfoReplacesList.Count - 1;

        return TypeInfoReplacesList[lastIndex];
    }

    public void DeleteUnused()
    {
        TypeInfoReplacesList.RemoveAll(item => item.defaultMaterial == null);
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }
}
