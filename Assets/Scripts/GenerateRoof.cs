using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEditor.Progress;
using static UnityEngine.UI.GridLayoutGroup;

public class GenerateRoof : MonoBehaviour
{
    public GameObject roof_onion;
    public BuildingMaterials buildingMaterials;

    public void CreateHippedRoof(List<Vector3> baseCorners, float min_height, float height, MeshData data, Vector2 min, Vector2 size)
    {
        if (baseCorners == null || baseCorners.Count < 3)
        {
            Debug.LogError("Недостаточно точек для создания крыши.");
            return;
        }

        baseCorners.Reverse();

        // Вычисляем центр основания крыши
        Vector3 center = Vector3.zero;
        foreach (var corner in baseCorners)
        {
            center += corner;
        }
        center /= baseCorners.Count;

        // Высота вершины крыши
        Vector3 peak = center + Vector3.up * height;

        // Добавляем вершины основания
        int baseVertexCount = data.Vertices.Count;
        foreach (var corner in baseCorners)
        {
            data.Vertices.Add(corner + Vector3.up * min_height); // Высота основания
        }

        // Добавляем вершину крыши
        data.Vertices.Add(peak);

        // Создаем индексы для треугольников
        int peakIndex = baseVertexCount + baseCorners.Count; // Индекс вершины крыши
        for (int i = 0; i < baseCorners.Count; i++)
        {
            int nextIndex = (i + 1) % baseCorners.Count; // Индекс следующей вершины
            // Создаем два треугольника для каждой стороны крыши
            data.Indices.Add(baseVertexCount + i); // Основание
            data.Indices.Add(baseVertexCount + nextIndex);
            data.Indices.Add(peakIndex);

            // Нормали
            Vector3 normal = Vector3.Cross(baseCorners[nextIndex] - baseCorners[i], peak - baseCorners[i]).normalized;
            data.Normals.Add(normal);
            data.Normals.Add(normal);
            data.Normals.Add(normal);
        }

        // Добавляем UV координаты (можно настроить по желанию)
        foreach (var corner in baseCorners)
        {
            data.UV.Add(new Vector2(corner.x, corner.z)); // Пример UV координат
        }
        data.UV.Add(new Vector2(center.x, center.z)); // UV для вершины
    }

    public void CreateGabledRoof(List<Vector3> baseCorners, float min_height, float height, MeshData data, Vector2 min, Vector2 size)
    {
        // Проверка на наличие минимум 3 точек для формирования крыши
        if (baseCorners.Count < 3)
        {
            Debug.LogError("Недостаточно точек для создания крыши");
            return;
        }

        baseCorners.Reverse();

        // Получение центра основания
        Vector3 center = Vector3.zero;
        foreach (Vector3 corner in baseCorners)
        {
            center += corner;
        }
        center /= baseCorners.Count;

        // Определение вершины крыши
        Vector3 roofPeak = new Vector3(center.x, height, center.z);

        // Добавление вершин основания
        foreach (Vector3 corner in baseCorners)
        {
            data.Vertices.Add(corner + Vector3.up * min_height);
        }

        // Добавление вершины крыши
        data.Vertices.Add(roofPeak);

        int baseCount = baseCorners.Count;

        // Создание индексов для треугольников
        for (int i = 0; i < baseCount; i++)
        {
            int nextIndex = (i + 1) % baseCount; // Следующий индекс (зацикливание)

            // Треугольники между основанием и вершиной крыши
            data.Indices.Add(i);
            data.Indices.Add(nextIndex);
            data.Indices.Add(baseCount); // Индекс вершины крыши
        }

        // Создание нормалей
        for (int i = 0; i < baseCount; i++)
        {
            int nextIndex = (i + 1) % baseCount; // Следующий индекс (зацикливание)
            Vector3 normal = Vector3.Cross(
                baseCorners[nextIndex] - baseCorners[i],
                roofPeak - baseCorners[i]
            ).normalized;

            data.Normals.Add(normal);
        }

        // Добавление нормали для вершины крыши
        data.Normals.Add(Vector3.up); // Нормаль для вершины крыши

        // Генерация UV координат (можно настроить по своему усмотрению)
        foreach (Vector3 vertex in data.Vertices)
        {
            data.UV.Add(new Vector2(vertex.x, vertex.z)); // Простая проекция UV
        }
    }
    
