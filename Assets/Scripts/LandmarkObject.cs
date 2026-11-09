using System;
using System.Collections.Generic;
using UnityEngine;

public enum LandmarkType
{
    START,
    BRIDGE,
    CHAINLIFT,
    FOREST,
    MISTYMARSH,
    MONSTERNEST,
    MOUNTAINS,
    RUINS,
    TREASURE,
    VILLAGE,
    WAYSTONE
}

public class LandmarkObject : MonoBehaviour
{
    public List<LandmarkData> travelRelations = new List<LandmarkData>();
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
