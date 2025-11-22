using UnityEngine;

public static class MeshAlignTools
{
    // -------------------------
    // 1) Центрирование pivot
    // -------------------------
    public static MeshFilter CenterPivot_CreateMeshRoot(GameObject model)
    {
        if (model == null) return null;
        MeshFilter mf = model.GetComponentInChildren<MeshFilter>(true);
        if (mf == null) return null;
        Mesh mesh = mf.sharedMesh;
        if (mesh == null) return mf;

        Vector3[] verts = mesh.vertices;
        if (verts == null || verts.Length == 0) return mf;

        // центроид локально (используется только чтобы сдвинуть root)
        Vector3 centroid = Vector3.zero;
        for (int i = 0; i < verts.Length; i++) centroid += verts[i];
        centroid /= verts.Length;

        GameObject root = new GameObject("MeshRoot");
        root.transform.SetParent(model.transform, false);
        mf.transform.SetParent(root.transform, false);

        // Сдвигаем MeshRoot так, чтобы локальный центроид стал в 0
        root.transform.localPosition = -centroid;

        return mf;
    }

    // -------------------------
    // 2) Выравнивание (основной)
    // -------------------------
    // modelRoot: трансформ верхнего объекта модели (тот который должен перемещаться)
    // detailedMF: MeshFilter внутри MeshRoot (после CenterPivot_CreateMeshRoot)
    // prototypeMF: MeshFilter прототипа (тот, на который приравниваем)
    public static void AlignMesh(Transform modelRoot, MeshFilter detailedMF, MeshFilter prototypeMF)
    {
        if (modelRoot == null || detailedMF == null || prototypeMF == null) return;
        if (detailedMF.sharedMesh == null || prototypeMF.sharedMesh == null) return;

        // Оси и центры, рассчитанные через min/max проекции (более точные)
        Vector3 dForward, dRight, dUp, dCenter;
        GetStableBuildingAxes_MinMax(detailedMF.sharedMesh, detailedMF.transform, out dForward, out dRight, out dUp, out dCenter);

        Vector3 pForward, pRight, pUp, pCenter;
        GetStableBuildingAxes_MinMax(prototypeMF.sharedMesh, prototypeMF.transform, out pForward, out pRight, out pUp, out pCenter);

        // Строим базисные матрицы
        Matrix4x4 D = Basis(dRight, dUp, dForward, dCenter);
        Matrix4x4 P = Basis(pRight, pUp, pForward, pCenter);

        // extents (min/max разницы) вдоль этих осей — используем ту же функцию, что и для центра
        Vector3 dExt = GetExtentAlongAxesWorld_MinMax(detailedMF.sharedMesh, detailedMF.transform, new Vector3[] { dRight, dUp, dForward });
        Vector3 pExt = GetExtentAlongAxesWorld_MinMax(prototypeMF.sharedMesh, prototypeMF.transform, new Vector3[] { pRight, pUp, pForward });

        // защита от нуля
        dExt.x = Mathf.Max(dExt.x, 1e-6f);
        dExt.y = Mathf.Max(dExt.y, 1e-6f);
        dExt.z = Mathf.Max(dExt.z, 1e-6f);

        Vector3 scaleVec = new Vector3(pExt.x / dExt.x, pExt.y / dExt.y, pExt.z / dExt.z);
        Matrix4x4 S = Matrix4x4.Scale(scaleVec);

        // M = P * S * D^-1
        Matrix4x4 M = P * S * D.inverse;

        // разложение матрицы на pos/rot/scale (в мировых)
        DecomposeMatrix(M, out Vector3 worldPos, out Quaternion worldRot, out Vector3 worldScale);

        // Применяем к modelRoot (сохранение мировых трансформ при временном отцеплении)
        Transform parent = modelRoot.parent;
        modelRoot.SetParent(null, true);

        modelRoot.position = worldPos;
        modelRoot.rotation = worldRot;
        modelRoot.localScale = worldScale;

        modelRoot.SetParent(parent, true);
    }

    // ==================================================================================
    // Функции: получение устойчивых осей + центр/extent через min/max проекции (точнее)
    // ==================================================================================

