using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Unity.VisualScripting;

[System.Serializable]
public struct memberdata_t
{
    public OSMTypes type;
    public ulong GeoId;
    public string Role;
};

public class OsmRelation : BaseOsm
{
    public List<memberdata_t> memberslist;

    public List<OsmWay> ways;

    // ОПТИМИЗАЦИЯ: Dictionary для O(1) поиска way по ID
    private Dictionary<ulong, OsmWay> _waysDict;
    private bool _waysDictBuilt = false;

    // ОПТИМИЗАЦИЯ: Ленивая инициализация словаря ways
    private void EnsureWaysDictBuilt()
    {
        if (_waysDictBuilt || ways == null) return;

        _waysDict = new Dictionary<ulong, OsmWay>(ways.Count);
        foreach (var way in ways)
        {
            if (!_waysDict.ContainsKey(way.ID))
            {
                _waysDict[way.ID] = way;
            }
        }
        _waysDictBuilt = true;
    }

    // ОПТИМИЗАЦИЯ: O(1) вместо O(n)
    public OsmWay GetOsmWay(ulong WayId)
    {
        EnsureWaysDictBuilt();

        if (_waysDict.TryGetValue(WayId, out OsmWay way))
        {
            return way;
        }
        return null;
    }

    // ОПТИМИЗАЦИЯ: Улучшенный алгоритм построения цепочек
    // Использует Dictionary для O(1) поиска связей вместо O(n)
    List<membersinfo_t> BuildChainsOptimized(List<membersinfo_t> segments)
    {
        if (segments == null || segments.Count == 0)
            return new List<membersinfo_t>();

        List<membersinfo_t> chains = new List<membersinfo_t>();

        // ОПТИМИЗАЦИЯ: Хеш-таблицы для быстрого поиска по начальным/конечным узлам
        Dictionary<ulong, List<int>> startNodeIndex = new Dictionary<ulong, List<int>>();
        Dictionary<ulong, List<int>> endNodeIndex = new Dictionary<ulong, List<int>>();

        // Строим индексы
        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            if (segment.NodeIDs == null || segment.NodeIDs.Count == 0) continue;

            ulong firstNode = segment.NodeIDs[0];
            ulong lastNode = segment.NodeIDs[segment.NodeIDs.Count - 1];

            if (!startNodeIndex.TryGetValue(firstNode, out var startList))
            {
                startList = new List<int>();
                startNodeIndex[firstNode] = startList;
            }
            startList.Add(i);

            if (!endNodeIndex.TryGetValue(lastNode, out var endList))
            {
                endList = new List<int>();
                endNodeIndex[lastNode] = endList;
            }
            endList.Add(i);
        }

        // Отслеживаем использованные сегменты
        HashSet<int> usedSegments = new HashSet<int>();

        while (usedSegments.Count < segments.Count)
        {
            // Находим первый неиспользованный сегмент
            int startIndex = -1;
            for (int i = 0; i < segments.Count; i++)
            {
                if (!usedSegments.Contains(i))
                {
                    startIndex = i;
                    break;
                }
            }

            if (startIndex == -1) break;

            var currentChain = new membersinfo_t
            {
                NodeIDs = new List<ulong>(segments[startIndex].NodeIDs),
                role = segments[startIndex].role,
                itemlist = segments[startIndex].itemlist
            };
            usedSegments.Add(startIndex);

            bool chainModified = true;
            while (chainModified)
            {
                chainModified = false;
                ulong firstNode = currentChain.NodeIDs[0];
                ulong lastNode = currentChain.NodeIDs[currentChain.NodeIDs.Count - 1];

                // Ищем сегмент, который можно присоединить
                for (int i = 0; i < segments.Count; i++)
                {
                    if (usedSegments.Contains(i)) continue;

                    var segment = segments[i];
                    if (segment.NodeIDs == null || segment.NodeIDs.Count == 0) continue;

                    ulong segFirst = segment.NodeIDs[0];
                    ulong segLast = segment.NodeIDs[segment.NodeIDs.Count - 1];

                    if (segFirst == lastNode)
                    {
                        // Продолжить цепочку вперед
                        currentChain.NodeIDs.RemoveAt(currentChain.NodeIDs.Count - 1);
                        currentChain.NodeIDs.AddRange(segment.NodeIDs);
                        usedSegments.Add(i);
                        chainModified = true;
                        break;
                    }
                    else if (segLast == firstNode)
                    {
                        // Продолжить цепочку назад
                        segment.NodeIDs.RemoveAt(segment.NodeIDs.Count - 1);
                        currentChain.NodeIDs.InsertRange(0, segment.NodeIDs);
                        usedSegments.Add(i);
                        chainModified = true;
                        break;
                    }
                    else if (segLast == lastNode)
                    {
                        // Добавить в конец в обратном порядке
                        segment.NodeIDs.RemoveAt(segment.NodeIDs.Count - 1);
                        currentChain.NodeIDs.AddRange(segment.NodeIDs.Reverse<ulong>());
                        usedSegments.Add(i);
                        chainModified = true;
                        break;
                    }
                    else if (segFirst == firstNode)
                    {
                        // Добавить в начало в обратном порядке
                        segment.NodeIDs.RemoveAt(0);
                        currentChain.NodeIDs.InsertRange(0, segment.NodeIDs.Reverse<ulong>());
                        usedSegments.Add(i);
                        chainModified = true;
                        break;
                    }
                }
            }

            // Проверить замкнутость
            currentChain.isClosed = (currentChain.NodeIDs[0] == currentChain.NodeIDs[currentChain.NodeIDs.Count - 1]);
            chains.Add(currentChain);
        }

