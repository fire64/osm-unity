using System.Collections;
using UnityEngine;

public class SpawnAtPoint : MonoBehaviour
{
    TileSystem tileSystem;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        tileSystem = FindObjectOfType<TileSystem>();

        if (tileSystem && tileSystem.tileType == TileSystem.TileType.Terrain)
        {
            if (tileSystem.isUseElevation)
            {
                //     water.transform.position  = new Vector3(water.transform.position.x, GR.getHeightPosition(water.transform), water.transform.position.z);

                StartCoroutine(SpawnInHeight(gameObject));
            }
        }
    }

    private IEnumerator SpawnInHeight(GameObject gameObject)
    {
        yield return new WaitForSeconds(4.0f);

   //     transform.position = getTerrianHeightPosition(gameObject, AlgorithmHeightSorting.CenterHeight) + Vector3.up * 5.0f;
    }


}
