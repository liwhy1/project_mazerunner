using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.EventSystems;
using System.Linq;
using UnityEngine.Events;
using System.Collections.Generic;

public class UIManager : NetworkBehaviour
{
    public static UIManager Instance;
    public EventSystem eventSystem;

    [Header("Pause Data")]
    [SerializeField] private GameObject pauseObject;
    [SerializeField] public GameObject resumeButton;
    [SerializeField] private GameObject pauseQuitButton;
    public TMP_Text pauseJoinCodeText;
    public TMP_Text pausePlayerListText;

    [Header("HUD Data")]
    [SerializeField] private Image pauseIcon;
    [SerializeField] private Image inventoryIcon;
    public Image cameraZoomInIcon;
    public Image cameraZoomOutIcon;
    public GameObject loadingIcon;
    private GameObject lastSelectedObject;

    [Header("Minimap Data")]
    public GameObject minimapObject;
    [SerializeField] private Image minimapIcon;
    [SerializeField] private List<GameObject> minimapPlayerIcons;

    [Header("Dialog Data")]
    public GameObject activeDialog;

    private void Start()
    {
        Debug.Log("UIManager: Setting up");

        // setup vars
        Instance = this;
        eventSystem = FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);

        // call setup on uielemenets
        foreach (var element in FindObjectsByType<UIElement>(FindObjectsInactive.Include))
        {
            element.OnSetup();
        }

        // reset ui
        ResetUIState();

        // setup pause triggers
        AddEventTrigger(resumeButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, InputManager.Instance.OnPauseAction);
        AddEventTrigger(resumeButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, InputManager.Instance.OnPauseAction);
        AddEventTrigger(pauseQuitButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnQuitButton);
        AddEventTrigger(pauseQuitButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnQuitButton);

