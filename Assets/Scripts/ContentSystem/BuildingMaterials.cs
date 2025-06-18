using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "BuildingMaterials", menuName = "GeoRender/BuildingMaterials", order = 1)]

public class BuildingMaterials : ScriptableObject
{
    // Start is called before the first frame update
    [System.Serializable]
    public struct BuildingMaterialReplace
    {
        [SerializeField]
        public string buildingmaterialname;
        [SerializeField]
        public Material buildingmaterial;
    }

    [SerializeField]
    List<BuildingMaterialReplace> BuildingMaterialsList;

    public bool CheckForFoundBuildingMaterial(string curBuildingMaterialName)
    {
        int countitems = BuildingMaterialsList.Count;

        BuildingMaterialsList.Sort((p1, p2) => p1.buildingmaterialname.CompareTo(p2.buildingmaterialname));

        bool isFound = false;

        for (int i = 0; i < countitems; i++)
        {
            BuildingMaterialReplace item = BuildingMaterialsList[i];

            if (item.buildingmaterialname.Equals(curBuildingMaterialName))
            {
                isFound = true;
                break;
            }
        }

        return isFound;
    }

    public void AddNewBuildingMaterial(string curBuildingMaterialName)
    {
        BuildingMaterialReplace item = new BuildingMaterialReplace();
        item.buildingmaterialname = curBuildingMaterialName;
        item.buildingmaterial = null;
        BuildingMaterialsList.Add(item);

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public Material GetBuildingMaterialByName(string curBuildingMaterialName)
    {
        int countitems = BuildingMaterialsList.Count;

        for (int i = 0; i < countitems; i++)
        {
            BuildingMaterialReplace item = BuildingMaterialsList[i];

            if (item.buildingmaterialname.Equals(curBuildingMaterialName))
            {
                return item.buildingmaterial;
            }
        }

        AddNewBuildingMaterial(curBuildingMaterialName);

        return null;
    }
}