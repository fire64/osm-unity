using System.Collections.Generic;
using UnityEngine;

public class Landuse : BaseDataObject
{
    public bool isEnableRender;
    public bool isGrassGenerate;
    public bool isTreesGenerate;
    public bool isFlatUV;
    public float fHeightLayer;

    [SerializeField]
    public List<GrassType> grassTypes;

    public void Activate()
    {
        if(isGrassGenerate && grassTypes != null && grassTypes.Count > 0)
        {
            GrassGenerator grassGen = gameObject.AddComponent<GrassGenerator>();

            grassGen.InitGrassGeneration(grassTypes);
        }
    }
}