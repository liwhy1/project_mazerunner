using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIHorizontalSelector : MonoBehaviour
{
    [SerializeField] private GameObject leftArrowButton;
    [SerializeField] private GameObject rightArrowButton;
    [SerializeField] private TMP_Text selectorText;
    public List<string> selectorItems = new List<string>();
    public string currentSelection;
    public event Action onValueChanged;

    private void Start()
    {
        OnUpdateSelection(0);
    }

    public void OnUpdateSelection(int targetIndex)
    {
        currentSelection = selectorItems[targetIndex];
        selectorText.text = currentSelection;
        onValueChanged?.Invoke();
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }

    public void OnNavigateSelection(int modifierNumber)
    {
        int currentIndex = selectorItems.IndexOf(currentSelection);
        int negativeModifier = modifierNumber < 0 ? selectorItems.Count : 0;
        int newIndex = (currentIndex + modifierNumber + negativeModifier) % selectorItems.Count;
        OnUpdateSelection(newIndex);
    }
}
