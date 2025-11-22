using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Route : BaseDataObject
{
    public List<memberdata_t> memberslist;
    public List<Vector3> stoppoints;
    public List<ulong> platformsid;

    void OnDrawGizmosSelected()
    {
        for( int i = 0; i < stoppoints.Count; i++)
        {
            Vector3 point = stoppoints[i];

            // Визуализация радиуса обнаружения лавочек
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(point, 1.0f);
        }

        for (int i = 0; i < coordpoints.Count; i++)
        {
            Vector3 point = coordpoints[i];

            if (i < coordpoints.Count - 1)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(point, coordpoints[i + 1]); 
            }
        }
    }
}
