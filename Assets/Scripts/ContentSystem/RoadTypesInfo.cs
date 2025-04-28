using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "RoadTypesInfo", menuName = "GeoRender/RoadTypesInfo", order = 1)]

public class RoadTypesInfo : ScriptableObject
{
    // Start is called before the first frame update
    [System.Serializable]
    public struct RoadTypeInfoItem
    {
        [SerializeField]
        public string roadtype;
        [SerializeField]
        public Material roadMaterial;
        [SerializeField]
        public float roadWidth;
        [SerializeField]
        public int layersLevel; //for sorting...
    }

    [SerializeField]
    List<RoadTypeInfoItem> RoadTypeInfoReplacesList;

    public bool CheckForFoundRoadTypeInfo(string curRoadTypeName)
    {
        int countitems = RoadTypeInfoReplacesList.Count;

        RoadTypeInfoReplacesList.Sort((p1, p2) => p1.roadtype.CompareTo(p2.roadtype));

        bool isFound = false;

        for (int i = 0; i < countitems; i++)
        {
            RoadTypeInfoItem item = RoadTypeInfoReplacesList[i];

            if (item.roadtype.Equals(curRoadTypeName))
            {
                isFound = true;
                break;
            }
        }

        return isFound;
    }

    public void AddNewRoadTypeInfo(string curRoadTypeName)
    {
        RoadTypeInfoItem item = new RoadTypeInfoItem();
        item.roadtype = curRoadTypeName;
        item.roadMaterial = null;
        item.roadWidth = 0.0f; //not use default 2.0f for for these, is not checked

        RoadTypeInfoReplacesList.Add(item);

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public RoadTypeInfoItem GetRoadTypeInfoByName(string curRoadTypeName)
    {
        int countitems = RoadTypeInfoReplacesList.Count;

        for (int i = 0; i < countitems; i++)
        {
            RoadTypeInfoItem item = RoadTypeInfoReplacesList[i];

            if (item.roadtype.Equals(curRoadTypeName))
            {
                return item;
            }
        }

        AddNewRoadTypeInfo(curRoadTypeName);

        int lastIndex = RoadTypeInfoReplacesList.Count - 1;

        return RoadTypeInfoReplacesList[lastIndex];
    }

    public void DeleteUnused()
    {
        RoadTypeInfoReplacesList.RemoveAll(item => item.roadMaterial == null);
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }
}
