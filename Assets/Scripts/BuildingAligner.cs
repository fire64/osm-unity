using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BuildingAligner : MonoBehaviour
{
    public GameObject building;          // Примитивный объект
    public GameObject buildingModel;    // Детализированный объект

    [ContextMenu("Align Model to Building")]
    public void AlignModelToBuilding()
    {
        if (building == null || buildingModel == null)
        {
            Debug.LogError("Objects are not assigned!");
            return;
        }

        // Получаем меши
        Mesh buildingMesh = GetMesh(building);
        Mesh modelMesh = GetMesh(buildingModel);

        if (buildingMesh == null || modelMesh == null)
        {
            Debug.LogError("Mesh not found on one of the objects");
            return;
        }

        // Рассчитываем OBB для объектов
        OBB buildingOBB = CalculateOBB(buildingMesh, building.transform);
        OBB modelOBB = CalculateOBB(modelMesh, buildingModel.transform);

        // Создаем контейнер для модели
        GameObject container = new GameObject("ModelContainer");
        container.transform.position = modelOBB.Center;

        // Сохраняем исходные трансформации модели
        Transform modelTransform = buildingModel.transform;
        Vector3 originalPosition = modelTransform.position;
        Quaternion originalRotation = modelTransform.rotation;
        Vector3 originalScale = modelTransform.localScale;

        // Помещаем модель в контейнер
        modelTransform.SetParent(container.transform, true);

        // Вычисляем матрицу поворота
        Matrix4x4 rotationMatrix = CalculateRotationMatrix(
            modelOBB.Axes,
            buildingOBB.Axes
        );

        // Применяем поворот и масштабирование
        container.transform.rotation = rotationMatrix.rotation;
        container.transform.localScale = CalculateScale(modelOBB.Size, buildingOBB.Size);
        container.transform.position = buildingOBB.Center;

        // Восстанавливаем локальные трансформации модели
        modelTransform.localPosition = originalPosition;
        modelTransform.localRotation = originalRotation;
        modelTransform.localScale = originalScale;
    }

    private Mesh GetMesh(GameObject obj)
    {
        MeshFilter filter = obj.GetComponent<MeshFilter>();
        return filter != null ? filter.sharedMesh : null;
    }

    private OBB CalculateOBB(Mesh mesh, Transform transform)
    {
        Vector3[] vertices = mesh.vertices;
        int vertexCount = vertices.Length;

        // Преобразуем вершины в мировые координаты
        Vector3[] worldVertices = new Vector3[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            worldVertices[i] = transform.TransformPoint(vertices[i]);
        }

        // Рассчитываем центр масс
        Vector3 center = Vector3.zero;
        foreach (Vector3 v in worldVertices) center += v;
        center /= vertexCount;

        // Строим ковариационную матрицу
        Matrix4x4 covariance = Matrix4x4.zero;
        foreach (Vector3 v in worldVertices)
        {
            Vector3 delta = v - center;
            covariance[0, 0] += delta.x * delta.x;
            covariance[1, 1] += delta.y * delta.y;
            covariance[2, 2] += delta.z * delta.z;
            covariance[0, 1] += delta.x * delta.y;
            covariance[0, 2] += delta.x * delta.z;
            covariance[1, 2] += delta.y * delta.z;
        }

        // Симметричные компоненты
        covariance[1, 0] = covariance[0, 1];
        covariance[2, 0] = covariance[0, 2];
        covariance[2, 1] = covariance[1, 2];

        // Находим собственные векторы (главные оси)
        Vector3[] axes = CalculatePrincipalAxes(covariance);

        // Рассчитываем размеры OBB
        Vector3 size = CalculateOBBSize(worldVertices, center, axes);

        // Упорядочиваем оси по размеру (X-самая большая, Z-самая маленькая)
        System.Array.Sort(new float[] { size.x, size.y, size.z }, axes, System.Collections.Generic.Comparer<float>.Create((a, b) => b.CompareTo(a)));

        return new OBB(center, axes, size);
    }

    private Vector3[] CalculatePrincipalAxes(Matrix4x4 covariance)
    {
        Vector3[] axes = new Vector3[3];
        float epsilon = 0.001f;
        int maxIterations = 50;

        // Исходная матрица
        float[,] matrix = {
            { covariance[0,0], covariance[0,1], covariance[0,2] },
            { covariance[1,0], covariance[1,1], covariance[1,2] },
            { covariance[2,0], covariance[2,1], covariance[2,2] }
        };

        // Итерационный метод Якоби
        for (int iter = 0; iter < maxIterations; iter++)
        {
            // Находим наибольший внедиагональный элемент
            int p = 0, q = 1;
            for (int i = 0; i < 3; i++)
            {
                for (int j = i + 1; j < 3; j++)
                {
                    if (Mathf.Abs(matrix[i, j]) > Mathf.Abs(matrix[p, q]))
                    {
                        p = i;
                        q = j;
                    }
                }
            }

            // Проверка на сходимость
            if (Mathf.Abs(matrix[p, q]) < epsilon) break;

            // Вычисляем угол поворота
            float theta = 0.5f * Mathf.Atan2(2 * matrix[p, q], matrix[q, q] - matrix[p, p]);
            float c = Mathf.Cos(theta);
            float s = Mathf.Sin(theta);

            // Обновляем матрицу
            float[,] newMatrix = (float[,])matrix.Clone();
            for (int r = 0; r < 3; r++)
            {
                newMatrix[r, p] = c * matrix[r, p] - s * matrix[r, q];
                newMatrix[r, q] = s * matrix[r, p] + c * matrix[r, q];
            }
            for (int r = 0; r < 3; r++)
            {
                matrix[p, r] = c * newMatrix[p, r] - s * newMatrix[q, r];
                matrix[q, r] = s * newMatrix[p, r] + c * newMatrix[q, r];
            }
            matrix[p, q] = matrix[q, p] = 0;
            matrix[p, p] = c * c * newMatrix[p, p] - 2 * c * s * newMatrix[p, q] + s * s * newMatrix[q, q];
            matrix[q, q] = s * s * newMatrix[p, p] + 2 * c * s * newMatrix[p, q] + c * c * newMatrix[q, q];
        }

        // Извлекаем собственные векторы
        axes[0] = new Vector3(matrix[0, 0], matrix[0, 1], matrix[0, 2]).normalized;
        axes[1] = new Vector3(matrix[1, 0], matrix[1, 1], matrix[1, 2]).normalized;
        axes[2] = Vector3.Cross(axes[0], axes[1]).normalized;
        axes[1] = Vector3.Cross(axes[2], axes[0]).normalized;

        return axes;
    }

    private Vector3 CalculateOBBSize(Vector3[] vertices, Vector3 center, Vector3[] axes)
    {
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (Vector3 vertex in vertices)
        {
            Vector3 delta = vertex - center;
            for (int i = 0; i < 3; i++)
            {
                float projection = Vector3.Dot(delta, axes[i]);
                if (projection < min[i]) min[i] = projection;
                if (projection > max[i]) max[i] = projection;
            }
        }

        return max - min;
    }

    private Matrix4x4 CalculateRotationMatrix(Vector3[] sourceAxes, Vector3[] targetAxes)
    {
        Matrix4x4 sourceMatrix = Matrix4x4.zero;
        Matrix4x4 targetMatrix = Matrix4x4.zero;

        for (int i = 0; i < 3; i++)
        {
            sourceMatrix.SetColumn(i, sourceAxes[i]);
            targetMatrix.SetColumn(i, targetAxes[i]);
        }

        return targetMatrix * sourceMatrix.inverse;
    }

    private Vector3 CalculateScale(Vector3 sourceSize, Vector3 targetSize)
    {
        return new Vector3(
            targetSize.x / sourceSize.x,
            targetSize.y / sourceSize.y,
            targetSize.z / sourceSize.z
        );
    }

    private struct OBB
    {
        public Vector3 Center { get; }
        public Vector3[] Axes { get; }
        public Vector3 Size { get; }

        public OBB(Vector3 center, Vector3[] axes, Vector3 size)
        {
            Center = center;
            Axes = axes;
            Size = size;
        }
    }
}