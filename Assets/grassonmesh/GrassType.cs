using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable] // <-- Эта строка добавлена
public class GrassType
{
    public string name = "Grass Type";
    public Texture2D grassTexture;
    [Header("Size")]
    public Vector2 sizeRange = new Vector2(0.8f, 1.2f);
    [Header("Appearance")]
    public Color grassColor = Color.white;
    [Header("Wind (if applicable)")]
    [Range(0, 0.5f)] public float windStrength = 0.1f;
    [Range(0, 2f)] public float windSpeed = 0.5f;

    [Range(0.1f, 10f)] public float density = 1.0f;
    [Range(0f, 90f)] public float maxSlope = 45f;

    // Новый: хранение сгенерированного меша
    public Mesh generatedGrassMesh;
    // Новый: хранение сгенерированного материала
    public Material generatedMaterial;

    public List<List<Matrix4x4>> batches = new List<List<Matrix4x4>>();
}