    void CreateGambrelRoof(List<Vector3> baseCorners, float min_height, float height, MeshData data, Vector2 min, Vector2 siz)
    {
        // Проверка входных данных
        if (baseCorners == null || baseCorners.Count < 3)
            return;

        // Находим центр основания
        Vector3 center = Vector3.zero;
        foreach (var corner in baseCorners)
            center += corner;
        center /= baseCorners.Count;

        // Устанавливаем высоту центра
        Vector3 roofTop = new Vector3(center.x, height, center.z);

        // Параметры для ломаной крыши
        float lowerSlopeHeight = min_height + (height - min_height) * 0.4f; // Высота перегиба (40% от общей высоты)
        float lowerSlopeWidth = 0.7f; // Ширина нижнего ската (70% от расстояния до центра)

        int baseVertexCount = data.Vertices.Count;

        // Добавляем вершины основания
        foreach (var corner in baseCorners)
        {
            data.Vertices.Add(new Vector3(corner.x, min_height, corner.z));
            data.UV.Add(new Vector2(corner.x * 0.2f, corner.z * 0.2f)); // Простая UV-развертка
        }

        // Добавляем вершины перегиба
        List<Vector3> middlePoints = new List<Vector3>();
        for (int i = 0; i < baseCorners.Count; i++)
        {
            Vector3 corner = baseCorners[i];
            Vector3 dirToCenter = (center - corner).normalized;
            Vector3 middlePoint = corner + dirToCenter * Vector3.Distance(corner, center) * lowerSlopeWidth;
            middlePoint.y = lowerSlopeHeight;

            middlePoints.Add(middlePoint);
            data.Vertices.Add(middlePoint);
            data.UV.Add(new Vector2(middlePoint.x * 0.2f, middlePoint.z * 0.2f));
        }

        // Добавляем вершину крыши
        data.Vertices.Add(roofTop);
        data.UV.Add(new Vector2(roofTop.x * 0.2f, roofTop.z * 0.2f));
        int roofTopIndex = data.Vertices.Count - 1;

        // Создаем треугольники для нижней части ломаной крыши (трапеции)
        for (int i = 0; i < baseCorners.Count; i++)
        {
            int nextI = (i + 1) % baseCorners.Count;

            // Индексы вершин основания
            int baseIndex = baseVertexCount + i;
            int nextBaseIndex = baseVertexCount + nextI;

            // Индексы вершин перегиба
            int middleIndex = baseVertexCount + baseCorners.Count + i;
            int nextMiddleIndex = baseVertexCount + baseCorners.Count + nextI;

            // Нижняя часть (трапеция)
            // Первый треугольник трапеции
            data.Indices.Add(baseIndex);
            data.Indices.Add(middleIndex);
            data.Indices.Add(nextBaseIndex);

            // Второй треугольник трапеции
            data.Indices.Add(nextBaseIndex);
            data.Indices.Add(middleIndex);
            data.Indices.Add(nextMiddleIndex);

            // Верхняя часть (треугольник к вершине)
            data.Indices.Add(middleIndex);
            data.Indices.Add(roofTopIndex);
            data.Indices.Add(nextMiddleIndex);
        }

        // Вычисляем нормали
        CalculateNormalsGambrelRoof(data);
    }

    // Вспомогательная функция для вычисления нормалей
    void CalculateNormalsGambrelRoof(MeshData data)
    {
        // Инициализируем нормали
        data.Normals.Clear();
        for (int i = 0; i < data.Vertices.Count; i++)
        {
            data.Normals.Add(Vector3.zero);
        }

        // Проходимся по всем треугольникам и вычисляем их вклад в нормали вершин
        for (int i = 0; i < data.Indices.Count; i += 3)
        {
            int indexA = data.Indices[i];
            int indexB = data.Indices[i + 1];
            int indexC = data.Indices[i + 2];

            Vector3 pointA = data.Vertices[indexA];
            Vector3 pointB = data.Vertices[indexB];
            Vector3 pointC = data.Vertices[indexC];

            // Вычисляем нормаль треугольника
            Vector3 sideAB = pointB - pointA;
            Vector3 sideAC = pointC - pointA;
            Vector3 normal = Vector3.Cross(sideAB, sideAC).normalized;

            // Добавляем эту нормаль ко всем вершинам треугольника
            data.Normals[indexA] += normal;
            data.Normals[indexB] += normal;
            data.Normals[indexC] += normal;
        }

        // Нормализуем результаты
        for (int i = 0; i < data.Normals.Count; i++)
        {
            data.Normals[i] = data.Normals[i].normalized;
        }
    }

