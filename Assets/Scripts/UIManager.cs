using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.EventSystems;
using System.Linq;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Collections;

public class UIManager : NetworkBehaviour
{
    public static UIManager Instance;
    public EventSystem eventSystem;

    [Header("Pause Data")]
    [SerializeField] private GameObject pauseObject;
    [SerializeField] public GameObject resumeButton;
    [SerializeField] private GameObject settingsButton;
    [SerializeField] private GameObject pauseQuitButton;
    public TMP_Text pauseJoinCodeText;
    public GameObject pausePlayerListObject;

    [Header("Settings Data")]
    [SerializeField] private GameObject settingsObject;
    [SerializeField] private Slider uiVolumeSlider;
    [SerializeField] private Slider environmentVolumeSlider;
    [SerializeField] private GameObject settingsBackButton;
    [SerializeField] private Toggle fpsToggle;
    [SerializeField] private TMP_Text fpsText;
    [SerializeField] private float fpsUpdateFrequency = 0.5f;
    private Coroutine fpsRoutine;
    private int fpsValue;

    [Header("HUD Data")]
    public Image pauseIcon;
    public Image inventoryIcon;
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
        AddEventTrigger(settingsButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnToggleSettings);
        AddEventTrigger(settingsButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnToggleSettings);

        // setup setting trigger
        AddEventTrigger(settingsBackButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnToggleSettings);
        AddEventTrigger(settingsBackButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnToggleSettings);
        fpsToggle.onValueChanged.AddListener(delegate { OnToggleFpsCounter(); });
        uiVolumeSlider.onValueChanged.AddListener(delegate { AudioManager.Instance.SetMixerGroupVolume("UI", uiVolumeSlider.value); });
        environmentVolumeSlider.onValueChanged.AddListener(delegate { AudioManager.Instance.SetMixerGroupVolume("Environment", environmentVolumeSlider.value); });

        // setup zoom triggers
        AddEventTrigger(cameraZoomInIcon.GetComponent<EventTrigger>(), EventTriggerType.PointerDown, InputManager.Instance.OnScrollStart, -1f);
        AddEventTrigger(cameraZoomInIcon.GetComponent<EventTrigger>(), EventTriggerType.PointerUp, InputManager.Instance.OnScrollStop);
        AddEventTrigger(cameraZoomOutIcon.GetComponent<EventTrigger>(), EventTriggerType.PointerDown, InputManager.Instance.OnScrollStart, 1f);
        AddEventTrigger(cameraZoomOutIcon.GetComponent<EventTrigger>(), EventTriggerType.PointerUp, InputManager.Instance.OnScrollStop);

        // setup menumanager
        MenuManager.Instance.OnSetup();
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
            if (currentSelectedObject == null && GameManager.Instance.isLobbyStarted.Value) 
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

    public void OnToggleSettings()
    {
        settingsObject.SetActive(!settingsObject.activeSelf);
        eventSystem.SetSelectedGameObject(uiVolumeSlider.gameObject);
    }

    private void SetupMinimapIcon(int playerId)
    {
        Transform targetParent = GameManager.Instance.isMaster ? MenuManager.Instance.lobbySharedMapView.transform : minimapIcon.transform.parent;
        GameObject newIcon = Instantiate(Resources.Load<GameObject>("MapPrefabs/PlayerIcon"), Vector3.zero, Quaternion.identity, targetParent);
        if (GameManager.Instance.isMaster) newIcon.transform.localScale = new Vector3(.5f, .5f, .5f);
        newIcon.name = "playerIcon_" + playerId;
        newIcon.SetActive(true);
        minimapPlayerIcons.Add(newIcon);
    }

