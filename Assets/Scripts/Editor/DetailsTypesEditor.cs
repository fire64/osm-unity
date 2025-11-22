using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DetailsTypes))]
public class DetailsTypesEditor : Editor
{
    private SerializedProperty detailsTypesList;
    private Gen3DModels modelGenerator;

    private Vector2 scrollPosition;
    private const float scrollAreaHeight = 500;
    private const float itemHeight = 120;

    private void OnEnable()
    {
        detailsTypesList = serializedObject.FindProperty("DetailsTypesReplacesList");
        modelGenerator = FindObjectOfType<Gen3DModels>();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(serializedObject, "DetailsTypesReplacesList");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Details Types List", EditorStyles.boldLabel);

        if (detailsTypesList == null || !detailsTypesList.isArray)
        {
            EditorGUILayout.HelpBox("DetailsTypesReplacesList is not found or not an array", MessageType.Error);
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(scrollAreaHeight));

        int firstIndex = Mathf.Max(0, (int)(scrollPosition.y / itemHeight) - 1);
        int lastIndex = Mathf.Min(detailsTypesList.arraySize - 1, firstIndex + (int)(scrollAreaHeight / itemHeight) + 2);

        GUILayout.Space(firstIndex * itemHeight);

        for (int i = firstIndex; i <= lastIndex; i++)
        {
            SerializedProperty item = detailsTypesList.GetArrayElementAtIndex(i);
            DrawItem(item);
        }

        GUILayout.Space((detailsTypesList.arraySize - lastIndex - 1) * itemHeight);

        EditorGUILayout.EndScrollView();

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add New Item"))
        {
            ((DetailsTypes)target).AddNewDetailsTypeInfo("NewDetailType");
        }

        if (GUILayout.Button("Delete Unused"))
        {
            ((DetailsTypes)target).DeleteUnused();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawItem(SerializedProperty item)
    {
        SerializedProperty detailsType = item.FindPropertyRelative("detailsType");
        SerializedProperty detailsDescription = item.FindPropertyRelative("detailsDescription");
        SerializedProperty isTempMarkerEnable = item.FindPropertyRelative("isTempMarkerEnable");
        SerializedProperty detailsPrefab = item.FindPropertyRelative("detailsPrefab");

        // Проверяем, заполнен ли detailsPrefab
        bool hasPrefab = detailsPrefab.objectReferenceValue != null;

        // Сохраняем оригинальный цвет фона
        Color originalColor = GUI.backgroundColor;

        // Если prefab заполнен, устанавливаем зеленый цвет фона
        if (hasPrefab)
        {
            GUI.backgroundColor = new Color(0.7f, 1f, 0.7f, 0.3f); // Светло-зеленый с прозрачностью
        }

        // Рисуем контейнер элемента с установленным цветом фона
        EditorGUILayout.BeginVertical("box", GUILayout.Height(itemHeight));

        // Восстанавливаем оригинальный цвет для остальных элементов
        GUI.backgroundColor = originalColor;

        EditorGUILayout.PropertyField(detailsType);
        EditorGUILayout.PropertyField(detailsDescription);
        EditorGUILayout.PropertyField(isTempMarkerEnable);
        EditorGUILayout.PropertyField(detailsPrefab);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        // Управляем активностью кнопки в зависимости от наличия prefab
        bool wasEnabled = GUI.enabled;
        GUI.enabled = !hasPrefab; // Делаем кнопку неактивной если есть prefab

        if (GUILayout.Button("Generate 3D Model", GUILayout.Width(150)))
        {
            if (modelGenerator != null)
            {
                modelGenerator.Generate3DModel(detailsType.stringValue);
            }
            else
            {
                Debug.LogError("Gen3DModels not found in scene! Please add a Gen3DModels component to a GameObject in the scene.");
            }
        }

        // Восстанавливаем состояние GUI
        GUI.enabled = wasEnabled;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }
}