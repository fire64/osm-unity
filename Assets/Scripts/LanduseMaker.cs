using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

class LanduseMaker : InfrstructureBehaviour
{
    public static GameContentSelector contentselector;
    public bool bNotCreateNotClosedPolygon;

    public Material grassMaterial;
    public LanduseTypes landuseTypes;
    public bool isFixHeight = true;
    public bool isCreateColision = false;
    private void SetProperties(BaseOsm geo, Landuse landuse)
    {
        landuse.name = "landuse " + geo.ID.ToString();

        if (geo.HasField("name"))
            landuse.Name = geo.GetValueStringByKey("name");

        landuse.Id = geo.ID.ToString();

        if (geo.HasField("source_type"))
            landuse.Source = geo.GetValueStringByKey("source_type");

        var kind = "";

        if (geo.HasField("natural"))
        {
            kind = geo.GetValueStringByKey("natural");
        }
        else if (geo.HasField("leisure"))
        {
            kind = geo.GetValueStringByKey("leisure");
        }
        else if (geo.HasField("landuse"))
        {
            kind = geo.GetValueStringByKey("landuse");
        }
        else
        {
            kind = "yes";
        }

        landuse.Kind = kind;

        var landuseInfo = landuseTypes.GetLanduseTypeInfoByName(landuse.Kind);

        if (geo.HasField("source_type"))
        {
            landuse.Source = geo.GetValueStringByKey("source_type");
        }

        landuse.isEnableRender = landuseInfo.isRenderEnable;
        landuse.isGrassGenerate = landuseInfo.isGrassGenerate;
        landuse.isTreesGenerate = landuseInfo.isTreesGenerate;

        if (landuseInfo.groundMaterial != null)
        {
            landuse.GetComponent<MeshRenderer>().material = landuseInfo.groundMaterial;
        }
        else
        {
            landuse.GetComponent<MeshRenderer>().material = grassMaterial;
        }
    }

    void CreateLanduse(BaseOsm geo)
    {
        var searchname = "landuse " + geo.ID.ToString();

        //Check for duplicates in case of loading multiple locations
        if (GameObject.Find(searchname))
        {
            return;
        }

        if (contentselector.isGeoObjectDisabled(geo.ID))
        {
            return;
        }

        if (!geo.IsClosedPolygon && bNotCreateNotClosedPolygon)
        {
            return;
        }

        var landuse = new GameObject(searchname).AddComponent<Landuse>();

        landuse.AddComponent<MeshFilter>();
        landuse.AddComponent<MeshRenderer>();

        landuse.itemlist = geo.itemlist;

        SetProperties(geo, landuse);

        var landuseCorners = new List<Vector3>();

        var count = geo.NodeIDs.Count;

        Vector3 localOrigin = GetCentre(geo);
        landuse.transform.position = localOrigin - map.bounds.Centre;

        if (isFixHeight)
        {
            landuse.transform.position = GR.getHeightPosition(landuse.transform.position);
        }

        for (int i = 0; i < count; i++)
        {
            OsmNode point = map.nodes[geo.NodeIDs[i]];

            Vector3 coords = point - localOrigin;

            landuseCorners.Add(coords);
        }

        var holesCorners = new List<List<Vector3>>();

        var countHoles = geo.HolesNodeListsIDs.Count;

        for (int i = 0; i < countHoles; i++)
        {
            var holeNodes = geo.HolesNodeListsIDs[i];

            var countHoleContourPoints = holeNodes.Count;

            // ������� ����� ������ ��� ������� ���������
            var holeContour = new List<Vector3>();

            for (int j = 0; j < countHoleContourPoints; j++)
            {
                OsmNode point = map.nodes[holeNodes[j]];
                Vector3 coords = point - localOrigin;
                holeContour.Add(coords);
            }

            holesCorners.Add(holeContour);
        }

        var mesh = landuse.GetComponent<MeshFilter>().mesh;

        var tb = new MeshData();

        if(landuse.isEnableRender)
        {
            GR.CreateMeshWithHeight(landuseCorners, 0.0f, 0.005f, tb, holesCorners);
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
            landuse.transform.gameObject.AddComponent<MeshCollider>();
            landuse.transform.GetComponent<MeshCollider>().sharedMesh = landuse.GetComponent<MeshFilter>().mesh;
            landuse.transform.GetComponent<MeshCollider>().convex = false;
        }
    }

    IEnumerator Start()
    {
        while (!map.IsReady)
        {
            yield return null;
        }

        contentselector = FindObjectOfType<GameContentSelector>();

        foreach (var way in map.ways.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Landuse && w.NodeIDs.Count > 1; }))
        {
            way.AddField("source_type", "way");
            CreateLanduse(way);
            yield return null;
        }

        foreach (var relation in map.relations.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Landuse && w.NodeIDs.Count > 1; }))
        {
            relation.AddField("source_type", "relation");
            CreateLanduse(relation);
            yield return null;
        }

    }
}
