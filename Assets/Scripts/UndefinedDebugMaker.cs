using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;

class UndefinedDebugMaker : InfrstructureBehaviour
{
    public static GameContentSelector contentselector;

    public GameObject tempMarker;
    public bool isUseTempMaker = true;
    public bool isUseRenders = false;
    public bool isCreateColision = false;
    public int MaxNodes = 150;
    public TileSystem tileSystem;

    private void SetProperties(BaseOsm geo, Undefined undefined)
    {
        undefined.name = "undefined " + geo.ID.ToString();

        if (geo.HasField("name"))
            undefined.Name = geo.GetValueStringByKey("name");

        undefined.Id = geo.ID.ToString();

        undefined.Kind = "undefined";

        if (geo.HasField("layer"))
        {
            undefined.layer = geo.GetValueIntByKey("layer");
        }

        if (geo.HasField("source_type"))
            undefined.Source = geo.GetValueStringByKey("source_type");

        undefined.isClosed = geo.IsClosedPolygon;
    }

    void CreateTempMarker(Undefined undefined)
    {
        var go = Instantiate(tempMarker, undefined.transform.position, Quaternion.identity);

        if(undefined.isClosed)
        {
            go.GetComponentInChildren<TMPro.TextMeshPro>().text = "Undefined Polygon";
        }
        else
        {
            go.GetComponentInChildren<TMPro.TextMeshPro>().text = "Undefined Line";
        }

        go.transform.SetParent(undefined.transform);
    }

    void CreateUndefinedDebugObject(BaseOsm geo)
    {
        var searchname = "undefined " + geo.ID.ToString();

        //Check for duplicates in case of loading multiple locations
        if (GameObject.Find(searchname))
        {
            return;
        }

        if (contentselector.isGeoObjectDisabled(geo.ID))
        {
            return;
        }

        var count = geo.NodeIDs.Count;

        if (count > MaxNodes)
        {
            Debug.LogError(searchname + " haved " + count + " nodes.");
            return;
        }

        var undefined = new GameObject(searchname).AddComponent<Undefined>();

        undefined.itemlist = geo.itemlist;

        SetProperties(geo, undefined);

        Vector3 localOrigin = GetCentre(geo);
        undefined.transform.position = localOrigin - map.bounds.Centre;

        if (tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            if (tileSystem.isUseElevation)
            {
                undefined.transform.position = GR.getHeightPosition(undefined.transform.position);
            }
        }

        undefined.transform.position += Vector3.up * (undefined.layer * BaseDataObject.layer_size);

        if(isUseTempMaker)
        {
            CreateTempMarker(undefined);
        }

        if(isUseRenders)
        {
            undefined.AddComponent<MeshFilter>();
            undefined.AddComponent<MeshRenderer>();

            var undefinedCorners = new List<Vector3>();

            var countContour = geo.NodeIDs.Count;

            for (int i = 0; i < countContour; i++)
            {
                OsmNode point = map.nodes[geo.NodeIDs[i]];

                Vector3 coords = point - localOrigin;

                undefinedCorners.Add(coords);
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

            var mesh = undefined.GetComponent<MeshFilter>().mesh;

            var tb = new MeshData();

            float finalWidth = 2.0f;

            if (geo.IsClosedPolygon)
            {
                GR.CreateMeshWithHeight(undefinedCorners, 0.0f, 0.01f, tb, holesCorners);
            }
            else if (geo.HasField("type") && geo.GetValueStringByKey("type") == "multipolygon")
            {
          //      GR.CreateMeshWithHeight(undefinedCorners, 0.0f, 0.01f, tb, holesCorners);
            }
            else
            {
                GR.CreateMeshLineWithWidth(undefinedCorners, finalWidth, tb);
            }

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
                undefined.transform.gameObject.AddComponent<MeshCollider>();
                undefined.transform.GetComponent<MeshCollider>().sharedMesh = undefined.GetComponent<MeshFilter>().mesh;
                undefined.transform.GetComponent<MeshCollider>().convex = false;
            }
        }
    }

    // Start is called before the first frame update
    IEnumerator Start()
    {
        while (!map.IsReady)
        {
            yield return null;
        }

        contentselector = FindObjectOfType<GameContentSelector>();

        tileSystem = FindObjectOfType<TileSystem>();

        foreach (var way in map.ways.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Undefined && w.NodeIDs.Count > 1; }))
        {
            if(way.itemlist.Length > 0)
            {
                way.AddField("source_type", "way");
                CreateUndefinedDebugObject(way);
            }

            yield return null;
        }

        foreach (var relation in map.relations.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Undefined && w.NodeIDs.Count > 1; }))
        {
            if (relation.itemlist.Length > 0)
            {
                relation.AddField("source_type", "relation");
                CreateUndefinedDebugObject(relation);
            }

            yield return null;
        }

        isFinished = true;
    }
}
