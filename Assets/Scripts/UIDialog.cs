using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIDialog : MonoBehaviour
{
    public static UIDialog Instance;

    [Header("Dialog Data")]
    public GameObject activeDialog;
    [SerializeField] private List<Sprite> dialogSprites;

    private void Awake() => Instance = this;

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
        newDialog.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -1000);
        activeDialog = newDialog;

        // setup vars
        GameObject dialogTitle = activeDialog.transform.Find("DialogTitle").gameObject;
        GameObject dialogText = activeDialog.transform.Find("DialogText").gameObject;
        GameObject dialogSprite = activeDialog.transform.Find("DialogSprite").gameObject;
        GameObject buttonLayout = activeDialog.transform.Find("ButtonLayout").gameObject;
        GameObject closeButton = buttonLayout.transform.Find("CloseButton").gameObject;
        GameObject mainButton = buttonLayout.transform.Find("MainButton").gameObject;

        // setup content
        dialogTitle.GetComponent<TMP_Text>().text = dialogEntry.title;
        dialogText.GetComponent<TMP_Text>().text = dialogEntry.text;
        dialogSprite.SetActive(!string.IsNullOrEmpty(dialogEntry.imageId));
        if (dialogSprite.activeSelf) dialogSprite.GetComponent<Image>().sprite = dialogSprites.FirstOrDefault(s => s.name.Contains(dialogEntry.imageId));
        mainButton.SetActive(!string.IsNullOrEmpty(dialogEntry.button));
        mainButton.transform.Find("Text").GetComponent<TMP_Text>().text = dialogEntry.button;
        closeButton.transform.Find("Text").GetComponent<TMP_Text>().text = UIManager.Instance.GetLocalDialogString("DIALOG_OPTION_CLOSE");

        // setup triggers
        SetupTriggers(closeButton, mainButton, dialogEntry.action, dialogEntry.forceAction);

        // animate dialog in
        StartCoroutine(UIManager.Instance.UIAnimator(newDialog.GetComponent<RectTransform>(), Vector2.zero));
    }

    private void SetupTriggers(GameObject closeButton, GameObject mainButton, string targetAction, bool forceAction)
    {
        // main button
        UIManager.Instance.AddEventTrigger(mainButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, DialogActionHandler, targetAction);
        UIManager.Instance.AddEventTrigger(mainButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, DialogActionHandler, targetAction);

        // close button
        UIManager.Instance.AddEventTrigger(closeButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnCloseDialog);
        UIManager.Instance.AddEventTrigger(closeButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnCloseDialog);

        // conditionally assign forced action to close button
        if (forceAction)
        {
            UIManager.Instance.AddEventTrigger(closeButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, DialogActionHandler, targetAction);
            UIManager.Instance.AddEventTrigger(closeButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, DialogActionHandler, targetAction);
        }
    }

    private void DialogActionHandler(string targetAction)
    {
        switch (targetAction)
        {
            case "mapclear":
                MapManager.Instance.OnClearMap();
                break;
            case "sharedmapclear":
                MapManager.SharedInstance.OnClearMap();
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
            case "maptutorial":
                InventoryManager.Instance.SetMapTutorialState(1);
                break;
            case "sessionconnectfail":
                MenuManager.Instance.OnSessionConnectFailed();
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

}