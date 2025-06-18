using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OBBVisualizer : MonoBehaviour
{
    public Color gizmoColor = Color.green;

    private MeshFilter _meshFilter;
    private OBB _obb;
    private Vector3[] _corners;

    void Start()
    {
        _meshFilter = GetComponent<MeshFilter>();
        if (_meshFilter != null)
        {
            _obb = MeshUtils.CalculateOBB(_meshFilter.sharedMesh);
            _corners = _obb.CalculateCorners();
        }
    }

    void OnDrawGizmos()
    {
        if (_obb == null || _corners == null) return;

        Gizmos.color = gizmoColor;
        Matrix4x4 originalMatrix = Gizmos.matrix;

        // Применяем трансформацию объекта
        Gizmos.matrix = transform.localToWorldMatrix;

        // Рисуем все 12 рёбер параллелепипеда
        for (int i = 0; i < BoxEdges.Length; i += 2)
        {
            Gizmos.DrawLine(_corners[BoxEdges[i]], _corners[BoxEdges[i + 1]]);
        }

        Gizmos.matrix = originalMatrix;
    }

    // Индексы рёбер для отрисовки (12 рёбер = 24 индекса)
    private static readonly int[] BoxEdges = {
        0,1, 1,2, 2,3, 3,0, // Нижняя грань
        4,5, 5,6, 6,7, 7,4, // Верхняя грань
        0,4, 1,5, 2,6, 3,7  // Вертикальные рёбра
    };
}
