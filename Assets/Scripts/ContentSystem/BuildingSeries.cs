using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static BuildingMaterials;

[CreateAssetMenu(fileName = "BuildingSeries", menuName = "GeoRender/BuildingSeries", order = 1)]

public class BuildingSeries : ScriptableObject
{
    // Start is called before the first frame update
    [System.Serializable]
    public struct BuildingSeriesReplace
    {
        [SerializeField]
        public string predstavlenie;
        [SerializeField]
        public string series;
        [SerializeField]
        public int floors;
        [SerializeField]
        public int entrances;
        [SerializeField]
        public GameObject buildingmodel;
    }

    [SerializeField]
    List<BuildingSeriesReplace> BuildingSeriesReplaceList;

    public BuildingSeriesReplace AddNewBuildingSeries(string curpredstavlenie, string curseries, int curfloors, int entrances)
    {
        BuildingSeriesReplace item = new BuildingSeriesReplace();
        item.predstavlenie = curpredstavlenie;
        item.series = curseries;
        item.floors = curfloors;
        item.entrances = entrances;
        item.buildingmodel = null;
        BuildingSeriesReplaceList.Add(item);

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif

        return item;
    }

    public BuildingSeriesReplace GetBuildingSeriesInfo(string curseries, int curfloors, int curentrances)
    {
        string curpredstavlenie = curseries + "_" + curfloors + "_" + curentrances;

        BuildingSeriesReplaceList.Sort((p1, p2) => p1.predstavlenie.CompareTo(p2.predstavlenie));

        int countitems = BuildingSeriesReplaceList.Count;

        for (int i = 0; i < countitems; i++)
        {
            BuildingSeriesReplace item = BuildingSeriesReplaceList[i];

            if (item.predstavlenie.Equals(curpredstavlenie))
            {
                return item;
            }
        }

        return AddNewBuildingSeries(curpredstavlenie, curseries, curfloors, curentrances);
    }
}
