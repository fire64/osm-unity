using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class ColumnMaker : DetailBase
{
    public new void ActivateObject()
    {
        Detail detailinfo = transform.parent.GetComponent<Detail>();

        if(detailinfo)
        {
            CreateColumn(detailinfo);
        }
    }

    private void CreateColumn(Detail detailinfo)
    {
        float min_height = 0.0f;

        if (detailinfo.HasField("min_height"))
        {
            min_height = detailinfo.GetValueFloatByKey("min_height");
        }

        float height = 2.0f;

        if (detailinfo.HasField("height"))
        {
            height = detailinfo.GetValueFloatByKey("height");
        }

        float width = 0.5f;

        // ��������� ����������, ���� �� ���
        if (!gameObject.GetComponent<MeshFilter>())
            gameObject.AddComponent<MeshFilter>();
        if (!gameObject.GetComponent<MeshRenderer>())
            gameObject.AddComponent<MeshRenderer>();

        var mesh = gameObject.GetComponent<MeshFilter>().mesh;
        mesh.Clear();

        // ��������� ��������
        int segments = 16; // ���������� ��������� ����������
        float radius = width;
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // ����������� ����� ���������
        vertices.Add(new Vector3(0, min_height, 0)); // 0
        uvs.Add(new Vector2(0.5f, 0.5f));
        vertices.Add(new Vector3(0, height, 0));     // 1
        uvs.Add(new Vector2(0.5f, 0.5f));

        // ��������� ������ ��� ��������� � ������� �����������
        for (int i = 0; i < segments; i++)
        {
            // ������ ���������
            float angle = 2 * Mathf.PI * i / segments;
            Vector3 bottomPos = new Vector3(
                Mathf.Cos(angle) * radius,
                min_height,
                Mathf.Sin(angle) * radius
            );
            vertices.Add(bottomPos);
            uvs.Add(new Vector2((Mathf.Cos(angle) + 1) * 0.5f, (Mathf.Sin(angle) + 1) * 0.5f));

            // ������� ���������
            Vector3 topPos = new Vector3(
                Mathf.Cos(angle) * radius,
                height,
                Mathf.Sin(angle) * radius
            );
            vertices.Add(topPos);
            uvs.Add(new Vector2((Mathf.Cos(angle) + 1) * 0.5f, (Mathf.Sin(angle) + 1) * 0.5f));
        }

        // ���������� ������������� ��� ���������
        for (int i = 0; i < segments; i++)
        {
            // ������ ���������
            int bottomIndex = 2 + i * 2;
            triangles.Add(2 + ((i + 1) % segments) * 2);
            triangles.Add(bottomIndex);
            triangles.Add(0);

            // ������� ���������
            int topIndex = 3 + i * 2;
            triangles.Add(topIndex);
            triangles.Add(3 + ((i + 1) % segments) * 2);
            triangles.Add(1);
        }

        // ������� �����������
        for (int i = 0; i < segments; i++)
        {
            int currentBottom = 2 + i * 2;
            int nextBottom = 2 + ((i + 1) % segments) * 2;
            int currentTop = currentBottom + 1;
            int nextTop = nextBottom + 1;

            // ���� �� ���� �������������
            triangles.Add(currentBottom);
            triangles.Add(nextBottom);
            triangles.Add(currentTop);

            triangles.Add(currentTop);
            triangles.Add(nextBottom);
            triangles.Add(nextTop);
        }

        // ��������� ������ � ����
        mesh.vertices = vertices.ToArray();

        triangles.Reverse();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();

        gameObject.GetComponent<MeshRenderer>().material = null;

        Color columnColor = Color.white;

        if (detailinfo.HasField("colour"))
        {
            columnColor = GR.hexToColor(detailinfo.GetValueStringByKey("colour"));
        }

        gameObject.GetComponent<MeshRenderer>().material.SetColor("_Color", columnColor);

        // ��������� ������� � �������
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
    }
}