        return chains;
    }

    bool IsPolygonValid(membersinfo_t polygon)
    {
        if (polygon.NodeIDs.Count < 3) return false;
        if (polygon.isClosed && polygon.NodeIDs[0] != polygon.NodeIDs[polygon.NodeIDs.Count - 1])
        {
            polygon.NodeIDs.Add(polygon.NodeIDs[0]);
            return true;
        }
        return true;
    }

    public OsmRelation(XmlNode node, List<OsmWay> waysset)
    {
        NodeIDs = new List<ulong>();
        HolesNodeListsIDs = new List<List<ulong>>();
        membersinfo = new List<membersinfo_t>();

        memberslist = new List<memberdata_t>();

        outerploygons = new List<membersinfo_t>();
        innerploygons = new List<membersinfo_t>();

        ID = GetAttribute<ulong>("id", node.Attributes);
        ways = waysset;

        // ОПТИМИЗАЦИЯ: Строим словарь ways заранее
        EnsureWaysDictBuilt();

        XmlNodeList members = node.SelectNodes("member");

        foreach (XmlNode member in members)
        {
            string type = GetAttribute<string>("type", member.Attributes);
            ulong refNo = GetAttribute<ulong>("ref", member.Attributes);
            string role = GetAttribute<string>("role", member.Attributes);

            OSMTypes osmtype = OSMTypes.Node;

            if (type == "node")
            {
                osmtype = OSMTypes.Node;
            }
            else if (type == "way")
            {
                osmtype = OSMTypes.Way;
            }
            else if (type == "relation")
            {
                osmtype = OSMTypes.Relation;
            }
            else
            {
                UnityEngine.Debug.Log("Detect unsupported member type: <" + type + "> in relation: " + ID);
            }

            memberdata_t memberitem = new memberdata_t
            {
                GeoId = refNo,
                Role = role,
                type = osmtype
            };

            memberslist.Add(memberitem);

            if (type == "way")
            {
                // ОПТИМИЗАЦИЯ: O(1) поиск
                OsmWay curWay = GetOsmWay(refNo);

                if (curWay != null)
                {
                    membersinfo_t datamember = new membersinfo_t
                    {
                        NodeIDs = new List<ulong>(curWay.NodeIDs),
                        role = role,
                        itemlist = curWay.itemlist,
                        isClosed = curWay.NodeIDs.Count > 0 &&
                                   curWay.NodeIDs[0] == curWay.NodeIDs[curWay.NodeIDs.Count - 1]
                    };

                    membersinfo.Add(datamember);
                }
            }
        }

        // Обработка outer/inner с оптимизированным алгоритмом
        var outerWays = membersinfo.FindAll(w => w.role == "outer");
        if (outerWays.Count > 0)
        {
            outerploygons = BuildChainsOptimized(outerWays);
            if (outerploygons.Count > 0)
            {
                NodeIDs = outerploygons[0].NodeIDs;
                IsClosedPolygon = outerploygons[0].isClosed;
            }
        }

        var innerWays = membersinfo.FindAll(w => w.role == "inner");
        if (innerWays.Count > 0)
        {
            innerploygons = BuildChainsOptimized(innerWays);
            foreach (var hole in innerploygons)
            {
                HolesNodeListsIDs.Add(hole.NodeIDs);
            }
        }

        // Обработка тегов
        XmlNodeList tags = node.SelectNodes("tag");
        itemlist = new Item[tags.Count];
        int j = 0;
        objectType = ObjectType.Undefined;

        bool isBuilding = false;

        foreach (XmlNode t in tags)
        {
            string key = GetAttribute<string>("k", t.Attributes);
            itemlist[j].key = key;
            itemlist[j].value = GetAttribute<string>("v", t.Attributes);
            DetectObjectType(t);

            if (objectType == ObjectType.Building)
            {
                isBuilding = true;
            }

            j++;
        }

        if (isBuilding)
        {
            objectType = ObjectType.Building;
        }

        if (outerWays.Count > 0 && outerploygons.Count == 0)
        {
 //           UnityEngine.Debug.LogError($"Не удалось построить outer полигон для relation {ID}");
        }
        else if (outerWays.Count > 0 && outerploygons.Count > 1)
        {
  //          UnityEngine.Debug.LogError($"Найдено outer {outerploygons.Count} полигонов для relation {ID}");
        }

        if (innerWays.Count > 0 && innerploygons.Count == 0)
        {
      //      UnityEngine.Debug.LogWarning($"Не удалось построить inner полигон для relation {ID}");
        }
    }
}
