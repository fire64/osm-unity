using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

class GraveSrcipt : DetailBase
{
    public TMP_Text m_Text1;
    public TMP_Text m_Text2;

    public new void ActivateObject()
    {
        Detail detailinfo = transform.parent.GetComponent<Detail>();

        if (detailinfo)
        {
            CreateInfo(detailinfo);
        }
    }

    // Start is called before the first frame update
    private void CreateInfo(Detail detailinfo)
    {
        if (detailinfo.HasField("name"))
        {
            var name_grave = detailinfo.GetValueStringByKey("name");

            m_Text1.text = name_grave;
            m_Text2.text = name_grave;
        }
    }
}
