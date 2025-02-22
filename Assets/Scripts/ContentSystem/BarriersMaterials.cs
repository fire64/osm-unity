using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static BuildingMaterials;

[CreateAssetMenu(fileName = "BarriersMaterials", menuName = "GeoRender/BarriersMaterials", order = 1)]

public class BarriersMaterials : ScriptableObject
{
    // Start is called before the first frame update
    [System.Serializable]
    public struct BarrierMaterialReplace
    {
        [SerializeField]
        public string barriermaterialname;
        [SerializeField]
        public Material barriermaterial;
    }

    [SerializeField]
    List<BarrierMaterialReplace> BarrierMaterialsList;

    public bool CheckForFoundBarrierMaterial(string curBarrierMaterialName)
    {
        int countitems = BarrierMaterialsList.Count;

        BarrierMaterialsList.Sort((p1, p2) => p1.barriermaterialname.CompareTo(p2.barriermaterialname));

        bool isFound = false;

        for (int i = 0; i < countitems; i++)
        {
            BarrierMaterialReplace item = BarrierMaterialsList[i];

            if (item.barriermaterialname.Equals(curBarrierMaterialName))
            {
                isFound = true;
                break;
            }
        }

        return isFound;
    }

    public void AddNewBarrierMaterial(string curBarrierMaterialName)
    {
        BarrierMaterialReplace item = new BarrierMaterialReplace();
        item.barriermaterialname = curBarrierMaterialName;
        item.barriermaterial = null;
        BarrierMaterialsList.Add(item);

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public Material GetBarrierMaterialByName(string curBarrierMaterialName)
    {
        int countitems = BarrierMaterialsList.Count;

        for (int i = 0; i < countitems; i++)
        {
            BarrierMaterialReplace item = BarrierMaterialsList[i];

            if (item.barriermaterialname.Equals(curBarrierMaterialName))
            {
                return item.barriermaterial;
            }
        }

        AddNewBarrierMaterial(curBarrierMaterialName);

        return null;
    }
}