        // setup menumanager
        GetComponent<MenuManager>().OnSetup();
    }

    private void Update()
    {
        if (GameManager.Instance.isLobbyStarted.Value)
        {
            UpdateMinimapPosition();
        }

        HandleActiveNavigationElement();
    }

    public void AddEventTrigger(EventTrigger trigger, EventTriggerType type, UnityAction action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    public void AddEventTrigger<T>(EventTrigger trigger, EventTriggerType type, UnityAction<T> action, T value)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action(value));
        trigger.triggers.Add(entry);
    }

    public void HandleActiveNavigationElement()
    {
        GameObject currentSelectedObject = eventSystem.currentSelectedGameObject;
        if (currentSelectedObject != lastSelectedObject)
        {
            // prevent deselecting on click in the menu
            if (currentSelectedObject == null && GameManager.Instance.networkState == NetworkState.None) 
            {
                currentSelectedObject = lastSelectedObject;
                eventSystem.SetSelectedGameObject(currentSelectedObject);
            }

            // unhighlight previous element
            if (lastSelectedObject && lastSelectedObject.GetComponent<UIElement>()) lastSelectedObject.GetComponent<UIElement>().OnElementShrink();

            // highlight selected element
            if (currentSelectedObject && currentSelectedObject.GetComponent<UIElement>()) currentSelectedObject.GetComponent<UIElement>().OnElementGrow();

            // activate input filed on current selection, if exits
            if (currentSelectedObject && currentSelectedObject.GetComponent<TMP_InputField>()) currentSelectedObject.GetComponent<TMP_InputField>().ActivateInputField();

            lastSelectedObject = currentSelectedObject;
        }
    }

    public void OnNavigationDown()
    {
        if (!eventSystem.currentSelectedGameObject) return;
        Selectable currentSelectable = eventSystem.currentSelectedGameObject.GetComponent<Selectable>();
        Selectable nextSelectable = currentSelectable.navigation.selectOnDown;
        if (nextSelectable != null)
        {
            EventSystem.current.SetSelectedGameObject(nextSelectable.gameObject);
            if (nextSelectable.GetComponent<TMP_InputField>()) nextSelectable.GetComponent<TMP_InputField>().ActivateInputField();
        }
    }

    private void SetupMinimapIcon(int playerId)
    {
        Transform targetParent = GameManager.Instance.isMaster ? MenuManager.Instance.sharedMapView.transform : minimapIcon.transform.parent;
        GameObject newIcon = Instantiate(Resources.Load<GameObject>("PlayerIcon"), Vector3.zero, Quaternion.identity, targetParent);
        if (GameManager.Instance.isMaster) newIcon.transform.localScale = new Vector3(.5f, .5f, .5f);
        newIcon.name = "playerIcon_" + playerId;
        newIcon.SetActive(true);
        minimapPlayerIcons.Add(newIcon);
    }

    private void UpdateMinimapPosition()
    {
        // setup vars
        GameObject terrainObject = GameManager.Instance.terrainObject;
        MeshRenderer renderer = terrainObject.GetComponent<MeshRenderer>();
        Bounds bounds = renderer.bounds;
        RectTransform minimapRect = minimapIcon.GetComponent<RectTransform>();

        // check for dead icons
        var deadIcons = minimapPlayerIcons.FindAll(i => GameManager.Instance.playerList.All(p => p.PersistentPlayerId.Value.ToString() != i.name.Split('_')[1]));
        foreach (var icon in deadIcons)
        {
            minimapPlayerIcons.Remove(icon);
            Destroy(icon.gameObject);
        }

        foreach (var player in GameManager.Instance.playerList)
        {
            int playerId = player.PersistentPlayerId.Value;
            GameObject playerIcon = minimapPlayerIcons.FirstOrDefault(i => i.name == "playerIcon_" + playerId);
            if (playerIcon)
            {
                Vector3 playerPosition = player.gameObject.transform.position;
                float normalizedX = Mathf.InverseLerp(bounds.min.x, bounds.max.x, playerPosition.x);
                float normalizedY = Mathf.InverseLerp(bounds.min.z, bounds.max.z, playerPosition.z);
                RectTransform playerIconRect = playerIcon.GetComponent<RectTransform>();
                Color newColor = Color.HSVToRGB(.1f + playerId * .1f, 1f, 1f);
                playerIconRect.gameObject.GetComponent<Image>().color = newColor;
                playerIconRect.gameObject.SetActive(true);
                playerIconRect.anchoredPosition = new Vector2(Mathf.Lerp(minimapRect.rect.xMin, minimapRect.rect.xMax, normalizedX), Mathf.Lerp(minimapRect.rect.yMin, minimapRect.rect.yMax, normalizedY));                
            }
            else if (player.IsGameStarted.Value) SetupMinimapIcon(playerId);
        }
    }

    public void MirrorSharedmaptoMinimap()
    {
        // TODO: this is hardcoded, has hacks and is unnecessarily complex (but it works)
        minimapIcon.transform.localScale = new Vector3(.7f, .7f, .7f);
        foreach (Transform icon in SharedMapManager.Instance.transform)
        {
            // prevent mirroring drawdots
            if (!icon.GetComponent<NetworkObject>() || icon.name.Contains("Dot")) continue;
            GameObject newIcon = Instantiate(Resources.Load<GameObject>("MapIcon"), minimapIcon.transform.parent);

            // calculate world canvas pos to screen canvas
            Vector2 screenPosition = GameManager.Instance.mapCamera.WorldToScreenPoint(icon.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(transform.GetComponent<RectTransform>(), screenPosition, null, out Vector2 localPosition);

            // apply position with slightly made up offset
            newIcon.GetComponent<RectTransform>().anchoredPosition = localPosition;
            newIcon.GetComponent<RectTransform>().anchoredPosition -= new Vector2(300f, 0f);

            // anchor hack pt1
            newIcon.transform.SetParent(minimapIcon.transform);

            // apply icon values
            newIcon.transform.localEulerAngles = Vector3.zero;
            newIcon.transform.Find("Sprite").GetComponent<Image>().sprite = Resources.LoadAll<Sprite>("MapIconsNew").FirstOrDefault(s => s.name.Contains(icon.name));
            newIcon.transform.Find("Sprite").GetComponent<Image>().preserveAspect = true;
            newIcon.transform.Find("Name").gameObject.SetActive(false);
        }
        // resize minimap sprite aka anchor hack pt2
        minimapIcon.transform.localScale = Vector3.one;
    }

    public void OnQuitButton()
    {
        // reset playerprefs
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        // destroy spawned player
        if (PlayerController.Instance != null)
        {
            Destroy(PlayerController.Instance.gameObject);
        }

        // reset networking
        GameManager.Instance.OnDisconnectClient();
    }

    public void ResetUIState()
    {
        pauseObject.SetActive(false);
        loadingIcon.SetActive(false);
        inventoryIcon.gameObject.SetActive(true);
        cameraZoomInIcon.gameObject.SetActive(true);
        cameraZoomOutIcon.gameObject.SetActive(true);
        minimapIcon.transform.parent.gameObject.SetActive(true);
        pauseIcon.gameObject.SetActive(true);
    }

    public void OnPauseToggle()
    {
        eventSystem.SetSelectedGameObject(resumeButton);
        pauseObject.SetActive(!pauseObject.activeSelf);
        pauseIcon.gameObject.SetActive(!pauseObject.activeSelf);
        inventoryIcon.gameObject.SetActive(!pauseObject.activeSelf);
    }

    public void OnRefreshPlayerList()
    {
        MenuManager.Instance.lobbyPlayerListText.text = "Players:\n";
        pausePlayerListText.text = "Players:\n";
        foreach (var player in GameManager.Instance.playerList)
        {
            string targetText = player.PlayerName.Value.ToString();
            targetText += player.OwnerClientId == NetworkManager.LocalClientId ? " (you)" : "";
            targetText += player.OwnerClientId == NetworkManager.ServerClientId ? " (host)" : "";
            MenuManager.Instance.lobbyPlayerListText.text += targetText + "\n";
            pausePlayerListText.text += targetText + "\n";
        }
    }

    public void OnOpenDialog(string titleText, string contentText, string buttonText = "", string targetAction = "")
    {
        // close any active dialogs
        OnCloseDialog();

        // create new dialog
        GameObject newDialog = Instantiate(Resources.Load<GameObject>("UIElements/TextDialog"), transform);
        newDialog.transform.localPosition = Vector3.zero;
        activeDialog = newDialog;

        // setup vars
        GameObject dialogTitle = activeDialog.transform.Find("DialogTitle").gameObject;
        GameObject dialogText = activeDialog.transform.Find("DialogText").gameObject;
        GameObject buttonLayout = activeDialog.transform.Find("ButtonLayout").gameObject;
        GameObject closeButton = buttonLayout.transform.Find("CloseButton").gameObject;
        GameObject mainButton = buttonLayout.transform.Find("MainButton").gameObject;

        // setup text
        dialogTitle.GetComponent<TMP_Text>().text = titleText;
        dialogText.GetComponent<TMP_Text>().text = contentText;
        mainButton.SetActive(!string.IsNullOrEmpty(buttonText));
        mainButton.transform.GetChild(0).GetComponent<TMP_Text>().text = buttonText;

        // set ui selected button
        eventSystem.SetSelectedGameObject(mainButton.activeSelf ? mainButton : closeButton);

        // setup triggers
        AddEventTrigger(closeButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnCloseDialog);
        AddEventTrigger(closeButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnCloseDialog);

        AddEventTrigger(mainButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, DialogActionHandler, targetAction);
        AddEventTrigger(mainButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, DialogActionHandler, targetAction);
    }

    private void DialogActionHandler(string targetAction)
    {
        switch (targetAction)
        {
            case "mapclear":
                MapManager.Instance.OnClearMap();
                break;
            case "sharedmapclear":
                SharedMapManager.Instance.OnClearMap();
                break;
        }
        OnCloseDialog();
    }

    public void OnCloseDialog()
    {
        if (activeDialog)
        {
            Destroy(activeDialog);
            activeDialog = null;
        }
    }
}
