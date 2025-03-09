using UnityEngine;

[RequireComponent(typeof(MapReader))]
abstract class InfrstructureBehaviour : MonoBehaviour
{
    protected MapReader map;

    public bool isFinished = false;

    void Awake()
    {
        map = GetComponent<MapReader>();
    }

    protected Vector3 GetCentre(BaseOsm way)
    {
        Vector3 total = Vector3.zero;

        foreach (var id in way.NodeIDs)
        {
            total += map.nodes[id];
        }

        return total / way.NodeIDs.Count;
    }
}