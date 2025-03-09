using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainTools : MonoBehaviour
{
    public enum TypeTerrainModife
    {
        AddGrass,
        CutGrass,
        AddTree,
        CutTree,
        AddHeight,
        AddHole,
    }

    public TypeTerrainModife PrimaryFunc;

    public TypeTerrainModife SecondaryFunc;

    protected float m_NextPrimaryAttack = 0.0f;
    public float m_PrimaryAttackInterval = 0.5f;

    // Start is called before the first frame update
    void Start()
    {

    }

    public void AddGrass(Terrain terrain, Vector3 position, float radius)
    {
        int[,] map = terrain.terrainData.GetDetailLayer(0, 0, terrain.terrainData.detailWidth, terrain.terrainData.detailHeight, 0);

        int TerrainDetailMapSize = terrain.terrainData.detailResolution;

        if (terrain.terrainData.size.x != terrain.terrainData.size.z)
        {
            Debug.Log("terrain.terrainData.size.x != terrain.terrainData.size.z");
        //    return;
        }

        float PrPxSize = TerrainDetailMapSize / terrain.terrainData.size.x;

        float[] xymaxmin = new float[4];

        int detailHeight = terrain.terrainData.detailHeight;
        int detailWidth = terrain.terrainData.detailWidth;

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

        int num = terrain.terrainData.detailPrototypes.Length;

        Debug.Log("terrain.terrainData.detailPrototypes.Length = " + num);

        for (int i = 0; i < num; i++)
        {
            terrain.terrainData.SetDetailLayer(0, 0, i, map);
        }
    }

    public void CutGrass(Terrain terrain, Vector3 position, float radius)
    {
        int[,] map = terrain.terrainData.GetDetailLayer(0, 0, terrain.terrainData.detailWidth, terrain.terrainData.detailHeight, 0);

        int TerrainDetailMapSize = terrain.terrainData.detailResolution;

        if (terrain.terrainData.size.x != terrain.terrainData.size.z)
        {
            return;
        }

        float PrPxSize = TerrainDetailMapSize / terrain.terrainData.size.x;

        float[] xymaxmin = new float[4];

        int detailHeight = terrain.terrainData.detailHeight;
        int detailWidth = terrain.terrainData.detailWidth;

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
                map[x, y] = 0;
            }
        }

        int num = terrain.terrainData.detailPrototypes.Length;

        for (int i = 0; i < num; i++)
        {
            terrain.terrainData.SetDetailLayer(0, 0, i, map);
        }
    }

    public void AddTrees(Terrain terrain, Vector3 position, float radius)
    {
        Debug.Log("CreateDepression");

        Vector3 terraincenter = terrain.transform.position;

        Vector3 hitpoint = position;

        Vector3 rangepoint = hitpoint - terraincenter;

        float terrainsize = terrain.terrainData.size.x;

        Vector3 terrpoint = rangepoint / terrainsize;

        int cout_trees = terrain.terrainData.treePrototypes.Length;

        TreeInstance treeTemp = new TreeInstance();
        treeTemp.position = terrpoint;
        treeTemp.prototypeIndex = UnityEngine.Random.Range(0, cout_trees);
        treeTemp.widthScale = 1f;
        treeTemp.heightScale = 1f;
        treeTemp.color = Color.white;
        treeTemp.lightmapColor = Color.white;
        terrain.AddTreeInstance(treeTemp);
        terrain.Flush();
    }

    public void CreateDepression(Terrain terrain, Vector3 position, float depth, float radius)
    {
        Debug.Log("CreateDepression");

        int heightmapResolution = terrain.terrainData.heightmapResolution;

        // ѕеревести глобальные координаты точки в локальные координаты Terrain
        Vector3 terrainLocalPos = position - terrain.transform.position;
        // Ќормализовать позицию по отношению к размеру Terrain
        float normalizedPosX = terrainLocalPos.x / terrain.terrainData.size.x;
        float normalizedPosZ = terrainLocalPos.z / terrain.terrainData.size.z;
        // –азмеры карты высоты Terrain
        int heightmapWidth = heightmapResolution;
        int heightmapHeight = heightmapResolution;
        // ¬ычислить координаты карты высоты дл€ воздействи€
        int heightMapX = (int)(normalizedPosX * heightmapWidth);
        int heightMapZ = (int)(normalizedPosZ * heightmapHeight);

        float maxheight = terrain.terrainData.size.y;
        float sizePerMeter = 1 / maxheight;
        float fixedSizeHeight = depth * sizePerMeter;

        float[,] Heights = terrain.terrainData.GetHeights(0, 0, heightmapWidth, heightmapHeight);

        // ¬ данной реализации heightMapX и heightMapZ указывают на одну точку в Heights.
        // ¬место этого нужно итерироватьс€ по окружности радиусом radius и уменьшить высоту
        // дл€ всех соответствующих точек.

        int radiusInHeightMapUnits = Mathf.RoundToInt(radius / terrain.terrainData.size.x * heightmapWidth);

        for (int z = -radiusInHeightMapUnits; z <= radiusInHeightMapUnits; z++)
        {
            for (int x = -radiusInHeightMapUnits; x <= radiusInHeightMapUnits; x++)
            {
                int currentHeightMapX = Mathf.Clamp(heightMapX + x, 0, heightmapWidth - 1);
                int currentHeightMapZ = Mathf.Clamp(heightMapZ + z, 0, heightmapHeight - 1);

                if ((x * x) + (z * z) <= radiusInHeightMapUnits * radiusInHeightMapUnits)
                {
                    // ”меньшаем высоту точки, создава€ углубление
                    Heights[currentHeightMapZ, currentHeightMapX] -= fixedSizeHeight;
                }
            }
        }

        // ”бедитесь, что изменени€ высот не выход€т за пределы допустимого минимального значени€.
        // ѕроходимс€ по всем высотам и гарантируем, что они больше или равны 0.
        for (int hz = 0; hz < heightmapHeight; hz++)
        {
            for (int hx = 0; hx < heightmapWidth; hx++)
            {
                Heights[hz, hx] = Mathf.Max(0, Heights[hz, hx]);
            }
        }

        // ѕримен€ем измененные высоты в Terrain
        terrain.terrainData.SetHeights(0, 0, Heights);
    }

    void TerrainModify(TypeTerrainModife modifeFunc)
    {
        if (Time.time > m_NextPrimaryAttack)
        {
            RaycastHit hit;

            if (Physics.Raycast(transform.position, transform.forward, out hit, Mathf.Infinity))
            {
                if (hit.collider)
                {
                    Terrain curTerrian = hit.transform.GetComponent<Terrain>();

                    if (curTerrian)
                    {
                        switch (modifeFunc)
                        {
                            case TypeTerrainModife.AddGrass:
                                AddGrass(curTerrian, hit.point, 10.0f);
                                break;

                            case TypeTerrainModife.CutGrass:
                                CutGrass(curTerrian, hit.point, 10.0f);
                                break;

                            case TypeTerrainModife.AddTree:
                                AddTrees(curTerrian, hit.point, 10.0f);
                                break;

                            case TypeTerrainModife.CutTree:
                                break;

                            case TypeTerrainModife.AddHeight:
                                CreateDepression(curTerrian, hit.point, -1.5f, 1.0f);
                                break;

                            case TypeTerrainModife.AddHole:
                                CreateDepression(curTerrian, hit.point, 1.5f, 1.0f);
                                break;

                            default:
                                break;
                        }
                    }
                }
            }

            m_NextPrimaryAttack = Time.time + m_PrimaryAttackInterval;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetButton("Fire1"))
        {
            TerrainModify(PrimaryFunc);
        }
        else if (Input.GetButton("Fire2"))
        {
            TerrainModify(SecondaryFunc);
        }
    }
}
