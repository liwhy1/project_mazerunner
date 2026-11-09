using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class UITooltip : MonoBehaviour
{
    public static UITooltip Instance;
    [SerializeField] private GameObject tooltipObject;
    private GameObject activeTooltip;

    private void Start() => Instance = this;

    private void Update()
    {
        if (activeTooltip)
        {
            RectTransform tooltipRect = activeTooltip.GetComponent<RectTransform>();
            Canvas targetCanvas = GetComponentInParent<Canvas>();
            bool showAbove = Pointer.current.position.ReadValue().y < Screen.height / 2f;
            Vector2 targetPos = Pointer.current.position.ReadValue() + Vector2.down * (showAbove ? -75f : 75f);

            // calculate tooltip dimensions
            float rectWidth = tooltipRect.rect.width * targetCanvas.scaleFactor;
            float rectHeight = tooltipRect.rect.height * targetCanvas.scaleFactor;

            // calculate boundaries
            float minX = tooltipRect.pivot.x * rectWidth;
            float maxX = Screen.width - ((1f - tooltipRect.pivot.x) * rectWidth);
            float minY = tooltipRect.pivot.y * rectHeight;
            float maxY = Screen.height - ((1f - tooltipRect.pivot.y) * rectHeight);

            // clamp position in screen bounds
            float clampedX = Mathf.Clamp(targetPos.x, minX, maxX);
            float clampedY = Mathf.Clamp(targetPos.y, minY, maxY);

            // apply transform
            tooltipRect.position = new Vector3(clampedX, clampedY, tooltipRect.position.z);
        }
    }

    public void OnCreateTooltip(UIElement targetElement)
    {
        OnDestroyTooltip();
        activeTooltip = Instantiate(tooltipObject);
        activeTooltip.transform.SetParent(UIManager.Instance.transform);
        string targetText = UIManager.Instance.GetLocalTooltipString(targetElement.tooltipText);
        tooltipObject.GetComponentInChildren<TMP_Text>().text = targetText;
    }

    public void OnDestroyTooltip()
    {
        if (activeTooltip != null)
        {
            Destroy(activeTooltip);
            activeTooltip = null;
        }
    }
}
