using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.UI.GridLayoutGroup;

class WaterMaker : InfrstructureBehaviour
{
    public Material waterMaterial;
    public static GameContentSelector contentselector;
    public bool isFixHeight = true;

    private void SetProperties(BaseOsm geo, Water water)
    {
        water.name = "water " + geo.ID.ToString();

        if (geo.HasField("name"))
            water.Name = geo.GetValueStringByKey("name");

        water.Id = geo.ID.ToString();

        var kind = "";

        if (geo.HasField("water"))
        {
            kind = geo.GetValueStringByKey("water");
        }
        else
        {
            kind = "yes";
        }

        water.Kind = kind;

        if (geo.HasField("source_type"))
            water.Source = geo.GetValueStringByKey("source_type");

        water.GetComponent<MeshRenderer>().material = waterMaterial;

//      water.GetComponent<MeshRenderer>().material.SetColor("_Color", GR.SetOSMColour(geo)); //Not used color for water ))
    }

    void CreateWaterss(BaseOsm geo)
    {
        var searchname = "water " + geo.ID.ToString();

        //Check for duplicates in case of loading multiple locations
        if (GameObject.Find(searchname))
        {
            return;
        }

        if (contentselector.isGeoObjectDisabled(geo.ID))
        {
            return;
        }

        var water = new GameObject(searchname).AddComponent<Water>();

        water.AddComponent<MeshFilter>();
        water.AddComponent<MeshRenderer>();

        water.itemlist = geo.itemlist;

        SetProperties(geo, water);

        var waterCorners = new List<Vector3>();

        var count = geo.NodeIDs.Count;

        Vector3 localOrigin = GetCentre(geo);
        water.transform.position = localOrigin - map.bounds.Centre;

        if (isFixHeight)
        {
            water.transform.position = GR.getHeightPosition(water.transform.position);
        }

        for (int i = 0; i < count; i++)
        {
            OsmNode point = map.nodes[geo.NodeIDs[i]];

            Vector3 coords = point - localOrigin;

            waterCorners.Add(coords);
        }

        var mesh = water.GetComponent<MeshFilter>().mesh;

        var tb = new MeshData();

        float finalWidth = 2.0f;

        if(geo.IsClosedPolygon)
        {
            GR.CreateMeshWithHeight(waterCorners, 0.0f, 0.01f, tb);
        }
        else if(geo.HasField("type") && geo.GetValueStringByKey("type") == "multipolygon")
        {
            GR.CreateMeshWithHeight(waterCorners, 0.0f, 0.01f, tb);
        }
        else
        {
            GR.CreateMeshLineWithWidth(waterCorners, finalWidth, tb);
        }

        mesh.vertices = tb.Vertices.ToArray();
        mesh.triangles = tb.Indices.ToArray();
        mesh.normals = tb.Normals.ToArray();
        mesh.SetUVs(0, tb.UV);

        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        mesh.RecalculateNormals();

        //Add colider 
        water.transform.gameObject.AddComponent<MeshCollider>();
        water.transform.GetComponent<MeshCollider>().sharedMesh = water.GetComponent<MeshFilter>().mesh;
        water.transform.GetComponent<MeshCollider>().convex = false;
    }

    IEnumerator Start()
    {
        while (!map.IsReady)
        {
            yield return null;
        }

        contentselector = FindObjectOfType<GameContentSelector>();

        foreach (var way in map.ways.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Water; }))
        {
            way.AddField("source_type", "way");
            CreateWaterss(way);
            yield return null;
        }

        foreach (var relation in map.relations.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Water; }))
        {
            relation.AddField("source_type", "relation");

            CreateWaterss(relation);

            yield return null;
        }
    }
}
