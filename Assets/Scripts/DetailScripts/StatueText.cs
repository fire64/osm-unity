using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

class StatueText : DetailBase
{
    public TMP_Text m_Text;

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
        if (detailinfo.HasField("inscription"))
        {
            var inscription = detailinfo.GetValueStringByKey("inscription");

            m_Text.text = inscription;
        }
        else if (detailinfo.HasField("name"))
        {
            var name = detailinfo.GetValueStringByKey("name");

            m_Text.text = name;
        }
        else
        {
            m_Text.text = "";
        }
    }
}
