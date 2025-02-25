using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class FlagResizer : DetailBase
{
    // Start is called before the first frame update
    public GameObject flagObject;

    public new void ActivateObject()
    {
        Detail detailinfo = transform.parent.GetComponent<Detail>();

        if (detailinfo)
        {
            float min_height = 0.0f;

            if (detailinfo.HasField("min_height"))
            {
                min_height = detailinfo.GetValueFloatByKey("min_height");
            }

            float height = 2.0f;

            if (detailinfo.HasField("height"))
            {
                height = detailinfo.GetValueFloatByKey("height");
            }

            float correctresize = (height - min_height) / 2.0f;

            flagObject.transform.localScale = new Vector3(correctresize, correctresize, correctresize);
            flagObject.transform.localPosition = flagObject.transform.localPosition + new Vector3(0, min_height, 0);
        }
    }
}
