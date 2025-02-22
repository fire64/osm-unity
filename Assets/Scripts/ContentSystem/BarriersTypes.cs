using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "BarriersTypes", menuName = "GeoRender/BarriersTypes", order = 1)]

public class BarriersTypes : ScriptableObject
{
    // Start is called before the first frame update
    [System.Serializable]
    public struct BarrierTypeInfoItem
    {
        [SerializeField]
        public string barriertype;
        [SerializeField]
        public string barrierDescription;
        [SerializeField]
        public Material barrierMaterial;
        [SerializeField]
        public float barrierWidth;
        [SerializeField]
        public float barrierHeight;
    }

    [SerializeField]
    List<BarrierTypeInfoItem> BarrierTypeInfoReplacesList;

    public bool CheckForFoundBarrierTypeInfo(string curBarrierTypeName)
    {
        int countitems = BarrierTypeInfoReplacesList.Count;

        BarrierTypeInfoReplacesList.Sort((p1, p2) => p1.barriertype.CompareTo(p2.barriertype));

        bool isFound = false;

        for (int i = 0; i < countitems; i++)
        {
            BarrierTypeInfoItem item = BarrierTypeInfoReplacesList[i];

            if (item.barriertype.Equals(curBarrierTypeName))
            {
                isFound = true;
                break;
            }
        }

        return isFound;
    }

    public void AddNewBarrierTypeInfo(string curBarrierTypeName)
    {
        BarrierTypeInfoItem item = new BarrierTypeInfoItem();
        item.barriertype = curBarrierTypeName;
        item.barrierMaterial = null;
        item.barrierWidth = 0.5f;
        item.barrierHeight = 1f;

        BarrierTypeInfoReplacesList.Add(item);

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public BarrierTypeInfoItem GetBarrierTypeInfoByName(string curBarrierTypeName)
    {
        int countitems = BarrierTypeInfoReplacesList.Count;

        for (int i = 0; i < countitems; i++)
        {
            BarrierTypeInfoItem item = BarrierTypeInfoReplacesList[i];

            if (item.barriertype.Equals(curBarrierTypeName))
            {
                return item;
            }
        }

        AddNewBarrierTypeInfo(curBarrierTypeName);

        int lastIndex = BarrierTypeInfoReplacesList.Count - 1;

        return BarrierTypeInfoReplacesList[lastIndex];
    }
}
