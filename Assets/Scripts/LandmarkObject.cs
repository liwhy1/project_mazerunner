using System;
using System.Collections.Generic;
using UnityEngine;

public class LandmarkObject : MonoBehaviour
{
    public List<LandmarkData> travelRelations = new List<LandmarkData>();
    [Tooltip("The name this landmark will be displayed as on the UI.")]
    public string displayName = "Landmark";
}

[Serializable]
public class LandmarkData
{
    [Tooltip("The <i>LandmarkData</i> object the player arrives <b>from</b>.")]
    public GameObject connectedLandmark;
    [Tooltip("The amount it costs to travel <b>from</b> the assigned landmark <b>to</b> the current object.")]
    public int travelCost;
}
