using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIProgressBar : MonoBehaviour
{
    [SerializeField] private Slider progressSlider;
    [SerializeField] private GameObject levelHolder;
    private List<GameObject> levelObjects = new List<GameObject>();
    [SerializeField] private Color normalColor = new Color(0.7254902f, 0.5607843f, 0.3882353f);
    [SerializeField] private Color activeColor = new Color(0.8000001f, 0.654902f, 0.5137255f);

    private void Start()
    {
        // setup slider
        progressSlider.value = progressSlider.minValue;

        // store level objects
        foreach (Transform child in levelHolder.transform)
        {
            levelObjects.Add(child.gameObject);
        }

        // reset progress
        OnProgressUpdate((int)progressSlider.value);
    }

    public void OnProgressUpdate(int targetLevel)
    {
        // set slider value
        progressSlider.value = targetLevel;

        if (levelObjects.Count == 0) return;

        // reset colors
        levelObjects.ForEach(l => l.GetComponent<Image>().color = normalColor);

        // set active color to levels up to targetLevel
        var targetLevels = levelObjects.FindAll(l => levelObjects.IndexOf(l) <= targetLevel - 1);
        targetLevels.ForEach(l => l.GetComponent<Image>().color = activeColor);

        // TODO: this is hardcoded af
        // update individual map ready count
        string readyText = targetLevel == 1 ? GameManager.Instance.playerList.Count(p => p.IsMapReady.Value == true) + "/" + GameManager.Instance.playerList.Count : "";
        levelObjects[0].GetComponentInChildren<TMP_Text>().text = "Individual Mapping " + readyText;
    }
}
