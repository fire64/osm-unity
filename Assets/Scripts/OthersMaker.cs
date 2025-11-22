using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;

class OthersMaker : InfrstructureBehaviour
{
    public static GameContentSelector contentselector;
    public bool isCreateColision = false;
    public int MaxNodes = 150;
    public TileSystem tileSystem;

    private int m_countProcessing = 0;

    public enum other_type
    {
        notset = 0,
        power_wire,
    };

    private void SetProperties(BaseOsm geo, Undefined other)
    {
        other.name = "other " + geo.ID.ToString();

        if (geo.HasField("name"))
            other.Name = geo.GetValueStringByKey("name");

        other.Id = geo.ID.ToString();

        other.Kind = "other";

        if (geo.HasField("layer"))
        {
            other.layer = geo.GetValueIntByKey("layer");
        }

        if (geo.HasField("source_type"))
            other.Source = geo.GetValueStringByKey("source_type");

        other.isClosed = geo.IsClosedPolygon;
    }

    private other_type GetOtherType(BaseOsm geo)
    {
        other_type cur_type = other_type.notset;

        if (geo.HasField("power") && geo.GetValueStringByKey("power").Equals("line") )
        {
            cur_type = other_type.power_wire;
        }

        return cur_type;
    }

    void CreateOtherObject(BaseOsm geo)
    {
        var searchname = "other " + geo.ID.ToString();

        m_countProcessing++;

        //Check for duplicates in case of loading multiple locations
        if (GameObject.Find(searchname))
        {
            return;
        }

        if (contentselector.isGeoObjectDisabled(geo.ID))
        {
            return;
        }

        other_type cur_type = GetOtherType(geo);

        if(cur_type == other_type.notset)
        {
            return;
        }

        var count = geo.NodeIDs.Count;

        if (count > MaxNodes)
        {
            Debug.LogError(searchname + " haved " + count + " nodes.");
            return;
        }

        var other = new GameObject(searchname).AddComponent<Undefined>();

        other.itemlist = geo.itemlist;

        SetProperties(geo, other);

        Vector3 localOrigin = GetCentre(geo);
        other.transform.position = localOrigin - map.bounds.Centre;

        if (tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            if (tileSystem.isUseElevation)
            {
                other.transform.position = GR.getHeightPosition(other.transform.position);
            }
        }

        other.transform.position += Vector3.up * (other.layer * BaseDataObject.layer_size);
        other.AddComponent<MeshFilter>();
        other.AddComponent<MeshRenderer>();

        var otherCorners = new List<Vector3>();

        var countContour = geo.NodeIDs.Count;

        for (int i = 0; i < countContour; i++)
        {
            OsmNode point = map.nodes[geo.NodeIDs[i]];

            Vector3 coords = point - localOrigin;

            otherCorners.Add(coords);
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

        var mesh = other.GetComponent<MeshFilter>().mesh;

        var tb = new MeshData();

        if (cur_type == other_type.power_wire)
        {
            other.GetComponent<MeshRenderer>().material.SetColor("_Color", Color.gray);
            GR.CreateMeshLineWithWidthAndHeight(otherCorners, 9.9f, 10.0f, 0.1f, tb);
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
            other.transform.gameObject.AddComponent<MeshCollider>();
            other.transform.GetComponent<MeshCollider>().sharedMesh = other.GetComponent<MeshFilter>().mesh;
            other.transform.GetComponent<MeshCollider>().convex = false;
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
            if (way.itemlist.Length > 0)
            {
                way.AddField("source_type", "way");
                CreateOtherObject(way);
            }

            yield return null;
        }

        foreach (var relation in map.relations.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Undefined && w.NodeIDs.Count > 1; }))
        {
            if (relation.itemlist.Length > 0)
            {
                relation.AddField("source_type", "relation");
                CreateOtherObject(relation);
            }

            yield return null;
        }

        isFinished = true;
    }

    public int GetCountProcessing()
    {
        return m_countProcessing;
    }
}
