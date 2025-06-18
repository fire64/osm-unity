using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NatureSettings", menuName = "Geocon/NatureSettings", order = 3)]

public class GrassSettings : ScriptableObject
{
    // Start is called before the first frame update
    [System.Serializable]
    public struct GrassSettingsInfo
    {
        [SerializeField]
        public GameObject m_Prototype;
        [SerializeField]
        public Texture2D m_PrototypeTexture;
        [SerializeField]
        public Color m_HealthyColor;
        [SerializeField]
        public Color m_DryColor;
        [SerializeField]
        public float m_MinWidth;
        [SerializeField]
        public float m_MaxWidth;
        [SerializeField]
        public float m_MinHeight;
        [SerializeField]
        public float m_MaxHeight;
        [SerializeField]
        public DetailRenderMode m_RenderMode;
        [SerializeField]
        public bool m_UsePrototypeMesh;
    }

    [SerializeField]
    public List<GrassSettingsInfo> GrassSettingsInfoList;

    [SerializeField]
    public List<GameObject> TreesList;

    public int GetCountTrees()
    {
        return TreesList.Count;
    }

    public TreePrototype GetTreeById(int id)
    {
        if (id >= TreesList.Count)
        {
            id = TreesList.Count - 1;
        }

        TreePrototype m_treeProtoType = new TreePrototype();
        m_treeProtoType.prefab = TreesList[id];

        return m_treeProtoType;
    }

    public int GetCountGrass()
    {
        return GrassSettingsInfoList.Count;
    }

    public DetailPrototype GetGrassById(int id)
    {
        if (id >= GrassSettingsInfoList.Count)
        {
            id = GrassSettingsInfoList.Count - 1;
        }

        DetailPrototype m_detailProtoType = new DetailPrototype();

        GrassSettingsInfo grassSettingsInfo = GrassSettingsInfoList[id];

        if (grassSettingsInfo.m_Prototype != null)
        {
            m_detailProtoType.prototype = grassSettingsInfo.m_Prototype;
        }

        if (grassSettingsInfo.m_PrototypeTexture != null)
        {
            m_detailProtoType.prototypeTexture = grassSettingsInfo.m_PrototypeTexture;
        }

        if (grassSettingsInfo.m_HealthyColor != null)
        {
            m_detailProtoType.healthyColor = grassSettingsInfo.m_HealthyColor;
        }

        if (grassSettingsInfo.m_DryColor != null)
        {
            m_detailProtoType.dryColor = grassSettingsInfo.m_DryColor;
        }

        if (grassSettingsInfo.m_MinWidth != 0)
        {
            m_detailProtoType.minWidth = grassSettingsInfo.m_MinWidth;
        }

        if (grassSettingsInfo.m_MaxWidth != 0)
        {
            m_detailProtoType.maxWidth = grassSettingsInfo.m_MaxWidth;
        }

        if (grassSettingsInfo.m_MinHeight != 0)
        {
            m_detailProtoType.minHeight = grassSettingsInfo.m_MinHeight;
        }

        if (grassSettingsInfo.m_MaxHeight != 0)
        {
            m_detailProtoType.maxHeight = grassSettingsInfo.m_MaxHeight;
        }

        m_detailProtoType.renderMode = grassSettingsInfo.m_RenderMode;

        m_detailProtoType.usePrototypeMesh = grassSettingsInfo.m_UsePrototypeMesh;

        return m_detailProtoType;
    }
}
