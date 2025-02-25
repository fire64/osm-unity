using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(DetailsTypes.DetailsTypesItem))]
public class DetailsTypesItemDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var prefabProp = property.FindPropertyRelative("detailsPrefab");
        bool isPrefabSet = prefabProp.objectReferenceValue != null;

        Color originalColor = GUI.color;
        if (isPrefabSet)
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