using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "BuildingTypeMaterials", menuName = "GeoRender/BuildingTypeMaterials", order = 1)]

public class BuildingTypeMaterials : ScriptableObject
{
    // Start is called before the first frame update
    [System.Serializable]
    public struct BuildingTypeMaterialReplace
    {
        [SerializeField]
        public string buildingtype;
        [SerializeField]
        public Material buildingtypematerial;
    }

    [SerializeField]
    List<BuildingTypeMaterialReplace> BuildingTypesMaterialsList;

    public bool CheckForFoundBuildingTypesMaterial(string curBuildingTypeName)
    {
        int countitems = BuildingTypesMaterialsList.Count;

        BuildingTypesMaterialsList.Sort((p1, p2) => p1.buildingtype.CompareTo(p2.buildingtype));

        bool isFound = false;

        for (int i = 0; i < countitems; i++)
        {
            BuildingTypeMaterialReplace item = BuildingTypesMaterialsList[i];

            if (item.buildingtype.Equals(curBuildingTypeName))
            {
                isFound = true;
                break;
            }
        }

        return isFound;
    }

    public void AddNewBuildingTypeMaterial(string curBuildingTypeName)
    {
        BuildingTypeMaterialReplace item = new BuildingTypeMaterialReplace();
        item.buildingtype = curBuildingTypeName;
        BuildingTypesMaterialsList.Add(item);

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public Material GetBuildingTypeMaterialByName(string curBuildingTypeName)
    {
        int countitems = BuildingTypesMaterialsList.Count;

        for (int i = 0; i < countitems; i++)
        {
            BuildingTypeMaterialReplace item = BuildingTypesMaterialsList[i];

            if (item.buildingtype.Equals(curBuildingTypeName))
            {
                return item.buildingtypematerial;
            }
        }

        AddNewBuildingTypeMaterial(curBuildingTypeName);

        return null;
    }
}