    public void CreateDomeRoof(List<Vector3> baseCorners, float minHeight, float height, MeshData data, Vector2 min, Vector2 size)
    {
        int numSegments = 6; // Увеличьте для более гладкого купола
        int numCorners = baseCorners.Count;
        if (numCorners < 3) return;

        // Рассчитываем центр основания
        Vector3 center = Vector3.zero;
        foreach (Vector3 corner in baseCorners)
        {
            center += corner;
        }
        center /= numCorners;
        center.y = minHeight;

        // Генерация вершин купола
        for (int i = 0; i <= numSegments; i++)
        {
            float t = i / (float)numSegments;
            float currentHeight = minHeight + (height * Mathf.Sin(t * Mathf.PI * 0.5f));

            for (int j = 0; j < numCorners; j++)
            {
                Vector3 interpolated = Vector3.Lerp(baseCorners[j] + Vector3.up * minHeight, center, t);
                interpolated.y = currentHeight;
                data.Vertices.Add(interpolated);
            }
        }

        // Добавляем вершину в центре купола
        Vector3 topCenter = center;
        topCenter.y = height;
        data.Vertices.Add(topCenter);
        int topIndex = data.Vertices.Count - 1;

        // Генерация треугольников для боковой поверхности
        for (int i = 0; i < numSegments; i++)
        {
            for (int j = 0; j < numCorners; j++)
            {
                int nextJ = (j + 1) % numCorners;

                int currentA = i * numCorners + j;
                int currentB = i * numCorners + nextJ;
                int nextA = (i + 1) * numCorners + j;
                int nextB = (i + 1) * numCorners + nextJ;

                // Первый треугольник
                data.Indices.Add(currentA);
                data.Indices.Add(nextA);
                data.Indices.Add(currentB);

                // Второй треугольник
                data.Indices.Add(currentB);
                data.Indices.Add(nextA);
                data.Indices.Add(nextB);
            }
        }

        // Генерация треугольников для верхушки
        int lastRingStart = numSegments * numCorners;
        for (int j = 0; j < numCorners; j++)
        {
            int nextJ = (j + 1) % numCorners;
            data.Indices.Add(lastRingStart + j);
            data.Indices.Add(topIndex);
            data.Indices.Add(lastRingStart + nextJ);
        }

        // Рассчитываем нормали
        CalculateNormalsDomeRoof(data, center, topIndex);

        // Генерация UV-координат
        GenerateUVDomeRoof(data, baseCorners);
    }

    private void CalculateNormalsDomeRoof(MeshData data, Vector3 center, int topIndex)
    {
        for (int i = 0; i < data.Vertices.Count; i++)
        {
            if (i == topIndex)
            {
                data.Normals.Add(Vector3.up);
            }
            else
            {
                Vector3 normal = (data.Vertices[i] - center).normalized;
                data.Normals.Add(normal);
            }
        }
    }

    private void GenerateUVDomeRoof(MeshData data, List<Vector3> baseCorners)
    {
        // Рассчитываем границы для UV-проекции
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;

        foreach (Vector3 corner in baseCorners)
        {
            if (corner.x < minX) minX = corner.x;
            if (corner.x > maxX) maxX = corner.x;
            if (corner.z < minZ) minZ = corner.z;
            if (corner.z > maxZ) maxZ = corner.z;
        }

        float width = maxX - minX;
        float depth = maxZ - minZ;

        foreach (Vector3 vertex in data.Vertices)
        {
            float u = width == 0 ? 0.5f : (vertex.x - minX) / width;
            float v = depth == 0 ? 0.5f : (vertex.z - minZ) / depth;
            data.UV.Add(new Vector2(u, v));
        }
    }
    
