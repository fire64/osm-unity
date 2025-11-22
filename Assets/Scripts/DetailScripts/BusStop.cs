using System;
using System.Collections.Generic;
using TMPro;
using Unity.Burst.Intrinsics;
using UnityEngine;

class BusStop : DetailBase
{
    // Start is called before the first frame update
    public TMP_Text m_PlatformName1;
    public TMP_Text m_PlatformName2;

    public TMP_Text m_RoutesNames1;
    public TMP_Text m_RoutesNames2;

    TransportPlatform platforminfo;

    TransportElementsMaker transportElementsMaker;
    RouteMaker routeMaker;

    public TransportStopPosition stoppoint;
    public List<Route> routes;

    public GameObject busPrefab;

    private void OnEnable()
    {
        routes = new List<Route>();

        // Подписка на событие при включении объекта
        CompleteLoadingBroadCast.OnAllModulesLoaded += HandleAllModulesLoaded;

        // Если данные уже загружены, вызываем обработчик сразу
        if (CompleteLoadingBroadCast.IsAllLoaded)
        {
            HandleAllModulesLoaded();
        }
    }

    private void OnDisable()
    {
        // Отписка при выключении объекта для избежания ошибок
        CompleteLoadingBroadCast.OnAllModulesLoaded -= HandleAllModulesLoaded;
    }

    private void HandleAllModulesLoaded()
    {
        FindStopPoint();
        AddUsedRoutes();
        SpawnBus();
    }

    private void SpawnBus()
    {
        if (!stoppoint)
            return;

        int routescount = routes.Count;

        int routeid = UnityEngine.Random.Range(0, routescount);

        if (routeid >= routes.Count || routeid < 1)
            return;

        GameObject curBus = Instantiate(busPrefab, stoppoint.transform.position, Quaternion.identity);

        AutoBus busScript = curBus.GetComponent<AutoBus>();

        busScript.SetRoute(routes[routeid]);
    }

    private void FindStopPoint()
    {
        // Получаем все объекты типа Detail
        float radius = 100;
        var stoppositions = transportElementsMaker.stoppositions;
        TransportStopPosition nearestStopPosition = null;
        float minDistance = Mathf.Infinity;

        foreach (TransportStopPosition stopposition in stoppositions)
        {
            // Проверяем расстояние между текущим объектом и stopposition
            float distance = Vector3.Distance(transform.position, stopposition.transform.position);

            // Если объект в радиусе и ближе предыдущих найденных
            if (distance <= radius && distance < minDistance)
            {
                minDistance = distance;
                nearestStopPosition = stopposition;
            }
        }

        stoppoint = nearestStopPosition;

        if(stoppoint != null)
        {
            transform.LookAt(stoppoint.transform);
        }
    } 

    private void AddUsedRoutes()
    {
        var curRoutes = routeMaker.routes;

        foreach (var route in curRoutes)
        {
            if (route.platformsid != null && route.platformsid.Count > 0)
            {
                foreach (var platform in route.platformsid)
                {
                    if(platforminfo != null && platform.ToString().Equals(platforminfo.Id))
                    {
//                      Debug.Log( "Fount platform: " + platforminfo.Id + " in route: " + route.Id);
                        routes.Add(route);
                        break;
                    }
                }
            }
        }

        string routesinfo = "";

        int count_routes = routes.Count;

        count_routes = Math.Min(count_routes, 7); //Max 7 station

        for ( int i = 0; i < count_routes; i++)
        {
            var route = routes[i];

            if(route.HasField("ref"))
            {
                var numbertrans = route.GetValueStringByKey("ref");

                var endstation = "";

                if (route.HasField("to"))
                {
                    endstation = route.GetValueStringByKey("to");
                }

                string delitel = "";

                int len_number = numbertrans.Length;

                if(len_number == 1)
                {
                    delitel = "           ";
                }
                else if(len_number == 2)
                {
                    delitel = "         ";
                }
                else if (len_number == 3)
                {
                    delitel = "       ";
                }
                else if (len_number == 4)
                {
                    delitel = "     ";
                }
                else if (len_number == 5)
                {
                    delitel = "   ";
                }

                routesinfo = routesinfo + numbertrans + delitel + endstation + "\n";
            }
        }

        m_RoutesNames1.text = routesinfo;
        m_RoutesNames2.text = routesinfo;
    }

    new public void ActivateObject()
    {
        platforminfo = transform.parent.GetComponent<TransportPlatform>();

        transportElementsMaker = FindFirstObjectByType<TransportElementsMaker>();

        routeMaker = FindFirstObjectByType<RouteMaker>();

        if (platforminfo)
        {
            CreateBusInfo();
        }
    }

    private void CreateBusInfo()
    {
        if (platforminfo.HasField("name"))
        {
            var name = platforminfo.GetValueStringByKey("name");

            m_PlatformName1.text = name;
            m_PlatformName2.text = name;
        }
    }
}
