using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class PBRCreator : EditorWindow
{
    private Material selectedMaterial;
    private Texture2D diffuseMap;

    // Настройки генерации
    private bool generateNormal = true;
    private bool generateMetallic = true;
    private bool generateSmoothness = true;
    private bool generateOcclusion = true;
    private bool generateHeightMap = true;

    // Параметры генерации
    private float normalStrength = 1.0f;
    private float metallicStrength = 0.5f;
    private float smoothnessStrength = 0.5f;
    private float occlusionStrength = 1.0f;
    private float heightStrength = 0.05f;

    private Vector2 scrollPosition;

    [MenuItem("Tools/PBR Creator")]
    public static void ShowWindow()
    {
        GetWindow<PBRCreator>("PBR Creator");
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        GUILayout.Label("PBR Material Transformation", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        selectedMaterial = (Material)EditorGUILayout.ObjectField("Material", selectedMaterial, typeof(Material), false);

        if (selectedMaterial != null)
        {
            diffuseMap = (Texture2D)selectedMaterial.mainTexture;
            if (diffuseMap == null)
            {
                EditorGUILayout.HelpBox("Selected material has no diffuse texture!", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            // Проверяем и включаем чтение/запись для текстуры
            string texturePath = AssetDatabase.GetAssetPath(diffuseMap);
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer != null && !importer.isReadable)
            {
                EditorGUILayout.HelpBox("Diffuse texture is not readable. Click 'Make Readable' button below.", MessageType.Warning);
                if (GUILayout.Button("Make Readable"))
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }
                EditorGUILayout.EndScrollView();
                return;
            }
        }

        EditorGUILayout.Space();
        GUILayout.Label("Generation Settings", EditorStyles.boldLabel);

        // Настройки генерации карт
        generateNormal = EditorGUILayout.Toggle("Generate Normal Map", generateNormal);
        if (generateNormal)
        {
            normalStrength = EditorGUILayout.Slider("Normal Strength", normalStrength, 0.1f, 5.0f);
        }

        generateMetallic = EditorGUILayout.Toggle("Generate Metallic Map", generateMetallic);
        if (generateMetallic)
        {
            metallicStrength = EditorGUILayout.Slider("Metallic Strength", metallicStrength, 0.0f, 1.0f);
        }

        generateSmoothness = EditorGUILayout.Toggle("Generate Smoothness Map", generateSmoothness);
        if (generateSmoothness)
        {
            smoothnessStrength = EditorGUILayout.Slider("Smoothness Strength", smoothnessStrength, 0.0f, 1.0f);
        }

        generateOcclusion = EditorGUILayout.Toggle("Generate Occlusion Map", generateOcclusion);
        if (generateOcclusion)
        {
            occlusionStrength = EditorGUILayout.Slider("Occlusion Strength", occlusionStrength, 0.1f, 5.0f);
        }

        generateHeightMap = EditorGUILayout.Toggle("Generate Height Map", generateHeightMap);
        if (generateHeightMap)
        {
            heightStrength = EditorGUILayout.Slider("Height Strength", heightStrength, 0.01f, 0.1f);
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Transform to PBR") && selectedMaterial != null)
        {
            TransformToPBR();
        }

        EditorGUILayout.EndScrollView();
    }

    private void TransformToPBR()
    {
        string path = AssetDatabase.GetAssetPath(selectedMaterial);
        string materialDir = Path.GetDirectoryName(path);
        string materialName = Path.GetFileNameWithoutExtension(path);

        // Создаем новый PBR материал или используем существующий
        Material pbrMaterial = new Material(Shader.Find("Standard"));
        pbrMaterial.CopyPropertiesFromMaterial(selectedMaterial);
        pbrMaterial.name = materialName + "_PBR";

        List<string> generatedTextures = new List<string>();

        // Генерация карт
        if (generateNormal)
        {
            Texture2D normalMap = GenerateNormalMap(diffuseMap, normalStrength);
            string normalPath = SaveTexture(normalMap, materialDir, pbrMaterial.name + "_Normal");
            generatedTextures.Add(normalPath);
            pbrMaterial.SetTexture("_BumpMap", normalMap);
            pbrMaterial.EnableKeyword("_NORMALMAP");
            pbrMaterial.SetFloat("_BumpScale", 1.0f);
        }

        if (generateMetallic || generateSmoothness)
        {
            Texture2D metallicSmoothnessMap = GenerateMetallicSmoothnessMap(diffuseMap, metallicStrength, smoothnessStrength, generateMetallic, generateSmoothness);
            string metallicSmoothnessPath = SaveTexture(metallicSmoothnessMap, materialDir, pbrMaterial.name + "_MetallicSmoothness");
            generatedTextures.Add(metallicSmoothnessPath);
            pbrMaterial.SetTexture("_MetallicGlossMap", metallicSmoothnessMap);
            pbrMaterial.EnableKeyword("_METALLICGLOSSMAP");

            if (generateMetallic) pbrMaterial.SetFloat("_Metallic", 1.0f);
            if (generateSmoothness) pbrMaterial.SetFloat("_Glossiness", 1.0f);
        }

        if (generateOcclusion)
        {
            Texture2D occlusionMap = GenerateOcclusionMap(diffuseMap, occlusionStrength);
            string occlusionPath = SaveTexture(occlusionMap, materialDir, pbrMaterial.name + "_Occlusion");
            generatedTextures.Add(occlusionPath);
            pbrMaterial.SetTexture("_OcclusionMap", occlusionMap);
        }

        if (generateHeightMap)
        {
            Texture2D heightMap = GenerateHeightMap(diffuseMap, heightStrength);
            string heightPath = SaveTexture(heightMap, materialDir, pbrMaterial.name + "_Height");
            generatedTextures.Add(heightPath);
            pbrMaterial.SetTexture("_ParallaxMap", heightMap);
            pbrMaterial.EnableKeyword("_PARALLAXMAP");
            pbrMaterial.SetFloat("_Parallax", heightStrength);
        }

        // Сохраняем материал
        string materialPath = materialDir + "/" + pbrMaterial.name + ".mat";
        AssetDatabase.CreateAsset(pbrMaterial, materialPath);

        // Обновляем импорт всех созданных текстур
        foreach (string texturePath in generatedTextures)
        {
            UpdateTextureImportSettings(texturePath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("PBR transformation complete! Material saved as: " + pbrMaterial.name);
        Selection.activeObject = pbrMaterial;
    }

    private Texture2D GenerateNormalMap(Texture2D diffuse, float strength)
    {
        Texture2D normalMap = new Texture2D(diffuse.width, diffuse.height, TextureFormat.RGBA32, true);
        Color[] pixels = new Color[diffuse.width * diffuse.height];

        for (int y = 0; y < diffuse.height; y++)
        {
            for (int x = 0; x < diffuse.width; x++)
            {
                // Получаем соседние пиксели
                Color pixelLeft = GetPixel(diffuse, x - 1, y);
                Color pixelRight = GetPixel(diffuse, x + 1, y);
                Color pixelUp = GetPixel(diffuse, x, y - 1);
                Color pixelDown = GetPixel(diffuse, x, y + 1);

                // Вычисляем градиенты
                float gradientLeft = pixelLeft.grayscale;
                float gradientRight = pixelRight.grayscale;
                float gradientUp = pixelUp.grayscale;
                float gradientDown = pixelDown.grayscale;

                // Вычисляем нормаль с использованием фильтра Собеля
                float xNormal = (gradientRight - gradientLeft) * strength;
                float yNormal = (gradientDown - gradientUp) * strength;
                float zNormal = 1.0f;

                // Нормализуем и преобразуем в цвет
                Vector3 normal = new Vector3(xNormal, yNormal, zNormal).normalized;
                Color normalColor = new Color(
                    normal.x * 0.5f + 0.5f,
                    normal.y * 0.5f + 0.5f,
                    normal.z * 0.5f + 0.5f,
                    1.0f
                );

                pixels[y * diffuse.width + x] = normalColor;
            }
        }

        normalMap.SetPixels(pixels);
        normalMap.Apply();
        return normalMap;
    }

    private Texture2D GenerateMetallicSmoothnessMap(Texture2D diffuse, float metallicStr, float smoothnessStr, bool genMetallic, bool genSmoothness)
    {
        Texture2D metallicSmoothnessMap = new Texture2D(diffuse.width, diffuse.height, TextureFormat.RGBA32, true);
        Color[] pixels = diffuse.GetPixels();

        for (int i = 0; i < pixels.Length; i++)
        {
            float grayscale = pixels[i].grayscale;
            float metallic = genMetallic ? grayscale * metallicStr : 0;
            float smoothness = genSmoothness ? (1.0f - grayscale) * smoothnessStr : 0;

            pixels[i] = new Color(metallic, 0, 0, smoothness);
        }

        metallicSmoothnessMap.SetPixels(pixels);
        metallicSmoothnessMap.Apply();
        return metallicSmoothnessMap;
    }

    private Texture2D GenerateOcclusionMap(Texture2D diffuse, float strength)
    {
        Texture2D occlusionMap = new Texture2D(diffuse.width, diffuse.height, TextureFormat.RGBA32, true);
        Color[] pixels = diffuse.GetPixels();

        for (int i = 0; i < pixels.Length; i++)
        {
            float occlusion = Mathf.Clamp01(1.0f - pixels[i].grayscale * strength);
            pixels[i] = new Color(occlusion, occlusion, occlusion, 1);
        }

        occlusionMap.SetPixels(pixels);
        occlusionMap.Apply();
        return occlusionMap;
    }

    private Texture2D GenerateHeightMap(Texture2D diffuse, float strength)
    {
        Texture2D heightMap = new Texture2D(diffuse.width, diffuse.height, TextureFormat.RGBA32, true);
        Color[] pixels = diffuse.GetPixels();

        for (int i = 0; i < pixels.Length; i++)
        {
            float height = pixels[i].grayscale * strength;
            pixels[i] = new Color(height, height, height, 1);
        }

        heightMap.SetPixels(pixels);
        heightMap.Apply();
        return heightMap;
    }

    private Color GetPixel(Texture2D texture, int x, int y)
    {
        // Обеспечиваем корректные координаты
        x = Mathf.Clamp(x, 0, texture.width - 1);
        y = Mathf.Clamp(y, 0, texture.height - 1);
        return texture.GetPixel(x, y);
    }

    private string SaveTexture(Texture2D texture, string directory, string name)
    {
        string path = directory + "/" + name + ".png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        return path;
    }

    private void UpdateTextureImportSettings(string path)
    {
        AssetDatabase.Refresh();

        // Настраиваем импорт текстуры
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            if (path.Contains("Normal"))
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
            }

            importer.sRGBTexture = !path.Contains("MetallicSmoothness") && !path.Contains("Normal");
            importer.SaveAndReimport();
        }
    }
}