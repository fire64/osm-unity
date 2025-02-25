using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ColorByName.ColorReplace))]
public class ColorReplaceItemDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // �������� �������� �����
        var colorProp = property.FindPropertyRelative("color");

        // �������� �������� ����� ��������� ����� colorValue
        Color colorValue = colorProp.colorValue;

        // ��������� ���������� �� ���� �� ������ (������ ������������ �������� objectReferenceValue)
        bool isColorChanged = colorValue != Color.white;

        Color originalColor = GUI.color;
        if (isColorChanged)
        {
            // ������������� ���� GUI ��������� ���������� ��������
            GUI.color = colorValue;
        }

        // ������ ��������
        EditorGUI.PropertyField(position, property, label, true);

        // ��������������� ������������ ����
        GUI.color = originalColor;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}