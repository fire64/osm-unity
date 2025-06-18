using UnityEngine;
using System.Collections.Generic;

public class OBB
{
    public Vector3 Center;
    public Vector3 Size;
    public Quaternion Rotation;
    public Vector3[] LocalAxes;
    public OBB(Vector3 center, Vector3 size, Quaternion rotation, Vector3[] axes)
    {
        Center = center;
        Size = size;
        Rotation = rotation;
        LocalAxes = axes;
    }

    public Vector3[] CalculateCorners()
    {
        Vector3[] corners = new Vector3[8];
        Vector3 halfSize = Size * 0.5f;

        // Вычисляем все 8 углов OBB
        corners[0] = Center - halfSize.x * LocalAxes[0] - halfSize.y * LocalAxes[1] - halfSize.z * LocalAxes[2];
        corners[1] = Center + halfSize.x * LocalAxes[0] - halfSize.y * LocalAxes[1] - halfSize.z * LocalAxes[2];
        corners[2] = Center + halfSize.x * LocalAxes[0] + halfSize.y * LocalAxes[1] - halfSize.z * LocalAxes[2];
        corners[3] = Center - halfSize.x * LocalAxes[0] + halfSize.y * LocalAxes[1] - halfSize.z * LocalAxes[2];

        corners[4] = Center - halfSize.x * LocalAxes[0] - halfSize.y * LocalAxes[1] + halfSize.z * LocalAxes[2];
        corners[5] = Center + halfSize.x * LocalAxes[0] - halfSize.y * LocalAxes[1] + halfSize.z * LocalAxes[2];
        corners[6] = Center + halfSize.x * LocalAxes[0] + halfSize.y * LocalAxes[1] + halfSize.z * LocalAxes[2];
        corners[7] = Center - halfSize.x * LocalAxes[0] + halfSize.y * LocalAxes[1] + halfSize.z * LocalAxes[2];

        return corners;
    }
}

public static class MeshUtils
{
    public static OBB CalculateOBB(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        if (vertices.Length == 0)
            return new OBB(Vector3.zero, Vector3.zero, Quaternion.identity, new Vector3[3]);

        // Вычисление центра масс
        Vector3 center = Vector3.zero;
        foreach (Vector3 vertex in vertices)
            center += vertex;
        center /= vertices.Length;

        // Вычисление ковариационной матрицы
        Matrix3x3 covarianceMatrix = ComputeCovarianceMatrix(vertices, center);

        // Вычисление ковариационной матрицы
        Matrix3x3 eigenvectors = ComputeEigenvectors(covarianceMatrix);

        // Вычисление собственных векторов
        Vector3[] axes = eigenvectors.GetOrthonormalBasis();

        // Преобразование вершин в новую систему координат
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (Vector3 vertex in vertices)
        {
            Vector3 relativePos = vertex - center;
            Vector3 transformed = new Vector3(
                Vector3.Dot(relativePos, axes[0]),
                Vector3.Dot(relativePos, axes[1]),
                Vector3.Dot(relativePos, axes[2])
            );

            min = Vector3.Min(min, transformed);
            max = Vector3.Max(max, transformed);
        }

        // Вычисление параметров OBB
        Vector3 size = max - min;
        Vector3 localCenter = (min + max) * 0.5f;
        Vector3 worldCenter = center +
                             axes[0] * localCenter.x +
                             axes[1] * localCenter.y +
                             axes[2] * localCenter.z;

        Quaternion rotation = Quaternion.LookRotation(axes[2], axes[1]);

        return new OBB(worldCenter, size, rotation, axes);
    }

    private static Matrix3x3 ComputeCovarianceMatrix(Vector3[] vertices, Vector3 center)
    {
        Matrix3x3 matrix = new Matrix3x3();
        int count = vertices.Length;

        foreach (Vector3 vertex in vertices)
        {
            Vector3 delta = vertex - center;
            matrix.m00 += delta.x * delta.x;
            matrix.m11 += delta.y * delta.y;
            matrix.m22 += delta.z * delta.z;
            matrix.m01 += delta.x * delta.y;
            matrix.m02 += delta.x * delta.z;
            matrix.m12 += delta.y * delta.z;
        }

        // Нормализация
        float invCount = 1.0f / count;
        matrix.m00 *= invCount;
        matrix.m11 *= invCount;
        matrix.m22 *= invCount;
        matrix.m01 *= invCount;
        matrix.m02 *= invCount;
        matrix.m12 *= invCount;
        matrix.m10 = matrix.m01;
        matrix.m20 = matrix.m02;
        matrix.m21 = matrix.m12;

        return matrix;
    }

