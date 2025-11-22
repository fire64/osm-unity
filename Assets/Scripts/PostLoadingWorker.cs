using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

public class PostLoadingWorker : MonoBehaviour
{
    private Dictionary<Terrain, int[,]> terrainDetailsMap = new Dictionary<Terrain, int[,]>();

    float[] xymaxmin = new float[4];
    int TerrainDetailMapSize = 0;
    float PrPxSize = 0.0f;

    int detailHeight = 0;
    int detailWidth = 0;

    public bool IsWaterDeformationSupport = true;
    public bool IsVegetationSupport = true;

    public bool isPlaceGrass = false;
    public bool isPlaceTrees = false;


    public float mincheckSize = 15f;
    public float maxcheckSize = 25f;
    public bool isFastGrass = false;


    TileSystem tileSystem;

    // Start is called before the first frame update
    IEnumerator Start()
    {
        while (!IsAllModulesLoaded())
        {
            yield return null;
        }

        Debug.Log("All data loaded...");

        tileSystem = FindObjectOfType<TileSystem>();

        if (IsVegetationSupport)
        {
            CreateVegetation();
        }

        if (IsWaterDeformationSupport && tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            var foundWaterObjects = FindObjectsOfType<Water>();

            foreach (Water waterObj in foundWaterObjects)
            {
                WaterTerrainDeformer deformer = waterObj.gameObject.AddComponent<WaterTerrainDeformer>();

                deformer.ModifyTerrains();
            }
        }
    }

    void LoadDetailsMaps()
    {
        foreach (Terrain terrainCur in Terrain.activeTerrains)
        {
            terrainDetailsMap[terrainCur] = terrainCur.terrainData.GetDetailLayer(0, 0, terrainCur.terrainData.detailWidth, terrainCur.terrainData.detailHeight, 0);
        }

        Terrain terrain = Terrain.activeTerrain;

        TerrainDetailMapSize = terrain.terrainData.detailResolution;

        PrPxSize = TerrainDetailMapSize / terrain.terrainData.size.x;

        detailHeight = terrain.terrainData.detailHeight;
        detailWidth = terrain.terrainData.detailWidth;
    }

    void CreateGrass()
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            int num = terrain.terrainData.detailPrototypes.Length;

