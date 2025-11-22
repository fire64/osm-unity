using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

class ChimneyResizer : DetailBase
{
    public GameObject body;
    public GameObject smokeprefab;
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
        if (detailinfo.HasField("height"))
        {
            var height = detailinfo.GetValueFloatByKey("height");

            body.transform.localScale = new Vector3 (body.transform.localScale.x, height, body.transform.localScale.z);

            body.transform.localPosition = new Vector3(body.transform.localPosition.x, height - 0.1f, body.transform.localPosition.z);
        }

        GameObject smoke = Instantiate(smokeprefab, transform.position, Quaternion.identity, body.transform);

        smoke.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        smoke.transform.localScale = new Vector3(2f, 2f, 0.3f);
        smoke.transform.localRotation = Quaternion.identity;
        smoke.transform.Rotate(Vector3.right, -90f);


    }
}
