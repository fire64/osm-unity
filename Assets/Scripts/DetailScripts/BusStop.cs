using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using TMPro;

public class BusStop : MonoBehaviour
{
    // Start is called before the first frame update
    public TMP_Text m_Text1;
    public TMP_Text m_Text2;

    public new void ActivateObject()
    {
        Detail detailinfo = transform.parent.GetComponent<Detail>();

        if (detailinfo)
        {
            CreateBusInfo(detailinfo);
        }
    }

    private void CreateBusInfo(Detail detailinfo)
    {
        if (detailinfo.HasField("name"))
        {
            var name = detailinfo.GetValueStringByKey("name");

            m_Text1.text = name;
            m_Text2.text = name;
        }
    }
}
