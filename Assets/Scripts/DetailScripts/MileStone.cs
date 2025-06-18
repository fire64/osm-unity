using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using TMPro;

class MileStone : DetailBase
{
    public TMP_Text m_Text1;
    public TMP_Text m_Text2;
    public TMP_Text m_Text3;
    public TMP_Text m_Text4;
    public new void ActivateObject()
    {
        Detail detailinfo = transform.parent.GetComponent<Detail>();

        if (detailinfo)
        {
            CreateMileStone(detailinfo);
        }
    }

    private void CreateMileStone(Detail detailinfo)
    {
        if (detailinfo.HasField("distance"))
        {
            var distance = detailinfo.GetValueStringByKey("distance");

            m_Text1.text = distance;
            m_Text2.text = distance;
            m_Text3.text = distance;
            m_Text4.text = distance;
        }
    }
}
