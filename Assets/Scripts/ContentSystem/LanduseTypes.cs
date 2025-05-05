using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "LanduseTypes", menuName = "GeoRender/LanduseTypes", order = 1)]

public class LanduseTypes : ScriptableObject
{
    // Start is called before the first frame update
    [System.Serializable]
    public struct LanduseTypesItem
    {
        [SerializeField]
        public string landuseType;
        [SerializeField]
        public string landuseDescription;
        [SerializeField]
        public Material groundMaterial;
        [SerializeField]
        public bool isGrassGenerate;
        [SerializeField]
        public bool isTreesGenerate;
        [SerializeField]
        public bool isRenderEnable;
        [SerializeField]
        public bool isFlatUV;
    }

    [SerializeField]
    List<LanduseTypesItem> LanduseTypesReplacesList;

    public bool CheckForFoundLanduseTypeInfo(string curLanduseTypeName)
    {
        int countitems = LanduseTypesReplacesList.Count;

        LanduseTypesReplacesList.Sort((p1, p2) => p1.landuseType.CompareTo(p2.landuseType));

        bool isFound = false;

        for (int i = 0; i < countitems; i++)
        {
            LanduseTypesItem item = LanduseTypesReplacesList[i];

            if (item.landuseType.Equals(curLanduseTypeName))
            {
                isFound = true;
                break;
            }
        }

        return isFound;
    }

    public void AddNewLanduseTypeInfo(string curLanduseTypeName)
    {
        LanduseTypesItem item = new LanduseTypesItem();
        item.landuseType = curLanduseTypeName;

        item.landuseDescription = null;
        item.groundMaterial = null;
        item.isGrassGenerate = false;
        item.isTreesGenerate = false;
        item.isRenderEnable = false;
        item.isFlatUV = false;

        LanduseTypesReplacesList.Add(item);

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public LanduseTypesItem GetLanduseTypeInfoByName(string curLanduseTypeName)
    {
        int countitems = LanduseTypesReplacesList.Count;

        for (int i = 0; i < countitems; i++)
        {
            LanduseTypesItem item = LanduseTypesReplacesList[i];

            if (item.landuseType.Equals(curLanduseTypeName))
            {
                return item;
            }
        }

        AddNewLanduseTypeInfo(curLanduseTypeName);

        int lastIndex = LanduseTypesReplacesList.Count - 1;

        return LanduseTypesReplacesList[lastIndex];
    }
}
