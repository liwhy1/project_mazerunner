using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIElement : MonoBehaviour
{
    public Color normalColor = Color.white;
    public Color highlightColor = Color.gray;
    public Color selectColor = Color.darkGray;
    public Color disabledColor = Color.white;
    Vector3 startSize;
    public bool isElementSetup = false;
    public bool enableBackground = true;
    public bool enableHighlight = true;
    public bool enableGrow = true;
    public bool isSelected = false;
    public bool isEnabled = true;
    public bool resetOnClick = true;

    public void Start() => OnSetup();

    public void OnSetup()
    {
        if (isElementSetup) return;

        // setup vars
        normalColor = Color.white;
        highlightColor = Color.lightGray;
        selectColor = Color.darkGray;
        disabledColor = Color.white;
        disabledColor.a = .3f;

        if (!enableBackground) 
        {
            normalColor.a = 0f;
            highlightColor.a = 0f;
            selectColor.a = 0f;
            disabledColor.a = 0f;
        }

        startSize = transform.localScale;
        if (startSize == Vector3.zero)
        {
            startSize = Vector3.one;
            transform.localScale = Vector3.one;
        }

        // setup event triggers
        if (GetComponent<EventTrigger>() == null) gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry pointerClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        pointerClickEntry.callback.AddListener((eventData) => { OnElementClick(); });
        GetComponent<EventTrigger>().triggers.Add(pointerClickEntry);

        EventTrigger.Entry pointerSubmitEntry = new EventTrigger.Entry() {eventID = EventTriggerType.Submit};
        pointerSubmitEntry.callback.AddListener((eventData) => { OnElementClick(); });
        GetComponent<EventTrigger>().triggers.Add(pointerSubmitEntry);

        EventTrigger.Entry pointerEnterEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerEnter};
        pointerEnterEntry.callback.AddListener((eventData) => { OnElementGrow(); });
        GetComponent<EventTrigger>().triggers.Add(pointerEnterEntry);

        EventTrigger.Entry pointerExitEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerExit};
        pointerExitEntry.callback.AddListener((eventData) => { OnElementShrink(); });
        GetComponent<EventTrigger>().triggers.Add(pointerExitEntry);

        isElementSetup = true;
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
        GetComponents<EventTrigger>().All(p => p.enabled = true);
        GetComponent<Image>().color = isSelected ? selectColor : normalColor;
        if (transform.Find("Sprite"))
        {
            Color tempColor = transform.Find("Sprite").GetComponent<Image>().color;
            tempColor.a = 1f;
            transform.Find("Sprite").GetComponent<Image>().color = tempColor;
        }
    }

    public void OnElementDisable()
    {
        isEnabled = false;
        isSelected = false;
        GetComponents<EventTrigger>().All(p => p.enabled = false);
        transform.localScale = startSize;
        GetComponent<Image>().color = disabledColor;
        if (transform.Find("Sprite"))
        {
            Color tempColor = transform.Find("Sprite").GetComponent<Image>().color;
            tempColor.a = .3f;
            transform.Find("Sprite").GetComponent<Image>().color = tempColor;
        }
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

        if (enableGrow) 
        {
            transform.localScale = startSize * 1.05f;
        }
        if (enableHighlight) OnElementHighlight();
    }

    public void OnElementShrink()
    {
        if (!isEnabled) return;
        transform.localScale = startSize;
        OnElementUnHighlight();
    }

    public void OnElementClick()
    {
        if (!isEnabled) return;
        AudioManager.Instance.OnButtonClick();
        if (resetOnClick) OnElementShrink();
    }
}
