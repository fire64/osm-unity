using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewLocation", menuName = "Custom/Location Data")]

public class LocationData : ScriptableObject
{
    [Header("Base params")]
    public string locationName;
    public double latitude;
    public double longitude;
    public float radiusmeters;
    public Texture2D image;
}
