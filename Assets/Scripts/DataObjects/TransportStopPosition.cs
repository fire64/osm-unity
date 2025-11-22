using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransportStopPosition : BaseDataObject
{
    // Для визуализации в редакторе
    void OnDrawGizmos()
    {

        Gizmos.color = Color.cyan;

        Gizmos.DrawSphere(transform.position, 1.0f);
    }
}