    private void CreatePyramidalRoof(List<Vector3> baseCorners, float min_height, float height, MeshData data, Vector2 min, Vector2 size)
    {
        // Проверяем, что основание состоит минимум из четырех точек
        if (baseCorners.Count < 4)
        {
            Debug.Log("Недостаточно вершин для создания пирамиды!");
            return;
        }

        if (!GR.IsClockwise(baseCorners))
        {
            baseCorners.Reverse();
        }

        // Количество вершин = углы основания + 1 центральная вершина
        Vector3[] vertices = new Vector3[baseCorners.Count + 1];
        for (int i = 0; i < baseCorners.Count; i++)
        {
            Vector3 curpoint = baseCorners[i];
            curpoint.y = min_height;
            baseCorners[i] = curpoint;
            vertices[i] = baseCorners[i];

        }
        // Центральная верхняя точка пирамиды
        Vector3 topCenter = Vector3.up * height;
        vertices[baseCorners.Count] = topCenter;

        // Создаем список треугольников
        List<int> trianglesList = new List<int>();

        // Добавляем боковые треугольники
        for (int i = 0; i < baseCorners.Count; i++)
        {
            trianglesList.Add(baseCorners.Count); // Верхняя центральная точка
            trianglesList.Add(i); // Текущая вершина основания
            trianglesList.Add((i + 1) % baseCorners.Count); // Следующая вершина основания (с зацикливанием)
        }

        // Добавляем треугольники для основания пирамиды
        // Это делается путем создания триангуляции для многоугольника,
        // можно использовать алгоритм триангуляции (например, "треугольник вентилятора")
        // Для простоты, мы ограничимся триангуляцией в форме веера, которая подходит для выпуклых оснований
        int firstCornerIndex = 0;
        for (int i = 1; i < baseCorners.Count - 1; i++)
        {
            trianglesList.Add(firstCornerIndex);
            trianglesList.Add(i);
            trianglesList.Add(i + 1);
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            data.Vertices.Add(vertices[i]);
        }

        for (int i = 0; i < trianglesList.Count; i++)
        {
            data.Indices.Add(trianglesList[i]);
        }
    }

    private void CreateOnionRoof(List<Vector3> corners, float min_height, float height, MeshData data, Vector2 min, Vector2 size)
    {
        GameObject go = Instantiate(roof_onion) as GameObject;

        var onionRoof = go.GetComponent<MeshFilter>().mesh;

        var verticesls = onionRoof.vertices;
        var triangles = onionRoof.triangles;

        for (int i = 0; i < verticesls.Length; i++)
        {
            var verticle = verticesls[i];

            float scale_fator = (Mathf.Min(size.x, size.y) / 2) * 100;

            data.Vertices.Add(new Vector3(verticle.x * scale_fator, (verticle.y * scale_fator) + min_height, verticle.z * scale_fator));
        }

        for (int i = 0; i < triangles.Length; i++)
        {
            data.Indices.Add(triangles[i]);
        }

        Destroy(go);
    }
    private int ParseDirection(string value)
    {
        int res;

        if (int.TryParse(value, out res))
        {
            return res;
        }

        switch (value)
        {
            case "N": return 0;
            case "NNE": return 22;
            case "NE": return 45;
            case "ENE": return 67;
            case "E": return 90;
            case "ESE": return 122;
            case "SE": return 135;
            case "SSE": return 157;
            case "S": return 180;
            case "SSW": return 202;
            case "SW": return 225;
            case "WSW": return 247;
            case "W": return 270;
            case "WNW": return 292;
            case "NW": return 315;
            case "NNW": return 337;
        }

        return 0;
    }


