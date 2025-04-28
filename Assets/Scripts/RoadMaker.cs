using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;

class RoadMaker : InfrstructureBehaviour
{
    public Material roadMaterial;
    public static GameContentSelector contentselector;

    public RoadTypesInfo roadTypes;
    public RoadSurfacesMaterials roadSurfacesMaterials;
    public bool isCreateColision = false;
    public TileSystem tileSystem;
    private void SetProperties(BaseOsm geo, Road road)
    {
        road.name = "road " + geo.ID.ToString();

        if (geo.HasField("name"))
            road.Name = geo.GetValueStringByKey("name");

        road.Id = geo.ID.ToString();

        var kind = "";

        if (geo.HasField("highway"))
        {
            kind = geo.GetValueStringByKey("highway");
        }
        else if (geo.HasField("railway"))
        {
            kind = geo.GetValueStringByKey("railway");
        }
        else
        {
            kind = "yes";
        }

        road.Kind = kind;

        if (geo.HasField("source_type"))
            road.Source = geo.GetValueStringByKey("source_type");

        if (geo.HasField("lanes"))
        {
            road.lanes = geo.GetValueIntByKey("lanes");
        }
        else
        {
            road.lanes = 1;
        }

        if (geo.HasField("layer"))
        {
            road.layer = geo.GetValueIntByKey("layer");
        }

        var roadInfo = roadTypes.GetRoadTypeInfoByName(road.Kind);

        Material surfaceMaterial = null;

        if (geo.HasField("surface"))
        {
            var surfaceName = geo.GetValueStringByKey("surface");

            surfaceMaterial = roadSurfacesMaterials.GetRoadSurfacesMaterialByName(surfaceName);
        }

        if (surfaceMaterial != null)
        {
            road.GetComponent<MeshRenderer>().material = surfaceMaterial;
        }
        else if(roadInfo.roadMaterial)
        {
            road.GetComponent<MeshRenderer>().material = roadInfo.roadMaterial;
        }
        else
        {
            road.GetComponent<MeshRenderer>().material = roadMaterial;
        }

        if (roadInfo.roadWidth != 0.0f)
        {
            road.width = roadInfo.roadWidth;
        }
        else
        {
            road.width = 2.0f;
        }

        road.layersLevel = roadInfo.layersLevel;

        road.GetComponent<MeshRenderer>().material.SetColor("_Color", GR.SetOSMColour(geo));
    }

    Vector3 GetRoadHeight(Road road, ulong roadid)
    {
        // Ѕазова€ высота из уровн€ слоев дороги
        double height = 0.001f * road.layersLevel;

        // √енераци€ уникального смещени€ в диапазоне [0.0001, 0.0009]
        double idBasedOffset = 0.0001f + (float)((double)roadid / 1000000000 * 0.0008f);

        // ƒобавл€ем смещение к общей высоте
        height += idBasedOffset;

        Vector3 vec = new Vector3(0f, (float)height, 0f);

        return vec;
    }

    void CreateRoads(BaseOsm geo)
    {
        var searchname = "road " + geo.ID.ToString();

        //Check for duplicates in case of loading multiple locations
        if (GameObject.Find(searchname))
        {
            return;
        }

        if (contentselector.isGeoObjectDisabled(geo.ID))
        {
            return;
        }

        var road = new GameObject(searchname).AddComponent<Road>();

        road.AddComponent<MeshFilter>();
        road.AddComponent<MeshRenderer>();

        road.itemlist = geo.itemlist;

        SetProperties(geo, road);

        var roadsCorners = new List<Vector3>();

        var count = geo.NodeIDs.Count;

        Vector3 roadlayerHeight = GetRoadHeight(road, geo.ID);

        Vector3 localOrigin = GetCentre(geo);
        road.transform.position = localOrigin - map.bounds.Centre;

        if (tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            if (tileSystem.isUseElevation)
            {
                road.transform.position = GR.getHeightPosition(road.transform.position);
            }
        }

        road.transform.position += roadlayerHeight;

        road.transform.position += Vector3.up * (road.layer * BaseDataObject.layer_size);

        for (int i = 0; i < count; i++)
        {
            OsmNode point = map.nodes[geo.NodeIDs[i]];

            Vector3 coords = point - localOrigin;

            roadsCorners.Add(coords);
        }

        var mesh = road.GetComponent<MeshFilter>().mesh;

        var tb = new MeshData();

        float finalWidth = road.width * road.lanes;

        GR.CreateMeshLineWithWidth(roadsCorners, finalWidth, tb);

        mesh.vertices = tb.Vertices.ToArray();
        mesh.triangles = tb.Indices.ToArray();
        mesh.normals = tb.Normals.ToArray();
        mesh.SetUVs(0, tb.UV);

        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        mesh.RecalculateNormals();

        //Add colider
        if (isCreateColision)
        {
            road.transform.gameObject.AddComponent<MeshCollider>();
            road.transform.GetComponent<MeshCollider>().sharedMesh = road.GetComponent<MeshFilter>().mesh;
            road.transform.GetComponent<MeshCollider>().convex = false;
        }

    }
    IEnumerator Start()
    {        
        while (!map.IsReady)
        {
            yield return null;
        }

        contentselector = FindObjectOfType<GameContentSelector>();

        tileSystem = FindObjectOfType<TileSystem>();

        foreach (var way in map.ways.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Road; }))
        {
            way.AddField("source_type", "way");
            CreateRoads(way);
            yield return null;
        }

        foreach (var relation in map.relations.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Road && w.IsClosedPolygon; }))
        {
            relation.AddField("source_type", "relation");
            CreateRoads(relation);
            yield return null;
        }

        isFinished = true;
    }         
}
