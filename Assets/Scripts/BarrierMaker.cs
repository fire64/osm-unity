using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

class BarrierMaker : InfrstructureBehaviour
{
    public static GameContentSelector contentselector;
    public BarriersTypes barrierTypes;
    public BarriersMaterials barrierMaterials;
    public bool isCreateColision = false;
    public int MaxNodes = 150;
    public TileSystem tileSystem;
    private void SetProperties(BaseOsm geo, Barrier barrier)
    {
        barrier.name = "barrier " + geo.ID.ToString();

        if (geo.HasField("name"))
            barrier.Name = geo.GetValueStringByKey("name");

        barrier.Id = geo.ID.ToString();

        var kind = "";

        if (geo.HasField("barrier"))
        {
            kind = geo.GetValueStringByKey("barrier");
        }
        else
        {
            kind = "yes";
        }

        barrier.Kind = kind;

        if (geo.HasField("source_type"))
            barrier.Source = geo.GetValueStringByKey("source_type");

        var barrierInfo = barrierTypes.GetBarrierTypeInfoByName(barrier.Kind);

        if (geo.HasField("height"))
        {
            barrier.height = geo.GetValueFloatByKey("height");
        }
        else
        {
            barrier.height = barrierInfo.barrierHeight;
        }

        if (geo.HasField("min_height"))
        {
            barrier.min_height = geo.GetValueFloatByKey("min_height");
        }
        else
        {
            barrier.min_height = 0.0f;
        }

        if (geo.HasField("layer"))
        {
            barrier.layer = geo.GetValueIntByKey("layer");
        }

        barrier.width = barrierInfo.barrierWidth;

        var materal_type = barrier.Kind;

        if (geo.HasField("fence_type"))
        {
            barrier.fence_type = geo.GetValueStringByKey("fence_type");

            materal_type = materal_type + "_" + barrier.fence_type;
        }

        if (geo.HasField("material"))
        {
            barrier.material = geo.GetValueStringByKey("material");

            materal_type = materal_type + "_" + barrier.material;
        }

        Material barrier_mat = barrierMaterials.GetBarrierMaterialByName(materal_type);

        if(barrier_mat != null)
        {
            barrier.GetComponent<MeshRenderer>().material = barrier_mat;
        }
        else
        {
            barrier.GetComponent<MeshRenderer>().material = barrierInfo.barrierMaterial;
        }

        barrier.GetComponent<MeshRenderer>().material.SetColor("_Color", GR.SetOSMColour(geo));
    }

    void CreateBarriers(BaseOsm geo)
    {
        var searchname = "barrier " + geo.ID.ToString();

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

        var barrier = new GameObject(searchname).AddComponent<Barrier>();

        barrier.AddComponent<MeshFilter>();
        barrier.AddComponent<MeshRenderer>();

        barrier.itemlist = geo.itemlist;

        SetProperties(geo, barrier);

        var barrierCorners = new List<Vector3>();

        Vector3 localOrigin = GetCentre(geo);
        barrier.transform.position = localOrigin - map.bounds.Centre;

        if (tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            if (tileSystem.isUseElevation)
            {
                barrier.transform.position = GR.getHeightPosition(barrier.transform.position);
            }
        }

        barrier.transform.position += Vector3.up * (barrier.layer * BaseDataObject.layer_size);

        for (int i = 0; i < count; i++)
        {
            OsmNode point = map.nodes[geo.NodeIDs[i]];

            Vector3 coords = point - localOrigin;

            barrierCorners.Add(coords);
        }

        var mesh = barrier.GetComponent<MeshFilter>().mesh;

        var tb = new MeshData();

        GR.CreateMeshLineWithWidthAndHeight(barrierCorners, barrier.height, barrier.min_height, barrier.width, tb);

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
            barrier.transform.gameObject.AddComponent<MeshCollider>();
            barrier.transform.GetComponent<MeshCollider>().sharedMesh = barrier.GetComponent<MeshFilter>().mesh;
            barrier.transform.GetComponent<MeshCollider>().convex = false;
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

        foreach (var way in map.ways.FindAll((w) => { return w.IsBarrier == true; }))
        {
            way.AddField("source_type", "way");
            CreateBarriers(way);
            yield return null;
        }

        foreach (var relation in map.relations.FindAll((w) => { return w.IsBarrier == true; }))
        {
            relation.AddField("source_type", "relation");

            CreateBarriers(relation);

            yield return null;
        }

        isFinished = true;
    }
}
