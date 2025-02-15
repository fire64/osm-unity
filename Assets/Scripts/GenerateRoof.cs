using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEngine.UI.GridLayoutGroup;

public class GenerateRoof : MonoBehaviour
{
    public GameObject roof_onion;
    public GameObject roof_dome;
    public BuildingMaterials buildingMaterials;

    private void CreateHippedRoof(List<Vector3> baseCorners, float min_height, float height, MeshData data, Vector2 min, Vector2 size)
    {
        if (baseCorners.Count < 4)
        {
            Debug.Log("Недостаточно точек для построения крыши");
            return; // Нужно минимум 4 точки для четырехскатной крыши
        }

        Vector3 midpoint = Vector3.zero;
        foreach (var point in baseCorners)
        {
            midpoint += point; // Суммируем все точки, чтобы найти центр
        }
        midpoint /= baseCorners.Count; // Находим среднюю точку
        midpoint += Vector3.up * height; // Поднимаем среднюю точку на высоту крыши

        Vector3[] vertices = new Vector3[baseCorners.Count + 1];
        for (int i = 0; i < baseCorners.Count; i++)
        {
            Vector3 curpoint = baseCorners[i];
            curpoint.y = min_height;
            vertices[i] = curpoint;
        }

        vertices[baseCorners.Count] = midpoint; // Добавляем вершину крыши

        int[] triangles = new int[baseCorners.Count * 3];
        for (int i = 0, j = 0; i < triangles.Length; i += 3, j++)
        {
            triangles[i] = j;
            triangles[i + 1] = (j + 1) % baseCorners.Count;
            triangles[i + 2] = baseCorners.Count; // Вершина крыши всегда последняя точка в массиве
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            data.Vertices.Add(vertices[i]);
        }

        for (int i = 0; i < triangles.Length; i++)
        {
            data.Indices.Add(triangles[i]);
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

    private Vector3 CalculateCentroid(List<Vector3> vertices)
    {
        if (vertices == null || vertices.Count == 0)
            return Vector3.zero;

        Vector3 centroid = Vector3.zero;
        foreach (var vertex in vertices)
        {
            centroid += vertex;
        }
        return centroid / vertices.Count;
    }

    private void CreateGambrelRoof(List<Vector3> baseCorners, float min_height, float height, MeshData data, Vector2 min, Vector2 size)
    {
        // Минимально допустимое количество вершин - 3 (треугольник)
        if (baseCorners.Count < 3)
        {
            Debug.LogError("The base must contain at least 3 corners.");
            return;
        }

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        // 1. Определение центра масс основания
        Vector3 center = CalculateCentroid(baseCorners);
        center += Vector3.up * min_height; // Поднимаем центр на minHeight, делая крышу выше

        // 2. Добавление вершины крыши на заданной высоте
        Vector3 peak = center + Vector3.up * (height - min_height);
        vertices.Add(peak); // первая вершина - пик крыши

        // 3. Добавление основных вершин и формирование треугольников крыши
        foreach (Vector3 corner in baseCorners)
        {
            Vector3 curpoint = corner;
            curpoint.y = min_height;
            vertices.Add(curpoint); // Добавляем основные вершины
        }

        for (int i = 1; i <= baseCorners.Count; i++)
        {
            // Создаем треугольники, соединяющие вершины основания с пиком
            triangles.Add(0); // Пик крыши
            triangles.Add(i);
            triangles.Add(i % baseCorners.Count + 1);
        }

        for (int i = 0; i < vertices.Count; i++)
        {
            data.Vertices.Add(vertices[i]);
        }

        for (int i = 0; i < triangles.Count; i++)
        {
            data.Indices.Add(triangles[i]);
        }
    }

    private void CreateGabledRoof(List<Vector3> baseCorners, float min_height, float height, MeshData data, Vector2 min, Vector2 size)
    {
        if (baseCorners == null || baseCorners.Count < 3)
        {
            Debug.LogError("Base points should form a closed figure with at least 3 points.");
            return;
        }

        Vector3 centroid = CalculateCentroid(baseCorners);
        centroid += Vector3.up * height; // Поднимаем центроид до высоты крыши

        List<Vector3> vertices = new List<Vector3>(baseCorners) { centroid }; // Добавляем центроид как вершину

        List<int> triangles = new List<int>();
        for (int i = 0; i < baseCorners.Count; i++)
        {
            int nextIndex = (i + 1) % baseCorners.Count;
            triangles.Add(i);
            triangles.Add(nextIndex);
            triangles.Add(vertices.Count - 1); // Центроид всегда последняя вершина
        }

        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 curpoint = vertices[i];

            if (curpoint.y == 0.0f)
                curpoint.y = min_height;

            data.Vertices.Add(curpoint);
        }

        for (int i = 0; i < triangles.Count; i++)
        {
            data.Indices.Add(triangles[i]);
        }
    }

    private void CreateDomeRoof(List<Vector3> corners, float min_height, float height, MeshData data, Vector2 min, Vector2 size)
    {
        GameObject go = Instantiate(roof_dome) as GameObject;

        var onionRoof = go.GetComponent<MeshFilter>().mesh;

        var verticesls = onionRoof.vertices;
        var triangles = onionRoof.triangles;

        for (int i = 0; i < verticesls.Length; i++)
        {
            var verticle = verticesls[i];

            float scale_fator = (Mathf.Min(size.x, size.y) / 2) * 100;

            data.Vertices.Add(new Vector3(verticle.x * scale_fator, verticle.y * scale_fator + min_height, verticle.z * scale_fator));
        }

        for (int i = 0; i < triangles.Length; i++)
        {
            data.Indices.Add(triangles[i]);
        }

        Destroy(go);
    }


    // Start is called before the first frame update
    public void GenerateRoofForBuillding(GameObject building, List<Vector3> corners, float minHeight, float height, Vector2 min, Vector2 size, BaseOsm geo)
    {
        var roof = new GameObject("roof");

        roof.transform.SetParent(building.transform);

        var mesh = roof.AddComponent<MeshFilter>().mesh;

        roof.AddComponent<MeshRenderer>();

        float roof_height = 0.01f;

        if (geo.HasField("roof:height"))
        {
            roof_height = geo.GetValueFloatByKey("roof:height");
        }

        var roof_type = "flat";

        if (geo.HasField("roof:shape"))
        {
            roof_type = geo.GetValueStringByKey("roof:shape");
        }

        if (geo.HasField("roof:angle"))
        {

        }

        var tb = new MeshData();

        if (roof_type == "flat")
        {
            GR.CreateMeshWithHeight(corners, minHeight + height, roof_height, tb);
        }
        else if (roof_type == "hipped")
        {
            if (!geo.HasField("roof:height"))
            {
                roof_height = 6.0f;
            }

            CreateHippedRoof(corners, height, height + roof_height, tb, min, size);
        }
        else if (roof_type == "gabled")
        {
            if (!geo.HasField("roof:height"))
            {
                roof_height = 6.0f;
            }

            CreateGabledRoof(corners, height, height + roof_height, tb, min, size);
        }
        else if (roof_type == "gambrel")
        {
            if (!geo.HasField("roof:height"))
            {
                roof_height = 6.0f;
            }

            CreateGambrelRoof(corners, height, height + roof_height, tb, min, size);
        }
        else if (roof_type == "dome")
        {
            if (!geo.HasField("roof:height"))
            {
                roof_height = 1.0f;
            }

            CreateDomeRoof(corners, minHeight + height, minHeight + height + roof_height, tb, min, size);
        }
        else if (roof_type == "pyramidal")
        {
            if (!geo.HasField("roof:height"))
            {
                roof_height = 1.0f;
            }

            CreatePyramidalRoof(corners, minHeight + height, minHeight + height + roof_height, tb, min, size);
        }
        else if (roof_type == "onion")
        {
            if (!geo.HasField("roof:height"))
            {
                roof_height = 1.0f;
            }

            CreateOnionRoof(corners, minHeight + height, minHeight + height + roof_height, tb, min, size);
        }
        else
        {
            //Not supported, use flat
            GR.CreateMeshWithHeight(corners, minHeight + height, roof_height, tb);
            Debug.Log("Unknown rooftype: " + roof_type);
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
