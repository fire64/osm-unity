using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "DetailsTypes", menuName = "GeoRender/DetailsTypes", order = 1)]

public class DetailsTypes : ScriptableObject
{
    // Start is called before the first frame update
    [System.Serializable]
    public struct DetailsTypesItem
    {
        [SerializeField]
        public string detailsType;
        [SerializeField]
        public string detailsDescription;
        [SerializeField]
        public bool isTempMarkerEnable;
        [SerializeField]
        public GameObject detailsPrefab;
    }

    [SerializeField]
    List<DetailsTypesItem> DetailsTypesReplacesList;

    public bool CheckForFoundDetailsTypeInfo(string curDetailsTypeName)
    {
        int countitems = DetailsTypesReplacesList.Count;

        DetailsTypesReplacesList.Sort((p1, p2) => p1.detailsType.CompareTo(p2.detailsType));

        bool isFound = false;

        for (int i = 0; i < countitems; i++)
        {
            DetailsTypesItem item = DetailsTypesReplacesList[i];

            if (item.detailsType.Equals(curDetailsTypeName))
            {
                isFound = true;
                break;
            }
        }

        return isFound;
    }

    public void DeleteUnused()
    {
        DetailsTypesReplacesList.RemoveAll(item => item.detailsPrefab == null);
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public void AddNewDetailsTypeInfo(string curDetailsTypeName)
    {
        DetailsTypesItem item = new DetailsTypesItem();
        item.detailsType = curDetailsTypeName;

        item.detailsDescription = null;
        item.detailsPrefab = null;
        item.isTempMarkerEnable = false;

        DetailsTypesReplacesList.Add(item);

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public DetailsTypesItem GetDetailsTypeInfoByName(string curDetailsTypeName)
    {
        int countitems = DetailsTypesReplacesList.Count;

        for (int i = 0; i < countitems; i++)
        {
            DetailsTypesItem item = DetailsTypesReplacesList[i];

            if (item.detailsType.Equals(curDetailsTypeName))
            {
                return item;
            }
        }

        AddNewDetailsTypeInfo(curDetailsTypeName);

        int lastIndex = DetailsTypesReplacesList.Count - 1;

        return DetailsTypesReplacesList[lastIndex];
    }
}
