using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ColorByName.ColorReplace))]
public class ColorReplaceItemDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Получаем свойство цвета
        var colorProp = property.FindPropertyRelative("color");

        // Получаем значение цвета правильно через colorValue
        Color colorValue = colorProp.colorValue;

        // Проверяем отличается ли цвет от белого (вместо неправильной проверки objectReferenceValue)
        bool isColorChanged = colorValue != Color.white;

        Color originalColor = GUI.color;
        if (isColorChanged)
        {
            // Устанавливаем цвет GUI используя полученное значение
            GUI.color = colorValue;
        }

        // Рисуем свойство
        EditorGUI.PropertyField(position, property, label, true);

        // Восстанавливаем оригинальный цвет
        GUI.color = originalColor;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}