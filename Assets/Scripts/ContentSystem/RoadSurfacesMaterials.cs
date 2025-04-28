using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "RoadSurfacesMaterials", menuName = "GeoRender/RoadSurfacesMaterials", order = 1)]


public class RoadSurfacesMaterials : ScriptableObject
{
    // Start is called before the first frame update
    [System.Serializable]
    public struct RoadSurfacesMaterialReplace
    {
        [SerializeField]
        public string roadSurfacesMaterialName;
        [SerializeField]
        public Material roadSurfacesMaterial;
    }

    [SerializeField]
    List<RoadSurfacesMaterialReplace> RoadSurfacesMaterialsList;

    public bool CheckForFoundRoadSurfacesMaterial(string curRoadSurfacesMaterialName)
    {
        int countitems = RoadSurfacesMaterialsList.Count;

        RoadSurfacesMaterialsList.Sort((p1, p2) => p1.roadSurfacesMaterialName.CompareTo(p2.roadSurfacesMaterialName));

        bool isFound = false;

        for (int i = 0; i < countitems; i++)
        {
            RoadSurfacesMaterialReplace item = RoadSurfacesMaterialsList[i];

            if (item.roadSurfacesMaterialName.Equals(curRoadSurfacesMaterialName))
            {
                isFound = true;
                break;
            }
        }

        return isFound;
    }

    public void AddNewRoadSurfacesMaterial(string curRoadSurfacesMaterialName)
    {
        RoadSurfacesMaterialReplace item = new RoadSurfacesMaterialReplace();
        item.roadSurfacesMaterialName = curRoadSurfacesMaterialName;
        item.roadSurfacesMaterial = null;
        RoadSurfacesMaterialsList.Add(item);

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public Material GetRoadSurfacesMaterialByName(string curRoadSurfacesMaterialName)
    {
        int countitems = RoadSurfacesMaterialsList.Count;

        for (int i = 0; i < countitems; i++)
        {
            RoadSurfacesMaterialReplace item = RoadSurfacesMaterialsList[i];

            if (item.roadSurfacesMaterialName.Equals(curRoadSurfacesMaterialName))
            {
                return item.roadSurfacesMaterial;
            }
        }

        AddNewRoadSurfacesMaterial(curRoadSurfacesMaterialName);

        return null;
    }

    public void DeleteUnused()
    {
        RoadSurfacesMaterialsList.RemoveAll(item => item.roadSurfacesMaterial == null);
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }
}