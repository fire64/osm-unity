using Mono.Cecil;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;

class ManMadeMaker : InfrstructureBehaviour
{
    public Material manade_material;

    public GenerateRoof generateRoof;

    public static GameContentSelector contentselector;

    public ManMadeTypes manmadeTypes;

    public bool isUseOldTriangulation = false;
    public bool isCreateColision = false;

    public int MaxNodes = 150;

    private int m_countProcessing = 0;

    public TileSystem tileSystem;

    public GameObject smokeprefab;

    // Start is called before the first frame update
    IEnumerator Start()
    {
        while (!map.IsReady)
        {
            yield return null;
        }

        contentselector = FindObjectOfType<GameContentSelector>();

        tileSystem = FindObjectOfType<TileSystem>();

        foreach (var way in map.ways.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.ManMade && w.NodeIDs.Count > 1; }))
        {
            way.AddField("source_type", "way");

            if (way.IsClosedPolygon)
            {
                CreateManMadePolygon(way);
            }
            else
            {
                CreateManMadeLine(way);
            }
            
            yield return null;
        }

        foreach (var relation in map.relations.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.ManMade && w.NodeIDs.Count > 1; }))
        {
            relation.AddField("source_type", "relation");

            if (relation.IsClosedPolygon)
            {
                CreateManMadePolygon(relation);
            }
            else
            {
                CreateManMadeLine(relation);
            }

            yield return null;
        }

        isFinished = true;
    }

    private float GetHeights(BaseOsm geo, ManMadeObj building)
    {
        float height;

        if (geo.HasField("height"))
        {
            height = geo.GetValueFloatByKey("height");
        }
        else if (geo.HasField("building:height"))
        {
            height = geo.GetValueFloatByKey("building:height");
        }
        else if (geo.HasField("building:levels"))
        {
            height = geo.GetValueFloatByKey("building:levels") * 3.0f;
        }
        else
        {
            var man_made_type = geo.GetValueStringByKey("man_made");

            if (man_made_type == "tower")
            {
                height = 100.0f;
            }
            else if (man_made_type == "reservoir_covered")
            {
                height = 10;
            }
            else if (man_made_type == "chimney")
            {
                height = 100.0f;
            }
            else if (man_made_type == "silo")
            {
                height = 1.75f;
            }
            else
            {
                height = 1.5f;
            }
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

    private void SetProperties(BaseOsm geo, ManMadeObj manmade, bool isPolygon)
    {
        if(isPolygon)
        {
            manmade.name = "manmade_polygon " + geo.ID.ToString();
        }
        else
        {
            manmade.name = "manmade_line " + geo.ID.ToString();
        }

        if (geo.HasField("name"))
            manmade.Name = geo.GetValueStringByKey("name");

        manmade.Id = geo.ID.ToString();

        string kind;

        if (geo.HasField("man_made"))
        {
            kind = geo.GetValueStringByKey("man_made");
        }
        else
        {
            kind = "yes";
        }

        manmade.Kind = kind;

        manmade.width = 2.0f;

        manmade.curSettings = manmadeTypes.GetTypeInfoByName(manmade.Kind);

        if (geo.HasField("source_type"))
            manmade.Source = geo.GetValueStringByKey("source_type");

        Material mat_by_type = null;

        if (!kind.Equals("yes"))
        {
            mat_by_type = manmade.curSettings.defaultMaterial;
        }

        if (geo.HasField("location"))
        {
            var location = geo.GetValueStringByKey("location");

            if( location == "overground")
            {
                manmade.layer = 1;
            }
            else if (location == "surface")
            {
                manmade.layer = 0;
            }
            else if (location == "underground")
            {
                manmade.layer = -1;
            }
            else if (geo.HasField("layer"))
            {
                manmade.layer = geo.GetValueIntByKey("layer");
            }
        }
        else if (geo.HasField("layer"))
        {
            manmade.layer = geo.GetValueIntByKey("layer");
        }

        if (mat_by_type != null)
        {
            manmade.GetComponent<MeshRenderer>().material = mat_by_type;
        }
        else
        {
            //Add default material
            manmade.GetComponent<MeshRenderer>().material = manade_material;
        }

        Color curColor = GR.SetOSMColour(geo);

        if (curColor != Color.white)
        {
            manmade.GetComponent<MeshRenderer>().material.SetColor("_Color", curColor);
        }
    }

    void CreateManMadePolygon(BaseOsm geo)
    {
        var searchname = "manmade_polygon " + geo.ID.ToString();

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

        var count = geo.NodeIDs.Count;

        if (count > MaxNodes)
        {
            Debug.LogError(searchname + " haved " + count + " nodes.");
            return;
        }

        var manmade = new GameObject(searchname).AddComponent<ManMadeObj>();

        manmade.AddComponent<MeshFilter>();
        manmade.AddComponent<MeshRenderer>();

        manmade.itemlist = geo.itemlist;
        manmade.count = count;

        SetProperties(geo, manmade, true);

        var height = GetHeights(geo, manmade);
        var minHeight = GetMinHeight(geo);

        manmade.height = height;
        manmade.min_height = minHeight;

        var manmadeCorners = new List<Vector3>();

        float minx = float.MaxValue, miny = float.MaxValue, maxx = float.MinValue, maxy = float.MinValue;

        Vector3 localOrigin = GetCentre(geo);
        manmade.transform.position = localOrigin - map.bounds.Centre;

        if (tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            if (tileSystem.isUseElevation)
            {
                manmade.transform.position = GR.getHeightPosition(manmade.transform.position);
            }
        }

        manmade.transform.position += Vector3.up * (manmade.layer * BaseDataObject.layer_size);

        for (int i = 0; i < count; i++)
        {
            OsmNode point = map.nodes[geo.NodeIDs[i]];

            Vector3 coords = point - localOrigin;

            if (coords.x < minx) minx = (float)coords.x;
            if (coords.z < miny) miny = (float)coords.z;
            if (coords.x > maxx) maxx = (float)coords.x;
            if (coords.z > maxy) maxy = (float)coords.z;

            manmadeCorners.Add(coords);
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

        var mesh = manmade.GetComponent<MeshFilter>().mesh;

        var tb = new MeshData();

        if (isUseOldTriangulation)
        {
            GR.CreateMeshWithHeightOld(manmadeCorners, minHeight, height, tb);
        }
        else
        {
            GR.CreateMeshWithHeight(manmadeCorners, minHeight, height, tb, holesCorners);
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
            manmade.transform.gameObject.AddComponent<MeshCollider>();
            manmade.transform.GetComponent<MeshCollider>().sharedMesh = manmade.GetComponent<MeshFilter>().mesh;
            manmade.transform.GetComponent<MeshCollider>().convex = false;
        }

        var man_made_type = geo.GetValueStringByKey("man_made");

        //Add smoke
        if (man_made_type.Equals("chimney"))
        {
            var go = Instantiate(smokeprefab, manmade.transform.position + (Vector3.up * (manmade.height - 0.20f)), Quaternion.identity);
            go.transform.localScale = new Vector3(25.0f, 25.0f, 25.0f);
            go.transform.Rotate(new Vector3(-90, 0, 0));
            go.transform.SetParent(manmade.transform);
        }
        else if (man_made_type.Equals("bridge"))
        {
            manmade.transform.position = new Vector3(manmade.transform.position.x, manmade.transform.position.y - manmade.height, manmade.transform.position.z);
        }


        bool isGenerateRoof = true;

        if (isGenerateRoof)
        {
            if (contentselector.isRoofDisabled(geo.ID))
            {
                isGenerateRoof = false;
            }
            else
            {
                if (!geo.HasField("roof:shape") && !geo.HasField("roof:colour") && !geo.HasField("roof:height"))
                {
                    isGenerateRoof = false;
                }
            }
        }

        if (isGenerateRoof)
        {
            generateRoof.GenerateRoofForObject(manmade, manmadeCorners, holesCorners, minHeight, height, new Vector2(minx, miny), new Vector2(maxx - minx, maxy - miny), geo, isUseOldTriangulation);
        }



    }

    void CreateManMadeLine(BaseOsm geo)
    {
        var searchname = "manmade_line " + geo.ID.ToString();

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

        var count = geo.NodeIDs.Count;

        if (count > MaxNodes)
        {
            Debug.LogError(searchname + " haved " + count + " nodes.");
            return;
        }

        var manmade = new GameObject(searchname).AddComponent<ManMadeObj>();

        manmade.AddComponent<MeshFilter>();
        manmade.AddComponent<MeshRenderer>();

        manmade.itemlist = geo.itemlist;
        manmade.count = count;

        SetProperties(geo, manmade, false);

        var height = GetHeights(geo, manmade);
        var minHeight = GetMinHeight(geo);

        manmade.height = height;
        manmade.min_height = minHeight;

        var manmadeCorners = new List<Vector3>();

        Vector3 localOrigin = GetCentre(geo);
        manmade.transform.position = localOrigin - map.bounds.Centre;

        if (tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            if (tileSystem.isUseElevation)
            {
                manmade.transform.position = GR.getHeightPosition(manmade.transform.position);
            }
        }

        manmade.transform.position += Vector3.up * (manmade.layer * BaseDataObject.layer_size);

        for (int i = 0; i < count; i++)
        {
            OsmNode point = map.nodes[geo.NodeIDs[i]];

            Vector3 coords = point - localOrigin;

            manmadeCorners.Add(coords);
        }

        var mesh = manmade.GetComponent<MeshFilter>().mesh;

        var tb = new MeshData();

        GR.CreateMeshLineWithWidthAndHeight(manmadeCorners, manmade.height, manmade.min_height, manmade.width, tb);

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
            manmade.transform.gameObject.AddComponent<MeshCollider>();
            manmade.transform.GetComponent<MeshCollider>().sharedMesh = manmade.GetComponent<MeshFilter>().mesh;
            manmade.transform.GetComponent<MeshCollider>().convex = false;
        }
    }

    public int GetCountProcessing()
    {
        return m_countProcessing;
    }
}
