using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Unity.VisualScripting;

public class OsmRelation : BaseOsm
{
    List<OsmWay> ways;
    OsmWay GetOsmWay(ulong WayId)
    {
        foreach (var geo in ways)
        {
            if (geo.ID == WayId)
            {
                return geo;
            }
        }

        return null;
    }

    // Замените текущую логику обработки outer/inner на этот метод
    List<membersinfo_t> BuildChains(List<membersinfo_t> segments)
    {
        List<membersinfo_t> chains = new List<membersinfo_t>();

        while (segments.Count > 0)
        {
            membersinfo_t currentChain = new membersinfo_t();
            currentChain.NodeIDs = new List<ulong>(segments[0].NodeIDs);
            currentChain.role = segments[0].role;
            currentChain.itemlist = segments[0].itemlist;
            segments.RemoveAt(0);

            bool chainModified = true;
            while (chainModified)
            {
                chainModified = false;
                ulong firstNode = currentChain.NodeIDs[0];
                ulong lastNode = currentChain.NodeIDs[currentChain.NodeIDs.Count - 1];

                for (int i = 0; i < segments.Count; i++)
                {
                    var segment = segments[i];
                    if (segment.NodeIDs[0] == lastNode)
                    {
                        // Продолжить цепочку вперед
                        currentChain.NodeIDs.RemoveAt(currentChain.NodeIDs.Count - 1); // Убрать дублирующий узел
                        currentChain.NodeIDs.AddRange(segment.NodeIDs);
                        segments.RemoveAt(i);
                        chainModified = true;
                        break;
                    }
                    else if (segment.NodeIDs[segment.NodeIDs.Count - 1] == firstNode)
                    {
                        // Продолжить цепочку назад
                        segment.NodeIDs.RemoveAt(segment.NodeIDs.Count - 1); // Убрать дублирующий узел
                        currentChain.NodeIDs.InsertRange(0, segment.NodeIDs);
                        segments.RemoveAt(i);
                        chainModified = true;
                        break;
                    }
                    else if (segment.NodeIDs[0] == firstNode)
                    {
                        // Добавить в начало в обратном порядке
                        segment.NodeIDs.RemoveAt(0); // Убрать дублирующий узел
                        currentChain.NodeIDs.InsertRange(0, segment.NodeIDs.Reverse<ulong>());
                        segments.RemoveAt(i);
                        chainModified = true;
                        break;
                    }
                    else if (segment.NodeIDs[segment.NodeIDs.Count - 1] == lastNode)
                    {
                        // Добавить в конец в обратном порядке
                        segment.NodeIDs.RemoveAt(segment.NodeIDs.Count - 1); // Убрать дублирующий узел
                        currentChain.NodeIDs.AddRange(segment.NodeIDs.Reverse<ulong>());
                        segments.RemoveAt(i);
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
        if (polygon.NodeIDs.Count < 3) return false; // Минимум 3 узла для замкнутого полигона
        if (polygon.isClosed && polygon.NodeIDs[0] != polygon.NodeIDs[polygon.NodeIDs.Count - 1])
        {
            // Автоматически закрыть полигон если нужно
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

        outerploygons = new List<membersinfo_t>();
        innerploygons = new List<membersinfo_t>();

        ID = GetAttribute<ulong>("id", node.Attributes);
        Visible = GetAttribute<bool>("visible", node.Attributes);

        ways = waysset;

        XmlNodeList members = node.SelectNodes("member");

        foreach (XmlNode member in members)
        {
            string type = GetAttribute<string>("type", member.Attributes);
            ulong refNo = GetAttribute<ulong>("ref", member.Attributes);
            string role = GetAttribute<string>("role", member.Attributes);

            if (type == "way") //current only way, nodes and relations add leter
            {
                OsmWay curWay = GetOsmWay(refNo);

                if (curWay != null)
                {
                    membersinfo_t datamember = new membersinfo_t();
                    datamember.NodeIDs = new List<ulong>();

                    datamember.role = role;
                    datamember.NodeIDs.AddRange(curWay.NodeIDs);
                    datamember.itemlist = curWay.itemlist;

                    if( curWay.NodeIDs[0] == curWay.NodeIDs[curWay.NodeIDs.Count - 1] )
                    {
                        datamember.isClosed = true;
                    }
                    else
                    {
                        datamember.isClosed = false;
                    }

                    membersinfo.Add(datamember);
                }
            }
        }

        // В конструкторе OsmRelation замените текущую обработку outer/inner на:
        var outerWays = membersinfo.FindAll(w => w.role == "outer");
        if (outerWays.Count > 0)
        {
            outerploygons = BuildChains(outerWays);
            if (outerploygons.Count > 0)
            {
                NodeIDs = outerploygons[0].NodeIDs;
                IsClosedPolygon = outerploygons[0].isClosed;
            }
        }

        var innerWays = membersinfo.FindAll(w => w.role == "inner");
        if (innerWays.Count > 0)
        {
            innerploygons = BuildChains(innerWays);
            foreach (var hole in innerploygons)
            {
                HolesNodeListsIDs.Add(hole.NodeIDs);
            }
        }

        var otherWays = membersinfo.FindAll(w => w.role != "inner" && w.role != "outer");

        if (otherWays.Count > 0)
        {
            foreach (var member in otherWays)
            {
         //       UnityEngine.Debug.Log("Realtion: " + ID + " Role: " + member.role);
            }
        }

        // Обработка тегов
        XmlNodeList tags = node.SelectNodes("tag");
        itemlist = new Item[tags.Count];
        int j = 0;
        objectType = ObjectType.Undefined;

        foreach (XmlNode t in tags)
        {
            string key = GetAttribute<string>("k", t.Attributes);
            itemlist[j].key = key;
            itemlist[j].value = GetAttribute<string>("v", t.Attributes);
            DetectObjectType(t);
            j++;
        }

        // В конец конструктора добавьте:
        if (outerWays.Count > 0 && outerploygons.Count == 0)
        {
            UnityEngine.Debug.LogError($"Не удалось построить outer полигон для relation {ID}");
        }
        else if(outerWays.Count > 0 && outerploygons.Count > 1)
        {
            UnityEngine.Debug.LogError($"Найдено outer {outerploygons.Count} полигонов для relation {ID}");
        }

        if (innerWays.Count > 0 && innerploygons.Count == 0)
        {
            UnityEngine.Debug.LogWarning($"Не удалось построить inner полигон для relation {ID}");
        }
    }

}
