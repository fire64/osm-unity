using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LaderSystem : MonoBehaviour
{
    public TextMeshProUGUI buildingText;
    public TextMeshProUGUI roadsText;
    public TextMeshProUGUI watersText;
    public TextMeshProUGUI landuseText;
    public TextMeshProUGUI detailsText;

    public TextMeshProUGUI cityLogoText;
    public RawImage cityLogo;


    [SerializeField]
    BuildingMaker buildingSystem;
    [SerializeField]
    RoadMaker roadsSystem;
    [SerializeField]
    WaterMaker watersSystem;
    [SerializeField]
    LanduseMaker landuseSystem;
    [SerializeField]
    DetailMaker detailsSystem;
    [SerializeField]
    MapReader mapReader;

    bool isInit = false;

    public int countBuildings = 0;
    public int countRoads = 0;
    public int countWaters = 0;
    public int countLanduse = 0;
    public int countDetails = 0;

    private void OnEnable()
    {
        CompleteLoadingBroadCast.OnAllModulesLoaded += HandleAllModulesLoaded;
        if (CompleteLoadingBroadCast.IsAllLoaded)
        {
            HandleAllModulesLoaded();
        }
    }

    private void OnDisable()
    {
        CompleteLoadingBroadCast.OnAllModulesLoaded -= HandleAllModulesLoaded;
    }

    private void HandleAllModulesLoaded()
    {
        transform.gameObject.SetActive(false);
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    void GetCounts()
    {
        var buildingsWay = mapReader.ways.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Building && w.NodeIDs.Count > 1; });
        var buildingsRelations = mapReader.relations.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Building && w.NodeIDs.Count > 1; });
        countBuildings = buildingsWay.Count + buildingsRelations.Count;

        var roadsWay = mapReader.ways.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Road && w.NodeIDs.Count > 1; });
        var roadsRelations = mapReader.relations.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Road && w.NodeIDs.Count > 1; });
        countRoads = roadsWay.Count + roadsRelations.Count;

        var watersWay = mapReader.ways.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Water && w.NodeIDs.Count > 1; });
        var watersRelations = mapReader.relations.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Water && w.NodeIDs.Count > 1; });
        countWaters = watersWay.Count + watersRelations.Count;

        var LanduseWay = mapReader.ways.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Landuse && w.NodeIDs.Count > 1; });
        var LanduseRelations = mapReader.relations.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Landuse && w.NodeIDs.Count > 1; });
        countLanduse = LanduseWay.Count + LanduseRelations.Count;


        var DetailNodes = mapReader.nodeslist.FindAll((w) => { return w.objectType == BaseOsm.ObjectType.Detail; });
        countDetails = DetailNodes.Count;

    }

    // Update is called once per frame
    void Update()
    {
        if(!isInit)
        {
            if(mapReader.IsReady)
            {
                if(mapReader.LocationData != null)
                {
                    cityLogoText.transform.gameObject.SetActive(true);
                    cityLogoText.text = mapReader.LocationData.locationName;

                    cityLogo.transform.gameObject.SetActive(true);
                    cityLogo.texture = mapReader.LocationData.image;
                }

                GetCounts();

                isInit = true;
            }
            else
            {
                return;
            }
        }

        int curBuildings = buildingSystem.GetCountProcessing();
        int buildingPresent = (int)((float)curBuildings / (float)countBuildings * 100f);
        buildingText.text = "Загрузка зданий " + buildingPresent + "% (" + curBuildings + "/" + countBuildings + ")";

        int curRoads = roadsSystem.GetCountProcessing();
        int roadsPresent = (int)((float)curRoads / (float)countRoads * 100f);
        roadsText.text = "Загрузка дорог " + roadsPresent + "% (" + curRoads + "/" + countRoads + ")";

        int curWaters = watersSystem.GetCountProcessing();
        int watersPresent = (int)((float)curWaters / (float)countWaters * 100f);
        watersText.text = "Загрузка водоемов " + watersPresent + "% (" + curWaters + "/" + countWaters + ")";

        int curLanduses = landuseSystem.GetCountProcessing();
        int landusesPresent = (int)((float)curLanduses / (float)countLanduse * 100f);
        landuseText.text = "Загрузка ландшафтов " + landusesPresent + "% (" + curLanduses + "/" + countLanduse + ")";

        int curDetails = detailsSystem.GetCountProcessing();
        int detailsPresent = (int)((float)curDetails / (float)countDetails * 100f);
        detailsText.text = "Загрузка детализации " + detailsPresent + "% (" + curDetails + "/" + countDetails + ")";
    }
}
