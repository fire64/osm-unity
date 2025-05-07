using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "BuildingTypes", menuName = "GeoRender/BuildingTypes", order = 1)]

public class BuildingTypes : ScriptableObject
{
    // Start is called before the first frame update
    [System.Serializable]
    public struct BuildingTypeItem
    {
        [SerializeField]
        public string buildingtype;
        [SerializeField]
        public string buildingDescription;
        [SerializeField]
        public Material buildingMaterial;
        [SerializeField]
        public float defaultHeight;
        [SerializeField]
        public bool isUseDoors;
        [SerializeField]
        public bool isUseWindows;
        [SerializeField]
        public bool isUseRoofs;
        [SerializeField]
        public string defaultRoofShape;
        [SerializeField]
        public float defaultRoofHeight;
    }

    [SerializeField]
    List<BuildingTypeItem> BuildingTypeInfoReplacesList;

    public bool CheckForFoundBuildingTypeInfo(string curBuildingTypeName)
    {
        int countitems = BuildingTypeInfoReplacesList.Count;

        BuildingTypeInfoReplacesList.Sort((p1, p2) => p1.buildingtype.CompareTo(p2.buildingtype));

        bool isFound = false;

        for (int i = 0; i < countitems; i++)
        {
            BuildingTypeItem item = BuildingTypeInfoReplacesList[i];

            if (item.buildingtype.Equals(curBuildingTypeName))
            {
                isFound = true;
                break;
            }
        }

        return isFound;
    }

    public void AddNewBuildingTypeInfo(string curBuildingTypeName)
    {
        BuildingTypeItem item = new BuildingTypeItem();
        item.buildingtype = curBuildingTypeName;
        item.buildingDescription = null;
        item.buildingMaterial = null;
        item.defaultHeight = 0.0f; //not use default 2.0f for for these, is not checked
        item.isUseDoors = true;
        item.isUseWindows = true;
        item.isUseRoofs = true;
        item.defaultRoofShape = "flat";
        item.defaultRoofHeight = 0.0f;
        BuildingTypeInfoReplacesList.Add(item);

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public BuildingTypeItem GetBuildingTypeInfoByName(string curBuildingTypeName)
    {
        int countitems = BuildingTypeInfoReplacesList.Count;

        for (int i = 0; i < countitems; i++)
        {
            BuildingTypeItem item = BuildingTypeInfoReplacesList[i];

            if (item.buildingtype.Equals(curBuildingTypeName))
            {
                return item;
            }
        }

        AddNewBuildingTypeInfo(curBuildingTypeName);

        int lastIndex = BuildingTypeInfoReplacesList.Count - 1;

        return BuildingTypeInfoReplacesList[lastIndex];
    }

    public void DeleteUnused()
    {
        BuildingTypeInfoReplacesList.RemoveAll(item => item.buildingMaterial == null);
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }
}
