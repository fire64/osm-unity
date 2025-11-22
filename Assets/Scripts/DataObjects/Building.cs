using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static BuildingTypes;

public class Building : BaseDataObject
{
    public BuildingTypeItem curSettings;
    public int count;
    public bool isModelSet;
    public string series_filter;

    // Draws a wireframe box around the selected object,
    // indicating world space bounding volume.
    public void OnDrawGizmos()
    {

    }
}
