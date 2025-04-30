using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(RoadSurfacesMaterials.RoadSurfacesMaterialReplace))]
public class RoadSurfacesMaterialsDrawe : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var dataProp = property.FindPropertyRelative("roadSurfacesMaterial");
        bool isDataSetSet = dataProp.objectReferenceValue != null;

        Color originalColor = GUI.color;
        if (isDataSetSet)
        {
            GUI.color = Color.green;
        }

        EditorGUI.PropertyField(position, property, label, true);
        GUI.color = originalColor;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}
