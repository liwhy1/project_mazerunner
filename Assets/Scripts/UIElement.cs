using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIElement : MonoBehaviour
{
    public Color normalColor = Color.white;
    public Color highlightColor = Color.gray;
    public Color selectColor = Color.darkGray;
    public Color disabledColor = Color.white;
    public bool enableHighlight = true;
    public bool enableGrow = true;
    public bool isSelected = false;
    public bool isEnabled = true;
    public bool resetOnClick = true;

    public void OnSetup()
    {
        // setup vars
        normalColor = Color.white;
        highlightColor = Color.lightGray;
        selectColor = Color.darkGray;
        disabledColor = Color.white;
        disabledColor.a = .3f;
        if (GetComponent<EventTrigger>() == null) gameObject.AddComponent<EventTrigger>();

        // enable element
        OnElementEnable();

        // setup event triggers
        EventTrigger.Entry pointerClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        pointerClickEntry.callback.AddListener((eventData) => { OnElementClick(); });
        GetComponent<EventTrigger>().triggers.Add(pointerClickEntry);

        EventTrigger.Entry pointerEnterEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerEnter};
        pointerEnterEntry.callback.AddListener((eventData) => { OnElementGrow(); });
        GetComponent<EventTrigger>().triggers.Add(pointerEnterEntry);

        EventTrigger.Entry pointerExitEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerExit};
        pointerExitEntry.callback.AddListener((eventData) => { OnElementShrink(); });
        GetComponent<EventTrigger>().triggers.Add(pointerExitEntry);
        
    }

    public void OnElementSelect()
    {
        if (!isEnabled) return;
        isSelected = true;
        GetComponent<Image>().color = selectColor;
    }

    public void OnElementDeSelect()
    {
        if (!isEnabled) return;
        isSelected = false;
        GetComponent<Image>().color = normalColor;
    }

    public void OnElementEnable()
    {
        isEnabled = true;
        GetComponent<EventTrigger>().enabled = true;
        GetComponent<Image>().color = normalColor;
        if (transform.Find("Sprite")) transform.Find("Sprite").GetComponent<Image>().color = normalColor;
    }

    public void OnElementDisable()
    {
        isEnabled = false;
        isSelected = false;
        GetComponent<EventTrigger>().enabled = false;
        transform.localScale = new Vector3(1f, 1f, 1f);
        GetComponent<Image>().color = disabledColor;
        if (transform.Find("Sprite")) transform.Find("Sprite").GetComponent<Image>().color = disabledColor;
    }

    public void OnElementHighlight() 
    {
        if (!enableHighlight || !isEnabled || isSelected) return;

        GetComponent<Image>().color = highlightColor;
    }
    
    public void OnElementUnHighlight() 
    {
        if (isSelected || !isEnabled) return;

        GetComponent<Image>().color = normalColor;
    }

    public void OnElementGrow()
    {
        if (isSelected || !isEnabled) return;

        if (enableGrow) transform.localScale = new Vector3(1.05f, 1.05f, 1.05f);
        if (enableHighlight) OnElementHighlight();
    }

    public void OnElementShrink()
    {
        if (!isEnabled) return;
        transform.localScale = new Vector3(1f, 1f, 1f);
        OnElementUnHighlight();
    }

    public void OnElementClick()
    {
        if (!isEnabled) return;
        if (resetOnClick) OnElementShrink();
    }
}
