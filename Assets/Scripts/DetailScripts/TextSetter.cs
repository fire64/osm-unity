using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TextSetter : MonoBehaviour
{
    public string KeyName = "name";

    public TMP_Text m_Text1;
    public TMP_Text m_Text2;
    public TMP_Text m_Text3;
    public TMP_Text m_Text4;

    public void ActivateObject()
    {
        Detail detailinfo = transform.parent.GetComponent<Detail>();

        if (detailinfo)
        {
            CreateTextInfo(detailinfo);
        }
    }

    private void CreateTextInfo(Detail detailinfo)
    {
        if (detailinfo.HasField(KeyName))
        {
            var value = detailinfo.GetValueStringByKey(KeyName);

            if(m_Text1 != null)
            {
                m_Text1.text = value;
            }

            if (m_Text2 != null)
            {
                m_Text2.text = value;
            }

            if (m_Text3 != null)
            {
                m_Text3.text = value;
            }

            if (m_Text4 != null)
            {
                m_Text4.text = value;
            }
        }
    }

}