    private void UpdateMinimapPosition()
    {
        // setup vars
        if (!GameManager.Instance.terrainObject)
        {
            if (GameManager.Instance.isMaster)
            {
                GameObject newTerrain = Instantiate(Resources.Load<GameObject>("TerrainObjects/MinimapTerrain"));
                GameManager.Instance.terrainObject = newTerrain.transform.Find("Terrain").Find("Inner terrain").gameObject;
            }
            else return;
        }
        MeshRenderer renderer = GameManager.Instance.terrainObject.GetComponent<MeshRenderer>();
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
            if (!player) continue;
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
            else if (player.IsLobbyReady.Value) SetupMinimapIcon(playerId);
        }
    }

    public void MirrorSharedmaptoMinimap()
    {
        // TODO: this is hardcoded, has hacks and is unnecessarily complex (but it works)
        minimapIcon.transform.localScale = new Vector3(.7f, .7f, .7f);
        foreach (Transform icon in MapManager.SharedInstance.transform)
        {
            // prevent mirroring drawdots
            if (!icon.GetComponent<NetworkObject>() || icon.name.Contains("Dot")) continue;
            GameObject newIcon = Instantiate(Resources.Load<GameObject>("MapPrefabs/MapIcon"), minimapIcon.transform.parent);

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
            newIcon.transform.Find("Sprite").GetComponent<Image>().sprite = Resources.LoadAll<Sprite>("MapIcons").FirstOrDefault(s => s.name.Contains(icon.name));
            newIcon.transform.Find("Sprite").GetComponent<Image>().preserveAspect = true;
            newIcon.transform.Find("Name").gameObject.SetActive(false);
        }
        // resize minimap sprite aka anchor hack pt2
        minimapIcon.transform.localScale = Vector3.one;
    }

    public void OnQuitButton()
    {
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
        settingsObject.SetActive(false);
        loadingIcon.SetActive(false);
        cameraZoomInIcon.gameObject.SetActive(GameManager.Instance.isLobbyStarted.Value);
        cameraZoomOutIcon.gameObject.SetActive(GameManager.Instance.isLobbyStarted.Value);
        minimapIcon.transform.parent.gameObject.SetActive(GameManager.Instance.isLobbyStarted.Value);
        inventoryIcon.gameObject.SetActive(GameManager.Instance.isLobbyStarted.Value);
        pauseIcon.gameObject.SetActive(GameManager.Instance.isLobbyStarted.Value);
    }

    public void OnPauseToggle()
    {
        pauseObject.SetActive(settingsObject.activeSelf ? false : !pauseObject.activeSelf);
        settingsObject.SetActive(false);
        inventoryIcon.gameObject.SetActive(!pauseObject.activeSelf && !InventoryManager.Instance.isInventoryActive);
        pauseIcon.gameObject.SetActive(!pauseObject.activeSelf && !InventoryManager.Instance.isInventoryActive || InventoryManager.Instance.isInventoryActive && InventoryManager.Instance.isLocked);
    }

    public void OnRefreshPlayerList()
    {
        // cleanup old entries
        foreach (Transform child in MenuManager.Instance.lobbyPlayerListObject.transform)
        {
            if (child.name == "Title") continue;
            Destroy(child.gameObject);
        }

        foreach (Transform child in pausePlayerListObject.transform)
        {
            if (child.name == "Title") continue;
            Destroy(child.gameObject);
        }

        // conditionally toggle player list visibility
        pausePlayerListObject.SetActive(GameManager.Instance.playerList.Count != 0);
        MenuManager.Instance.lobbyPlayerListObject.SetActive(GameManager.Instance.playerList.Count != 0);

        foreach (var player in GameManager.Instance.playerList)
        {
            string targetName = player.PlayerName.Value.ToString();
            targetName += player.OwnerClientId == NetworkManager.LocalClientId ? " (you)" : "";

            GameObject newEntry = Instantiate(Resources.Load<GameObject>("UIElements/PlayerListEntry"), MenuManager.Instance.lobbyPlayerListObject.transform);
            newEntry.transform.Find("Name").GetComponent<TMP_Text>().text = targetName;
            newEntry.transform.Find("KickButton").gameObject.SetActive(IsHost && player.OwnerClientId != NetworkManager.LocalClientId);
            AddEventTrigger(newEntry.transform.Find("KickButton").GetComponent<EventTrigger>(), EventTriggerType.PointerClick, GameManager.Instance.PlayerKickNotifyClientRpc, player.OwnerClientId);
            GameObject newPauseEntry = Instantiate(newEntry, pausePlayerListObject.transform);
            AddEventTrigger(newPauseEntry.transform.Find("KickButton").GetComponent<EventTrigger>(), EventTriggerType.PointerClick, GameManager.Instance.PlayerKickNotifyClientRpc, player.OwnerClientId);
        }
    }

    public void OnOpenDialog(DialogId id)
    {
        var dialogEntry = DialogDatabase.Instance.GetEntry(id);
        if (dialogEntry == null) return;

        // close any active dialogs
        OnCloseDialog();

        // play open sound
        AudioManager.Instance.OnDialogOpen();

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
        dialogTitle.GetComponent<TMP_Text>().text = dialogEntry.title;
        dialogText.GetComponent<TMP_Text>().text = dialogEntry.text;
        mainButton.SetActive(!string.IsNullOrEmpty(dialogEntry.button));
        mainButton.transform.GetChild(0).GetComponent<TMP_Text>().text = dialogEntry.button;

        // set ui selected button
        //eventSystem.SetSelectedGameObject(mainButton.activeSelf ? mainButton : closeButton);

        // setup triggers
        AddEventTrigger(closeButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnCloseDialog);
        AddEventTrigger(closeButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnCloseDialog);

        AddEventTrigger(mainButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, DialogActionHandler, dialogEntry.action);
        AddEventTrigger(mainButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, DialogActionHandler, dialogEntry.action);
    }

    private void DialogActionHandler(string targetAction)
    {
        switch (targetAction)
        {
            case "mapclear":
                MapManager.Instance.OnClearMap();
                break;
            case "openeditor":
                MenuManager.Instance.ResetUIState();
                GameManager.Instance.isPaused = false;
                InventoryManager.Instance.OnToggleInventory();
                InventoryManager.Instance.OnOpenEditor();
                break;
            case "selfdisconnect":
                GameManager.Instance.OnDisconnectClient();
                break;
        }
        OnCloseDialog();
    }

    public void OnCloseDialog()
    {
        if (activeDialog)
        {
            AudioManager.Instance.OnDialogClose();
            Destroy(activeDialog);
            activeDialog = null;
        }
    }

    private void OnToggleFpsCounter()
    {
        if (fpsRoutine != null) 
        {
            StopCoroutine(fpsRoutine);
            fpsRoutine = null;
        }
        else fpsRoutine = StartCoroutine(FpsRoutine());

        fpsText.gameObject.SetActive(fpsRoutine != null);
    }

    private IEnumerator FpsRoutine()
    {
        int lastFrameCount;
        float lastTime;
        float timeSpan;
        int frameCount;

        while (true)
        {
            lastFrameCount = Time.frameCount;
            lastTime = Time.realtimeSinceStartup;
            yield return new WaitForSeconds(fpsUpdateFrequency);
            timeSpan = Time.realtimeSinceStartup - lastTime;
            frameCount = Time.frameCount - lastFrameCount;

            fpsValue = Mathf.RoundToInt(frameCount / timeSpan);
            fpsText.text = fpsValue + " FPS";
        }
    }
}
