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

    private void SetProperties(OsmWay geo, Building building)
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

        building.GetComponent<MeshRenderer>().material.SetColor("_Color", GR.SetOSMColour(geo));
    }

    IEnumerator Start()
    {        
        while (!map.IsReady)
        {
            yield return null;
        }

        foreach (var way in map.ways.FindAll((w) => { return w.IsBuilding && w.NodeIDs.Count > 1; }))
        {
            var searchname = "building " + way.ID.ToString();

            //Check for duplicates in case of loading multiple locations
            if (GameObject.Find(searchname))
            {
                continue;
            }

            //Check on parts of a complex building if their creation is prohibited.
            if (way.HasField("building:part") && way.GetValueStringByKey("building:part").Equals("yes") && bNotCreateOSMParts)
            {
                continue;
            }

            var building = new GameObject(searchname).AddComponent<Building>();

            building.itemlist = way.itemlist;

            MeshFilter mf = building.AddComponent<MeshFilter>();
            MeshRenderer mr = building.AddComponent<MeshRenderer>();
            mr.material = building_material;

            SetProperties(way, building);

            var height = GetHeights(way);
            var minHeight = GetMinHeight(way);

            building.Height = height;
            building.MinHeight = minHeight;

            Vector3 localOrigin = GetCentre(way);
            building.transform.position = localOrigin - map.bounds.Centre;

            List<Vector3> vectors = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<int> indices = new List<int>();

            for (int i = 1; i < way.NodeIDs.Count; i++)
            {
                OsmNode p1 = map.nodes[way.NodeIDs[i - 1]];
                OsmNode p2 = map.nodes[way.NodeIDs[i]];

                Vector3 v1 = p1 - localOrigin + new Vector3(0, minHeight, 0);
                Vector3 v2 = p2 - localOrigin + new Vector3(0, minHeight, 0);
                Vector3 v3 = p1 - localOrigin + new Vector3(0, height, 0);
                Vector3 v4 = p2 - localOrigin + new Vector3(0, height, 0);

                vectors.Add(v1);
                vectors.Add(v2);
                vectors.Add(v3);
                vectors.Add(v4);

                normals.Add(-Vector3.forward);
                normals.Add(-Vector3.forward);
                normals.Add(-Vector3.forward);
                normals.Add(-Vector3.forward);
                
                // index values
                int idx1, idx2,idx3, idx4;
                idx4 = vectors.Count - 1;
                idx3 = vectors.Count - 2;
                idx2 = vectors.Count - 3;
                idx1 = vectors.Count - 4;

                // first triangle v1, v3, v2
                indices.Add(idx1);
                indices.Add(idx3);
                indices.Add(idx2);

                // second triangle v3, v4, v2
                indices.Add(idx3);
                indices.Add(idx4);
                indices.Add(idx2);

                // third triangle v2, v3, v1
                indices.Add(idx2);
                indices.Add(idx3);
                indices.Add(idx1);

                // fourth triangle v2, v4, v3
                indices.Add(idx2);
                indices.Add(idx4);
                indices.Add(idx3);
            }

            mf.mesh.vertices = vectors.ToArray();
            mf.mesh.normals = normals.ToArray();
            mf.mesh.triangles = indices.ToArray();

            yield return null;
        }

    }

}