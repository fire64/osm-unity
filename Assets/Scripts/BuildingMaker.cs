using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

class BuildingMaker : InfrstructureBehaviour
{
    public Material building_material;
    public bool bNotCreateOSMParts;

    public bool isUseHeightFix = true;

    public float MinRandHeight = 3.0f;
    public float MaxRandHeight = 21.0f;

    private float GetHeights(BaseOsm geo)
    {
        var height = 0.0f;

        var min_height = 0.0f;

        if (geo.HasField("height"))
        {
            height = geo.GetValueFloatByKey("height");
        }
        else if (geo.HasField("building:levels"))
        {
            height = geo.GetValueFloatByKey("building:levels") * 3.0f;
        }
        else if (geo.HasField("man_made"))
        {
            var man_made_type = geo.GetValueStringByKey("man_made");

            if (man_made_type == "tower")
            {
                height = 200.0f;
            }
        }
        else
        {
            height = UnityEngine.Random.Range(MinRandHeight, MaxRandHeight);
        }

        if (geo.GetValueStringByKey("kind") == "pier")
        {
            height = 0.1f;
        }
        else if (geo.GetValueStringByKey("kind") == "bridge")
        {
            height = 0.1f;
        }

        if (geo.HasField("min_height"))
        {
            min_height = geo.GetValueFloatByKey("min_height");
        }
        else if (geo.HasField("building:min_level"))
        {
            min_height = geo.GetValueFloatByKey("building:min_level") * 3.0f;
        }

        if (isUseHeightFix)
        {
            height -= min_height;
        }

        return height;
    }

    private float GetMinHeight(BaseOsm geo)
    {
        var min_height = 0.0f;

        if (geo.HasField("min_height"))
        {
            min_height = geo.GetValueFloatByKey("min_height");
        }
        else if (geo.HasField("building:min_level"))
        {
            min_height = geo.GetValueFloatByKey("building:min_level") * 3.0f;
        }

        //Level correction
        return min_height;
    }

    private void SetProperties(BaseOsm geo, Building building)
    {
        building.name = "building " + geo.ID.ToString();
		
        if (geo.HasField("name"))
            building.Name = geo.GetValueStringByKey("name");		
		
        building.Id = geo.ID.ToString();

        var kind = "";

        if (geo.HasField("building"))
        {
            kind = geo.GetValueStringByKey("building");
        }
        else
        {
            kind = "yes";
        }

        building.Kind = kind;

        if (geo.HasField("source_type"))
            building.Source = geo.GetValueStringByKey("source_type");

        building.GetComponent<MeshRenderer>().material.SetColor("_Color", GR.SetOSMColour(geo));
    }

    private void CreateMesh(List<Vector3> corners, float min_height, float height, MeshData data, Vector2 min, Vector2 size)
    {
        // Create bottom face
        for (int i = 0; i < corners.Count; i++)
        {
            data.Vertices.Add(corners[i] + new Vector3(0, min_height, 0));
            data.Normals.Add(-Vector3.forward);
        }
        for (int i = 2; i < corners.Count; i++)
        {
            data.Indices.Add(0);
            data.Indices.Add(i - 1);
            data.Indices.Add(i);
        }

        for (int i = 1; i < corners.Count; i++)
        {
            Vector3 p1 = corners[i - 1];
            Vector3 p2 = corners[i];

            Vector3 v1 = p1 + new Vector3(0, min_height, 0);
            Vector3 v2 = p2 + new Vector3(0, min_height, 0);
            Vector3 v3 = p1 + new Vector3(0, min_height + height, 0);
            Vector3 v4 = p2 + new Vector3(0, min_height + height, 0);

            data.Vertices.Add(v3);
            data.Vertices.Add(v4);

            data.Normals.Add(-Vector3.forward);
            data.Normals.Add(-Vector3.forward);

            // index values
            int idx1 = i - 1;
            int idx2 = i;
            int idx3 = data.Vertices.Count - 2;
            int idx4 = data.Vertices.Count - 1;

            // first triangle v1, v3, v2
            data.Indices.Add(idx1);
            data.Indices.Add(idx3);
            data.Indices.Add(idx2);

            // second triangle v3, v4, v2
            data.Indices.Add(idx3);
            data.Indices.Add(idx4);
            data.Indices.Add(idx2);

            // third triangle v2, v3, v1
            data.Indices.Add(idx2);
            data.Indices.Add(idx3);
            data.Indices.Add(idx1);

            // fourth triangle v2, v4, v3
            data.Indices.Add(idx2);
            data.Indices.Add(idx4);
            data.Indices.Add(idx3);
        }

        // Create top face
        int topOffset = data.Vertices.Count;
        for (int i = 0; i < corners.Count; i++)
        {
            data.Vertices.Add(corners[i] + new Vector3(0, min_height + height, 0));
            data.Normals.Add(-Vector3.forward);
        }

        for (int i = 2; i < corners.Count; i++)
        {
            data.Indices.Add(topOffset + 0);
            data.Indices.Add(topOffset + i);
            data.Indices.Add(topOffset + i - 1); // Обратный порядок индексов для верхней грани
        }

        for (int i = 2; i < corners.Count; i++) //fix for backfaces
        {
            data.Indices.Add(topOffset + i - 1); // Обратный порядок индексов для верхней грани
            data.Indices.Add(topOffset + i);
            data.Indices.Add(topOffset + 0);
        }

    }

