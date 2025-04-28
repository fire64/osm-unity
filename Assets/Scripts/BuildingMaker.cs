using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;

class BuildingMaker : InfrstructureBehaviour
{
    public Material building_material;
    public bool bNotCreateOSMParts;

    public float MinRandHeight = 3.0f;
    public float MaxRandHeight = 21.0f;

    public GenerateRoof generateRoof;

    public static GameContentSelector contentselector;

    public BuildingTypeMaterials buildingTypes;
    public BuildingMaterials buildingMaterials;
    public bool bNotCreateNotClosedPolygon;

    public bool isUseOldTriangulation = false;
    public bool isCreateColision = false;

    public TileSystem tileSystem;

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

        Material mat_by_type = null;
        Material mat_by_tag = null;

        if (!kind.Equals("yes"))
        {
            mat_by_type = buildingTypes.GetBuildingTypeMaterialByName(kind);
        }

        if (geo.HasField("building:material"))
        {
            var mat_name = geo.GetValueStringByKey("building:material");

            mat_by_tag = buildingMaterials.GetBuildingMaterialByName(mat_name);
        }

        if (geo.HasField("layer"))
        {
            building.layer = geo.GetValueIntByKey("layer");
        }

        if (kind == "apartments")
        {
            //TODO: Add default material for apartments buildings
        }
        else if (mat_by_type != null)
        {
            building.GetComponent<MeshRenderer>().material = mat_by_type;
        }
        else if (geo.HasField("building:material") && mat_by_tag != null)
        {
            building.GetComponent<MeshRenderer>().material = mat_by_tag;
        }
        else if (geo.HasField("building:material") && mat_by_tag == null)
        {
            building.GetComponent<MeshRenderer>().material = null;
        }
        else
        {
            //TODO: Add default material for apartments buildings
        }

        building.GetComponent<MeshRenderer>().material.SetColor("_Color", GR.SetOSMColour(geo));
    }

    void CreateBuilding(BaseOsm geo)
    {
        var searchname = "building " + geo.ID.ToString();

        //Check for duplicates in case of loading multiple locations
        if (GameObject.Find(searchname))
        {
            return;
        }

        if (contentselector.isGeoObjectDisabled(geo.ID))
        {
            return;
        }

        //Check on parts of a complex building if their creation is prohibited.
        if (geo.HasField("building:part") && geo.GetValueStringByKey("building:part").Equals("yes") && bNotCreateOSMParts)
        {
            return;
        }

        if(!geo.IsClosedPolygon && bNotCreateNotClosedPolygon)
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

        building.height = height;
        building.min_height = minHeight;

        var buildingCorners = new List<Vector3>();

        float minx = float.MaxValue, miny = float.MaxValue, maxx = float.MinValue, maxy = float.MinValue;

        var count = geo.NodeIDs.Count;

        Vector3 localOrigin = GetCentre(geo);
        building.transform.position = localOrigin - map.bounds.Centre;

        if (tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            if (tileSystem.isUseElevation)
            {
                building.transform.position = GR.getHeightPosition(building.transform.position);
            }
        }

        building.transform.position += Vector3.up * (building.layer * BaseDataObject.layer_size);

        for (int i = 0; i < count; i++)
        {
            OsmNode point = map.nodes[geo.NodeIDs[i]];

            Vector3 coords = point - localOrigin;

            if (coords.x < minx) minx = (float)coords.x;
            if (coords.z < miny) miny = (float)coords.z;
            if (coords.x > maxx) maxx = (float)coords.x;
            if (coords.z > maxy) maxy = (float)coords.z;

            buildingCorners.Add(coords);
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

        var mesh = building.GetComponent<MeshFilter>().mesh;

        var tb = new MeshData();

        if(isUseOldTriangulation)
        {
            GR.CreateMeshWithHeightOld(buildingCorners, minHeight, height, tb);
        }
        else
        {
            GR.CreateMeshWithHeight(buildingCorners, minHeight, height, tb, holesCorners);
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
            building.transform.gameObject.AddComponent<MeshCollider>();
            building.transform.GetComponent<MeshCollider>().sharedMesh = building.GetComponent<MeshFilter>().mesh;
            building.transform.GetComponent<MeshCollider>().convex = false;
        }

        if (!contentselector.isRoofDisabled(geo.ID) && !geo.HasField("man_made"))
        {
            generateRoof.GenerateRoofForBuillding(building.gameObject, buildingCorners, holesCorners, minHeight, height, new Vector2(minx, miny), new Vector2(maxx - minx, maxy - miny), geo, isUseOldTriangulation);
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

        foreach (var way in map.ways.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Building && w.NodeIDs.Count > 1; }))
        {
            way.AddField("source_type", "way");
            CreateBuilding(way);
            yield return null;
        }

        foreach (var relation in map.relations.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Building && w.NodeIDs.Count > 1; }))
        {
            relation.AddField("source_type", "relation");
            CreateBuilding(relation);
            yield return null;
        }

        isFinished = true;
    }

}