    // Start is called before the first frame update
    public void GenerateRoofForBuillding(Building building, List<Vector3> corners, List<List<Vector3>> holesCorners, float minHeight, float height, Vector2 min, Vector2 size, BaseOsm geo, bool isUseOldTriangulation)
    {
        var roof = new GameObject("roof");

        roof.transform.SetParent(building.transform);

        var mesh = roof.AddComponent<MeshFilter>().mesh;

        roof.AddComponent<MeshRenderer>();

        bool isRoofHeightExternalSet = false;

        float roof_height = 0.01f;

        if (geo.HasField("roof:height"))
        {
            roof_height = geo.GetValueFloatByKey("roof:height");
            isRoofHeightExternalSet = true;
        }
        else if (geo.HasField("roof:levels"))
        {
            roof_height = geo.GetValueFloatByKey("roof:levels") * 3.0f;
            isRoofHeightExternalSet = true;
        }
        else if (building.curSettings.defaultRoofHeight > 0.0f)
        {
            roof_height = building.curSettings.defaultRoofHeight;
            isRoofHeightExternalSet = true;
        }

        var roof_type = "flat";

        if (geo.HasField("roof:shape"))
        {
            roof_type = geo.GetValueStringByKey("roof:shape");
        }
        else
        {
            roof_type = building.curSettings.defaultRoofShape;
        }

        float roofangle = 0.0f;

        if (geo.HasField("roof:angle"))
        {
            roofangle = geo.GetValueFloatByKey("roof:angle");
        }
        else if (geo.HasField("building:roof:angle"))
        {
            roofangle = geo.GetValueFloatByKey("building:roof:angle");
        }

        int roof_direction = 0;

        if (geo.HasField("roof:direction"))
        {
            var roof_direction_str = geo.GetValueStringByKey("roof:direction");

            roof_direction = ParseDirection(roof_direction_str);
        }

        string roof_orientation = null;

        if (geo.HasField("roof:orientation"))
        {
            roof_orientation = geo.GetValueStringByKey("roof:orientation");
        }
        else if(geo.HasField("building:roof:orientation"))
        {
            roof_orientation = geo.GetValueStringByKey("building:roof:orientation");
        }

        var tb = new MeshData();

        if (roof_type == "flat") //fix
        {
            if(isUseOldTriangulation)
            {
                GR.CreateMeshWithHeightOld(corners, height, height + roof_height, tb);
            }
            else
            {
                GR.CreateMeshWithHeight(corners, height, height + roof_height, tb, holesCorners);
            }

        }
        else if (roof_type == "hipped")
        {
            if (!isRoofHeightExternalSet)
            {
                roof_height = 6.0f;
            }

            CreateHippedRoof(corners, height, height + roof_height, tb, min, size);
        }
        else if (roof_type == "gabled")
        {
            if (!isRoofHeightExternalSet)
            {
                roof_height = 6.0f;
            }

            CreateGabledRoof(corners, height, height + roof_height, tb, min, size);
        }
        else if (roof_type == "gambrel")
        {
            if (!isRoofHeightExternalSet)
            {
                roof_height = 6.0f;
            }

            CreateGambrelRoof(corners, height, height + roof_height, tb, min, size);
        }
        else if (roof_type == "dome") //fix
        {
            if (!isRoofHeightExternalSet)
            {
                roof_height = 1.0f;
            }

            CreateDomeRoof(corners, height, height + roof_height, tb, min, size);
        }
        else if (roof_type == "pyramidal")
        {
            if (!isRoofHeightExternalSet)
            {
                roof_height = 1.0f;
            }

            CreatePyramidalRoof(corners, height, height + roof_height, tb, min, size);
        }
        else if (roof_type == "onion")
        {
            if (!isRoofHeightExternalSet)
            {
                roof_height = 1.0f;
            }

            CreateOnionRoof(corners, height, height + roof_height, tb, min, size);
        }
        else
        {
            Debug.Log("Try create roofs: " + roof_type);

            //Not supported, use flat
            if (isUseOldTriangulation)
            {
                GR.CreateMeshWithHeightOld(corners, height, height + roof_height, tb);
            }
            else
            {
                GR.CreateMeshWithHeight(corners, height, height + roof_height, tb, holesCorners);
            }
        }

        mesh.vertices = tb.Vertices.ToArray();
        mesh.triangles = tb.Indices.ToArray();
        mesh.SetUVs(0, tb.UV);

        roof.transform.localPosition = Vector3.zero;

        if (geo.HasField("roof:material"))
        {
            var mat_name = geo.GetValueStringByKey("roof:material");

            var mat_by_tag = buildingMaterials.GetBuildingMaterialByName(mat_name);

            if(mat_by_tag != null)
            {
                roof.GetComponent<MeshRenderer>().material = mat_by_tag;
            }
        }

        roof.GetComponent<MeshRenderer>().material.SetColor("_Color", GR.SetOSMRoofColour(geo));
    }

}