            for (int i = 0; i < num; i++)
            {
                terrain.terrainData.SetDetailLayer(0, 0, i, terrainDetailsMap[terrain]);
            }
        }
    }

    Terrain GetTerrainByCoord(Vector3 coordPoint)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            Vector3 terrainPos = terrain.transform.position;
            Vector3 terrainSize = Vector3.Scale(
                terrain.terrainData.size,
                terrain.transform.lossyScale
            );

            if (coordPoint.x >= terrainPos.x && coordPoint.x <= terrainPos.x + terrainSize.x &&
                coordPoint.z >= terrainPos.z && coordPoint.z <= terrainPos.z + terrainSize.z)
            {
                return terrain;
            }
        }
        return null;
    }

    public void AddGrass(Terrain terrain, Vector3 position, float radius)
    {
        int[,] map = terrainDetailsMap[terrain];

        Vector3 TexturePoint3D = position - terrain.transform.position;
        TexturePoint3D = TexturePoint3D * PrPxSize;

        xymaxmin[0] = TexturePoint3D.z + radius;
        xymaxmin[1] = TexturePoint3D.z - radius;
        xymaxmin[2] = TexturePoint3D.x + radius;
        xymaxmin[3] = TexturePoint3D.x - radius;

        int minY = (int)xymaxmin[3] + 1;
        minY = Math.Max(minY, 0);

        int maxY = (int)xymaxmin[2];
        maxY = Math.Min(maxY, detailHeight);

        int minX = (int)xymaxmin[1] + 1;
        minX = Math.Max(minX, 0);

        int maxX = (int)xymaxmin[0];
        maxX = Math.Min(maxX, detailWidth);

        for (int y = minY; y < maxY; y++)
        {
            for (int x = minX; x < maxX; x++)
            {
                map[x, y] = 200;
            }
        }

        terrainDetailsMap[terrain] = map;
    }

    void CreateTree(Terrain terrain, Vector3 worldPos)
    {
        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = Vector3.Scale(
            terrain.terrainData.size,
            terrain.transform.lossyScale
        );

        Vector3 normalizedPos = new Vector3(
            (worldPos.x - terrainPos.x) / terrainSize.x,
            0,
            (worldPos.z - terrainPos.z) / terrainSize.z
        );

        TreeInstance tree = new TreeInstance
        {
            position = normalizedPos,
            prototypeIndex = Random.Range(0, terrain.terrainData.treePrototypes.Length),
            widthScale = 1f,
            heightScale = 1f,
            color = Color.white,
            lightmapColor = Color.white
        };

        terrain.AddTreeInstance(tree);
        terrain.Flush();
    }

    private void CreateVegetation()
    {
        if (tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            CreateVegetationTerrain();
        }
        else
        {
            CreateVegetationToMesh();
        }
    }

    private void CreateVegetationToMesh()
    {
        var gameLanduses = FindObjectsByType<Landuse>(FindObjectsSortMode.None);

        foreach (var curLanduse in gameLanduses)
        {
            if (curLanduse.isGrassGenerate && isPlaceGrass)
            {
                CreateGrassOnMesh(curLanduse);
            }

            if ( curLanduse.isTreesGenerate && isPlaceTrees)
            {
                CreateTreesOnMesh(curLanduse);
            }
        }
    }

    private void CreateTreesOnMesh(Landuse curLanduse)
    {
        TreePlacer treePlacer = curLanduse.AddComponent<TreePlacer>();

        treePlacer.GenerateTree(curLanduse, tileSystem.grassSettings.TreesList.ToArray());
    }

    private void CreateGrassOnMesh(Landuse curLanduse)
    {
        var grassPlacer = new GameObject("grassplacer").AddComponent<GrassPlacer>();
        grassPlacer.transform.position = grassPlacer.transform.position;
        grassPlacer.transform.SetParent(curLanduse.transform, true);

        grassPlacer.GenerateGrass(curLanduse, tileSystem.grassSettings.GrassSettingsInfoList.ToArray());
    }

    private void CreateVegetationTerrain()
    {
        LoadDetailsMaps();

        var foundLanduseObjects = FindObjectsOfType<Landuse>();

        foreach (Landuse landuseObj in foundLanduseObjects)
        {
            if (landuseObj.isActiveAndEnabled && (landuseObj.isGrassGenerate || landuseObj.isTreesGenerate) )
            {
                MeshFilter _meshFilter = landuseObj.GetComponent<MeshFilter>();
                Collider _collider = landuseObj.GetComponent<Collider>();

                if (!_meshFilter || !_collider)
                {
                    Debug.LogError("Missing components on: " + name);
                    continue;
                }

                Bounds worldBounds = _meshFilter.sharedMesh.bounds;
                worldBounds.center = landuseObj.transform.TransformPoint(worldBounds.center);
                worldBounds.extents = landuseObj.transform.TransformVector(worldBounds.extents);

                float minX = worldBounds.min.x;
                float maxX = worldBounds.max.x;
                float minZ = worldBounds.min.z;
                float maxZ = worldBounds.max.z;

                if (landuseObj.isGrassGenerate)
                {
                    float checkSize = 0.5f;

                    if (isFastGrass)
                    {
                        checkSize = 1f;
                    }

                    float grassRadius = 2.1f;

                    if (isFastGrass)
                    {
                        grassRadius = 2.2f;
                    }

                    for (float x = minX; x < maxX; x += checkSize)
                    {
                        for (float z = minZ; z < maxZ; z += checkSize)
                        {
                            Vector3 origin = new Vector3(x, 100000, z);
                            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 200000))
                            {
                                Landuse landuse = hit.collider.GetComponent<Landuse>();
                                if (landuse != null && landuse.Name == landuseObj.Name)
                                {
                                    Terrain terrain = GetTerrainByCoord(hit.point);
                                    if (terrain != null)
                                        AddGrass(terrain, hit.point, grassRadius);
                                }
                            }
                        }
                    }
                }

                if (landuseObj.isTreesGenerate)
                {
                    for (float x = minX; x < maxX; x += UnityEngine.Random.Range(mincheckSize, maxcheckSize))
                    {
                        for (float z = minZ; z < maxZ; z += UnityEngine.Random.Range(mincheckSize, maxcheckSize))
                        {
                            Vector3 origin = new Vector3(x, 100000, z);
                            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 200000))
                            {
                                Landuse landuse = hit.collider.GetComponent<Landuse>();
                                if (landuse != null && landuse.Name == landuseObj.Name)
                                {
                                    Terrain terrain = GetTerrainByCoord(hit.point);
                                    if (terrain != null)
                                        CreateTree(terrain, hit.point);
                                }
                            }
                        }
                    }
                }
            }
        }

        CreateGrass();
    }

    private bool IsAllModulesLoaded()
    {
        var foundInfrstructureObjects = FindObjectsOfType<InfrstructureBehaviour>();

        Debug.Log( "Found: " + foundInfrstructureObjects.Length);

        foreach (InfrstructureBehaviour InfrstructureObjects in foundInfrstructureObjects)
        {
            if(!InfrstructureObjects.isFinished && InfrstructureObjects.isActiveAndEnabled)
            {
                return false;
            }
        }

        return true;
    }

}