    void CreateBuilding(BaseOsm geo)
    {
        var searchname = "building " + geo.ID.ToString();

        //Check for duplicates in case of loading multiple locations
        if (GameObject.Find(searchname))
        {
            return;
        }

        //Check on parts of a complex building if their creation is prohibited.
        if (geo.HasField("building:part") && geo.GetValueStringByKey("building:part").Equals("yes") && bNotCreateOSMParts)
        {
            return;
        }

        var building = new GameObject(searchname).AddComponent<Building>();

        building.AddComponent<MeshFilter>();
        building.AddComponent<MeshRenderer>();

        building.itemlist = geo.itemlist;

        SetProperties(geo, building);

        var height = GetHeights(geo);
        var minHeight = GetMinHeight(geo);

        building.Height = height;
        building.MinHeight = minHeight;

        var buildingCorners = new List<Vector3>();

        float minx = float.MaxValue, miny = float.MaxValue, maxx = float.MinValue, maxy = float.MinValue;

        var count = geo.NodeIDs.Count;

        Vector3 localOrigin = GetCentre(geo);
        building.transform.position = localOrigin - map.bounds.Centre;

        for (int i = 0; i < count; i++)
        {
            OsmNode point = map.nodes[geo.NodeIDs[i]];

            Vector3 coords = point - localOrigin;

            if (coords.x < minx) minx = (float)coords.x;
            if (coords.y < miny) miny = (float)coords.y;
            if (coords.x > maxx) maxx = (float)coords.x;
            if (coords.y > maxy) maxy = (float)coords.y;

            buildingCorners.Add(coords);
        }

        var mesh = building.GetComponent<MeshFilter>().mesh;

        var tb = new MeshData();

        CreateMesh(buildingCorners, minHeight, height, tb, new Vector2(minx, miny), new Vector2(maxx - minx, maxy - miny));

        mesh.vertices = tb.Vertices.ToArray();
        mesh.triangles = tb.Indices.ToArray();
        mesh.normals = tb.Normals.ToArray();
        mesh.SetUVs(0, tb.UV);

        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
//      mesh.RecalculateNormals(); //TODO: Fix calculating lightmaps

        //Add colider 
//TODO: fix error or add check
/*
        building.transform.gameObject.AddComponent<MeshCollider>();
        building.transform.GetComponent<MeshCollider>().sharedMesh = building.GetComponent<MeshFilter>().mesh;
        building.transform.GetComponent<MeshCollider>().convex = false;
*/
    }

    IEnumerator Start()
    {        
        while (!map.IsReady)
        {
            yield return null;
        }

        foreach (var way in map.ways.FindAll((w) => { return w.IsBuilding && w.IsClosedPolygon && w.NodeIDs.Count > 1; }))
        {
            way.AddField("source_type", "way");
            CreateBuilding(way);
            yield return null;
        }

        foreach (var relation in map.relations.FindAll((w) => { return w.IsBuilding && w.IsClosedPolygon && w.NodeIDs.Count > 1; }))
        {
            relation.AddField("source_type", "relation");
            CreateBuilding(relation);
            yield return null;
        }

    }

}