    /// <summary>
    /// Делаем 2D-PCA только чтобы найти направление фасада (forward) на плоскости XZ.
    /// Но центр и extents вычисляем через min/max проекции на оси right/up/forward (world).
    /// forward = старший eigen-vector на XZ (как и раньше).
    /// center = суммарная точка ( (min+max)/2 по каждой проекции ) в world-координатах.
    /// </summary>
    static void GetStableBuildingAxes_MinMax(Mesh mesh, Transform tf, out Vector3 forward, out Vector3 right, out Vector3 up, out Vector3 center)
    {
        Vector3[] verts = mesh.vertices;
        int n = verts.Length;
        if (n == 0)
        {
            forward = Vector3.forward;
            right = Vector3.right;
            up = Vector3.up;
            center = tf.position;
            return;
        }

        // 1) вычисляем PCA-ось на XZ (как раньше)
        Vector2 mean = Vector2.zero;
        for (int i = 0; i < n; i++)
        {
            Vector3 w = tf.TransformPoint(verts[i]);
            mean += new Vector2(w.x, w.z);
        }
        mean /= n;

        float xx = 0, xz = 0, zz = 0;
        for (int i = 0; i < n; i++)
        {
            Vector3 w = tf.TransformPoint(verts[i]);
            float dx = w.x - mean.x;
            float dz = w.z - mean.y;
            xx += dx * dx;
            xz += dx * dz;
            zz += dz * dz;
        }

        float T = xx + zz;
        float D = xx * zz - xz * xz;
        float discr = Mathf.Max(0f, (T * T) / 4f - D);
        float lambda1 = T / 2f + Mathf.Sqrt(discr);

        Vector2 ev;
        float a = lambda1 - zz;
        if (Mathf.Abs(xz) > 1e-9f)
            ev = new Vector2(a, xz).normalized;
        else
            ev = (xx >= zz) ? new Vector2(1, 0) : new Vector2(0, 1);

        forward = new Vector3(ev.x, 0f, ev.y).normalized;
        right = new Vector3(-forward.z, 0f, forward.x).normalized;
        up = Vector3.up;

        // 2) min/max проекции на right/up/forward — затем center = (min+max)/2 * axis
        float minR = float.MaxValue, maxR = float.MinValue;
        float minU = float.MaxValue, maxU = float.MinValue;
        float minF = float.MaxValue, maxF = float.MinValue;

        float sumY = 0f; // на случай, если хотим альтернативно взять среднюю высоту
        for (int i = 0; i < n; i++)
        {
            Vector3 w = tf.TransformPoint(verts[i]);
            float pr = Vector3.Dot(w, right);
            float pu = Vector3.Dot(w, up);
            float pf = Vector3.Dot(w, forward);

            if (pr < minR) minR = pr;
            if (pr > maxR) maxR = pr;
            if (pu < minU) minU = pu;
            if (pu > maxU) maxU = pu;
            if (pf < minF) minF = pf;
            if (pf > maxF) maxF = pf;

            sumY += w.y;
        }

        float centerR = (minR + maxR) * 0.5f;
        float centerU = (minU + maxU) * 0.5f;
        float centerF = (minF + maxF) * 0.5f;

        // перестраиваем center в world координаты
        center = right * centerR + up * centerU + forward * centerF;
    }

    // Extent вдоль axesWorld: возвращает (max-min) по каждой оси
    static Vector3 GetExtentAlongAxesWorld_MinMax(Mesh mesh, Transform tf, Vector3[] axesWorld)
    {
        if (axesWorld == null || axesWorld.Length != 3)
            return Vector3.zero;

        Vector3[] verts = mesh.vertices;
        int n = verts.Length;
        if (n == 0) return Vector3.zero;

        float[] minVals = new float[3] { float.MaxValue, float.MaxValue, float.MaxValue };
        float[] maxVals = new float[3] { float.MinValue, float.MinValue, float.MinValue };

        for (int i = 0; i < n; i++)
        {
            Vector3 w = tf.TransformPoint(verts[i]);
            for (int a = 0; a < 3; a++)
            {
                float p = Vector3.Dot(w, axesWorld[a]);
                if (p < minVals[a]) minVals[a] = p;
                if (p > maxVals[a]) maxVals[a] = p;
            }
        }

        return new Vector3(maxVals[0] - minVals[0], maxVals[1] - minVals[1], maxVals[2] - minVals[2]);
    }

    // -------------------------
    // Базовые матрицы и декомпозиция
    // -------------------------
    static Matrix4x4 Basis(Vector3 col0, Vector3 col1, Vector3 col2, Vector3 pos)
    {
        Matrix4x4 m = Matrix4x4.identity;
        m.SetColumn(0, new Vector4(col0.x, col0.y, col0.z, 0f));
        m.SetColumn(1, new Vector4(col1.x, col1.y, col1.z, 0f));
        m.SetColumn(2, new Vector4(col2.x, col2.y, col2.z, 0f));
        m.SetColumn(3, new Vector4(pos.x, pos.y, pos.z, 1f));
        return m;
    }

    static void DecomposeMatrix(Matrix4x4 m, out Vector3 position, out Quaternion rotation, out Vector3 scale)
    {
        // позиция из last column
        position = new Vector3(m.m03, m.m13, m.m23);

        Vector3 col0 = new Vector3(m.m00, m.m10, m.m20);
        Vector3 col1 = new Vector3(m.m01, m.m11, m.m21);
        Vector3 col2 = new Vector3(m.m02, m.m12, m.m22);

        float sx = col0.magnitude;
        float sy = col1.magnitude;
        float sz = col2.magnitude;
        scale = new Vector3(sx, sy, sz);

        if (sx > 1e-9f) col0 /= sx;
        if (sy > 1e-9f) col1 /= sy;
        if (sz > 1e-9f) col2 /= sz;

        Matrix4x4 rotMat = Matrix4x4.identity;
        rotMat.SetColumn(0, new Vector4(col0.x, col0.y, col0.z, 0f));
        rotMat.SetColumn(1, new Vector4(col1.x, col1.y, col1.z, 0f));
        rotMat.SetColumn(2, new Vector4(col2.x, col2.y, col2.z, 0f));

        rotation = Quaternion.LookRotation(rotMat.GetColumn(2), rotMat.GetColumn(1));
    }
}