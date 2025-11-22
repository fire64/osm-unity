using UnityEditor;
using UnityEngine;
using System.Collections;

public class ModelReadWriteEnabler : EditorWindow
{
    [MenuItem("Tools/Enable Model Read/Write")]
    static void EnableReadWriteForAllModels()
    {
        string[] modelGuids = AssetDatabase.FindAssets("t:Model");
        int processed = 0;
        int errors = 0;

        foreach (string guid in modelGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter modelImporter = AssetImporter.GetAtPath(path) as ModelImporter;

            if (modelImporter != null)
            {
                try
                {
                    if (!modelImporter.isReadable)
                    {
                        modelImporter.isReadable = true;
                        modelImporter.SaveAndReimport();
                    }
                    processed++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to process {path}: {e.Message}");
                    errors++;
                }
            }
        }

        Debug.Log($"Processed {processed} models. Errors: {errors}");
        AssetDatabase.Refresh();
    }
}