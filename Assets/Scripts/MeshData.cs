using System.Collections.Generic;
using UnityEngine;

/// <summary>
///  ласс дл€ хранени€ данных меша при генерации
/// ќптимизирован дл€ переиспользовани€ через Object Pooling
/// </summary>
public class MeshData
{
    public List<Vector3> Vertices = new List<Vector3>();
    public List<int> Indices = new List<int>();
    public List<Vector3> Normals = new List<Vector3>();
    public List<Vector2> UV = new List<Vector2>();

    // ============================================
    // ќѕ“»ћ»«ј÷»я: ѕредварительное выделение пам€ти
    // ============================================
    public MeshData(int initialCapacity = 64)
    {
        Vertices = new List<Vector3>(initialCapacity);
        Indices = new List<int>(initialCapacity * 6);
        Normals = new List<Vector3>(initialCapacity);
        UV = new List<Vector2>(initialCapacity);
    }

    // ============================================
    // ќѕ“»ћ»«ј÷»я: ћетод очистки дл€ переиспользовани€
    // ============================================
    public void Clear()
    {
        Vertices.Clear();
        Indices.Clear();
        Normals.Clear();
        UV.Clear();
    }

    // ============================================
    // ќѕ“»ћ»«ј÷»я: ћетод дл€ предварительного резервировани€ пам€ти
    // ============================================
    public void EnsureCapacity(int vertexCount)
    {
        if (Vertices.Capacity < vertexCount)
        {
            Vertices.Capacity = vertexCount;
            Normals.Capacity = vertexCount;
            UV.Capacity = vertexCount;
            Indices.Capacity = vertexCount * 6;
        }
    }
}