    // Реализация метода Якоби для поиска собственных векторов
    public static void JacobiSolver(Matrix3x3 matrix, out Vector3 eigenvalues, out Matrix3x3 eigenvectors, int iterations)
    {
        eigenvectors = Matrix3x3.identity;
        Matrix3x3 a = matrix;

        for (int i = 0; i < iterations; i++)
        {
            // Поиск наибольшего недиагонального элемента
            int p = 0, q = 1;
            float maxVal = Mathf.Abs(a.m01);
            if (Mathf.Abs(a.m02) > maxVal) { maxVal = Mathf.Abs(a.m02); p = 0; q = 2; }
            if (Mathf.Abs(a.m12) > maxVal) { maxVal = Mathf.Abs(a.m12); p = 1; q = 2; }

            if (maxVal < 1e-6f) break;

            // Вычисление вращения
            float theta = 0.5f * Mathf.Atan2(2 * a[p, q], a[q, q] - a[p, p]);
            float c = Mathf.Cos(theta);
            float s = Mathf.Sin(theta);

            Matrix3x3 j = Matrix3x3.identity;
            j[p, p] = c; j[q, q] = c;
            j[p, q] = s; j[q, p] = -s;

            // Обновление матрицы
            a = j.Transpose() * a * j;
            eigenvectors = eigenvectors * j;
        }

        eigenvalues = new Vector3(a.m00, a.m11, a.m22);
    }

    private static Matrix3x3 ComputeEigenvectors(Matrix3x3 matrix)
    {
        Vector3 eigenvalues;
        Matrix3x3 eigenvectors;
        JacobiSolver(matrix, out eigenvalues, out eigenvectors, 20);
        return eigenvectors;
    }
}


// Вспомогательная структура для работы с матрицами 3x3
public struct Matrix3x3
{
    public float m00, m01, m02;
    public float m10, m11, m12;
    public float m20, m21, m22;

    public float this[int row, int col]
    {
        get
        {
            return row == 0 ?
                (col == 0 ? m00 : col == 1 ? m01 : m02) :
                row == 1 ?
                (col == 0 ? m10 : col == 1 ? m11 : m12) :
                (col == 0 ? m20 : col == 1 ? m21 : m22);
        }
        set
        {
            if (row == 0)
            {
                if (col == 0) m00 = value;
                else if (col == 1) m01 = value;
                else m02 = value;
            }
            else if (row == 1)
            {
                if (col == 0) m10 = value;
                else if (col == 1) m11 = value;
                else m12 = value;
            }
            else
            {
                if (col == 0) m20 = value;
                else if (col == 1) m21 = value;
                else m22 = value;
            }
        }
    }

    public static Matrix3x3 identity
    {
        get
        {
            return new Matrix3x3
            {
                m00 = 1,
                m11 = 1,
                m22 = 1
            };
        }
    }

    public Matrix3x3 Transpose()
    {
        Matrix3x3 result;
        result.m00 = m00; result.m01 = m10; result.m02 = m20;
        result.m10 = m01; result.m11 = m11; result.m12 = m21;
        result.m20 = m02; result.m21 = m12; result.m22 = m22;
        return result;
    }

    public Vector3[] GetOrthonormalBasis()
    {
        return new Vector3[]
        {
            new Vector3(m00, m10, m20).normalized,
            new Vector3(m01, m11, m21).normalized,
            new Vector3(m02, m12, m22).normalized
        };
    }

    public static Matrix3x3 operator *(Matrix3x3 a, Matrix3x3 b)
    {
        Matrix3x3 result;
        result.m00 = a.m00 * b.m00 + a.m01 * b.m10 + a.m02 * b.m20;
        result.m01 = a.m00 * b.m01 + a.m01 * b.m11 + a.m02 * b.m21;
        result.m02 = a.m00 * b.m02 + a.m01 * b.m12 + a.m02 * b.m22;

        result.m10 = a.m10 * b.m00 + a.m11 * b.m10 + a.m12 * b.m20;
        result.m11 = a.m10 * b.m01 + a.m11 * b.m11 + a.m12 * b.m21;
        result.m12 = a.m10 * b.m02 + a.m11 * b.m12 + a.m12 * b.m22;

        result.m20 = a.m20 * b.m00 + a.m21 * b.m10 + a.m22 * b.m20;
        result.m21 = a.m20 * b.m01 + a.m21 * b.m11 + a.m22 * b.m21;
        result.m22 = a.m20 * b.m02 + a.m21 * b.m12 + a.m22 * b.m22;

        return result;
    }
}