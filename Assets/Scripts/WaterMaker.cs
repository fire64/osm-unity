using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.UI.GridLayoutGroup;

class WaterMaker : InfrstructureBehaviour
{
    public Material waterMaterial;
    public static GameContentSelector contentselector;
    public bool isCreateColision = false;
    public TileSystem tileSystem;
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

        if (geo.HasField("layer"))
        {
            water.layer = geo.GetValueIntByKey("layer");
        }

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

        var countContour = geo.NodeIDs.Count;

        if(countContour < 3)
        {
            return;
        }

        Vector3 localOrigin = GetCentre(geo);
        water.transform.position = localOrigin - map.bounds.Centre;

        if(tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            if(tileSystem.isUseElevation)
            {
                water.transform.position = GR.getHeightPosition(water.transform.position);
            }
        }
        
        water.transform.position += Vector3.up * 0.025f;
        water.transform.position += Vector3.up * (water.layer * BaseDataObject.layer_size);

        for (int i = 0; i < countContour; i++)
        {
            OsmNode point = map.nodes[geo.NodeIDs[i]];

            Vector3 coords = point - localOrigin;

            waterCorners.Add(coords);
        }

        var holesCorners = new List<List<Vector3>>();

        var countHoles = geo.HolesNodeListsIDs.Count;

        for (int i = 0; i < countHoles; i++)
        {
            var holeNodes = geo.HolesNodeListsIDs[i];

            var countHoleContourPoints = holeNodes.Count;

            // Создаем новый контур для каждого отверстия
            var holeContour = new List<Vector3>();

            for (int j = 0; j < countHoleContourPoints; j++)
            {
                OsmNode point = map.nodes[holeNodes[j]];
                Vector3 coords = point - localOrigin;
                holeContour.Add(coords);
            }

            holesCorners.Add(holeContour);
        }

        var mesh = water.GetComponent<MeshFilter>().mesh;

        var tb = new MeshData();

        float finalWidth = 2.0f;

        if(geo.IsClosedPolygon)
        {
            GR.CreateMeshWithHeight(waterCorners, -10.0f, 0.0f, tb, holesCorners);
        }
        else if(geo.HasField("type") && geo.GetValueStringByKey("type") == "multipolygon")
        {
            GR.CreateMeshWithHeight(waterCorners, -10.0f, 0.0f, tb, holesCorners);
        }
        else
        {
            GR.CreateMeshLineWithWidthAndHeight(waterCorners, 0.01f, 0.0f, finalWidth, tb);
        }

        mesh.vertices = tb.Vertices.ToArray();
        mesh.triangles = tb.Indices.ToArray();
        mesh.normals = tb.Normals.ToArray();
        mesh.SetUVs(0, tb.UV);

        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        mesh.RecalculateNormals();

        //Add colider 
        if(isCreateColision)
        {
            water.transform.gameObject.AddComponent<MeshCollider>();
            water.transform.GetComponent<MeshCollider>().sharedMesh = water.GetComponent<MeshFilter>().mesh;
            water.transform.GetComponent<MeshCollider>().convex = false;
        }

        water.Activate();
    }
    IEnumerator Start()
    {
        while (!map.IsReady)
        {
            yield return null;
        }

        contentselector = FindObjectOfType<GameContentSelector>();

        tileSystem = FindObjectOfType<TileSystem>();

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

        isFinished = true;
    }
}
