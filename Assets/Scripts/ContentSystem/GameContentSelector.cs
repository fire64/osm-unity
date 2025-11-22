using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameContentSelector : MonoBehaviour
{
    public double[] DisabledObjects; //Информационный объект 
    public double[] DisabledRoofs; //Информационный объект

    public ColorByName colorByName;

    public bool isClearUnusedData = false;

    [Header("Road marking materials")]
    public Material solidLineMaterial;
    public Material dashedLineMaterial;
    public Material doubleSolidMaterial;
    public Material doubleDashedMaterial;
    public Material zigZagMaterial;
    public Material dottedLineMaterial;

    void Start()
    {
        if (isClearUnusedData)
        {
            colorByName.DeleteUnused();
        }
    }

    public bool isGeoObjectDisabled(double objid)
    {
        int countobj = DisabledObjects.Length;

        for (int i = 0; i < countobj; i++)
        {
            if (DisabledObjects[i] == objid)
            {
                return true;
            }
        }

        return false;
    }

    public bool isRoofDisabled(double buildingid)
    {
        int countobj = DisabledRoofs.Length;

        for (int i = 0; i < countobj; i++)
        {
            if (DisabledRoofs[i] == buildingid)
            {
                return true;
            }
        }

        return false;
    }
}