using System;
using System.Collections.Generic;
using UnityEngine;

public enum LandmarkType
{
    Start,
    Brug, // bridge
    Kettinglift, // chainlift
    Bos, // forest
    Mistige_Moeras, // misty marsh
    Monsternest, //monster nest
    Bergen, // mountains
    Ruïnes, // ruins
    Schat, // treasure
    Dorp, // village
    Grenssteen // waystone
}

public class LandmarkObject : MonoBehaviour
{
    public List<LandmarkData> travelRelations = new List<LandmarkData>();
    [Tooltip("The name this landmark will be displayed as on the UI.")]
    public LandmarkType landmarkType;
    public bool isStartArea;
}

[Serializable]
public class LandmarkData
{
    [Tooltip("The <i>LandmarkData</i> object the player arrives <b>from</b>.")]
    public GameObject connectedLandmark;
    [Tooltip("The amount it costs to travel <b>from</b> the assigned landmark <b>to</b> the current object.")]
    public int travelCost;
}
