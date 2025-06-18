using System.Collections;
using System.Collections.Generic;
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

    public BuildingTypes buildingTypes;
    public BuildingMaterials buildingMaterials;
    public BuildingSeries buildingSeries;
    public bool bNotCreateNotClosedPolygon;

    public bool isUseOldTriangulation = false;
    public bool isCreateColision = false;

    public int MaxNodes = 150;

    public TileSystem tileSystem;

    public GameObject smokeprefab;
    public Material windowMaterial;
    public float tolerance = 0.1f;
    private float GetHeights(BaseOsm geo, Building building)
    {
        var height = 0.0f;

        var min_height = 0.0f;

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
        else if (geo.HasField("man_made"))
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
            else if(man_made_type == "chimney")
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
        else if(building.curSettings.defaultHeight > 0.0f)
        {
            height = building.curSettings.defaultHeight;
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

    private float GetMinHeight(BaseOsm geo, Building building)
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

        building.curSettings = buildingTypes.GetBuildingTypeInfoByName(building.Kind);

        if (geo.HasField("source_type"))
            building.Source = geo.GetValueStringByKey("source_type");

        Material mat_by_type = null;
        Material mat_by_tag = null;

        if (!kind.Equals("yes"))
        {
            mat_by_type = building.curSettings.buildingMaterial;
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

        if (mat_by_type != null)
        {
            building.GetComponent<MeshRenderer>().material = mat_by_type;
        }
        else if (geo.HasField("building:material") && mat_by_tag != null)
        {
            building.GetComponent<MeshRenderer>().material = mat_by_tag;
        }
        else if (geo.HasField("building:material") && mat_by_tag == null)
        {
            //not set for debug
        }
        else
        {
            //Add default material
            building.GetComponent<MeshRenderer>().material = building_material;
        }

        UnityEngine.Color curColor = GR.SetOSMColour(geo);

        if(curColor != UnityEngine.Color.white)
        {
            building.GetComponent<MeshRenderer>().material.SetColor("_Color", curColor);
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

        var count = geo.NodeIDs.Count;

        if(count > MaxNodes)
        {
            Debug.LogError(searchname + " haved " + count + " nodes.");
            return;
        }

        var building = new GameObject(searchname).AddComponent<Building>();

        building.AddComponent<MeshFilter>();
        building.AddComponent<MeshRenderer>();

        building.itemlist = geo.itemlist;
        building.count = count;

        SetProperties(geo, building);

        var height = GetHeights(geo, building);
        var minHeight = GetMinHeight(geo, building);

        building.height = height;
        building.min_height = minHeight;

        var buildingCorners = new List<Vector3>();

        float minx = float.MaxValue, miny = float.MaxValue, maxx = float.MinValue, maxy = float.MinValue;

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

        if (geo.HasField("design:ref") /*&& count == 5*/) //ony for box
        {
            var series = geo.GetValueStringByKey("design:ref");

            var floors = 0.0f;

            if (geo.HasField("building:levels"))
            {
                floors = geo.GetValueFloatByKey("building:levels");
            }
            else if (geo.HasField("building:height"))
            {
                floors = geo.GetValueFloatByKey("building:height") / 3.0f;
            }

            var entrances = 0.0f;

            if (geo.HasField("building:flats"))
            {
                var flats = geo.GetValueFloatByKey("building:flats");

                entrances = flats / floors / 4.0f;
            }

            BuildingSeries.BuildingSeriesReplace curSeries = buildingSeries.GetBuildingSeriesInfo(series, (int)floors, (int)entrances);

            if (curSeries.buildingmodel != null)
            {
/*
                // Создаем экземпляр модели
                GameObject buildingModel = Instantiate(
                    curSeries.buildingmodel,
                    building.transform.position,
                    building.transform.rotation
                );
                buildingModel.name = "AlignedBuildingModel";*/
            }
        }

        //Add colider 
        if (isCreateColision)
        {
            building.transform.gameObject.AddComponent<MeshCollider>();
            building.transform.GetComponent<MeshCollider>().sharedMesh = building.GetComponent<MeshFilter>().mesh;
            building.transform.GetComponent<MeshCollider>().convex = false;
        }

        if (!contentselector.isRoofDisabled(geo.ID) && !geo.HasField("man_made"))
        {
            generateRoof.GenerateRoofForBuillding(building, buildingCorners, holesCorners, minHeight, height, new Vector2(minx, miny), new Vector2(maxx - minx, maxy - miny), geo, isUseOldTriangulation);
        }

        //Add smoke
        if (geo.HasField("man_made") && geo.GetValueStringByKey("man_made").Equals("chimney") )
        {
            var go = Instantiate(smokeprefab, building.transform.position + (Vector3.up * (building.height - 0.20f)), Quaternion.identity);
            go.transform.localScale = new Vector3(25.0f, 25.0f, 25.0f);
            go.transform.Rotate(new Vector3(-90, 0, 0));

            go.transform.SetParent(building.transform);
        }

        if(windowMaterial != null && building.curSettings.isUseWindows)
        {
            var WindowPlacer = new GameObject("WindowPlacer");
            WindowPlacer.transform.position = building.transform.position;
            WindowPlacer.transform.SetParent(building.transform);
            WindowPlacer.transform.localScale = new Vector3(1.001f, 1f, 1.001f);

            WindowPlacer.AddComponent<MeshFilter>().mesh = building.GetComponent<MeshFilter>().mesh;
            WindowPlacer.AddComponent<MeshRenderer>().material = windowMaterial;
            WindowPlacer.AddComponent<WindowPlacerOptimized>();
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