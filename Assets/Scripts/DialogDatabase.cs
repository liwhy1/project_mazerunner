using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public enum DialogId
{
    InvalidPlayerName,
    InvalidJoinCode,
    FailedGameJoin,
    HostLostConnection,
    SceneLoadFail,
    SharedMapClear,
    MapClear,
    PlayerTutorial,
    TreasureReached,
    EditorOpen,
    MapTutorial,
    MapTutorial1,
    MapTutorial2,
    MapTutorial3,
    MapTutorial4,
    MapTutorial5
}

[Serializable]
public class DialogEntry
{
    public DialogId id;
    public string title;
    public string text;
    public string button;
    public string action;
    public bool forceAction = false;
    public string imageId;
}

public class DialogDatabase : MonoBehaviour
{
    public static DialogDatabase Instance;
    private List<DialogEntry> dialogs;

    private void Awake()
    {
        LocalizationSettings.SelectedLocaleChanged += Setup;
        Setup(null);
    }

    private void Setup(Locale newLocale)
    {
        Instance = this;
        dialogs = new List<DialogEntry>()
        { 
            new DialogEntry() { id = DialogId.InvalidPlayerName,    title = UIManager.Instance.GetLocalDialogString("DIALOG_TITLE_NOTICE"),     text = UIManager.Instance.GetLocalDialogString("DIALOG_EMPTY_NAME"),                                                                                                                                          button = "",                                                                    action = "sessionconnectfail",             forceAction = true},
            new DialogEntry() { id = DialogId.InvalidJoinCode,      title = UIManager.Instance.GetLocalDialogString("DIALOG_TITLE_NOTICE"),     text = UIManager.Instance.GetLocalDialogString("DIALOG_EMPTY_CODE"),                                                                                                                                          button = "",                                                                    action = "sessionconnectfail",             forceAction = true},
            new DialogEntry() { id = DialogId.FailedGameJoin,       title = UIManager.Instance.GetLocalDialogString("DIALOG_TITLE_NOTICE"),     text = UIManager.Instance.GetLocalDialogString("DIALOG_JOIN_FAIL"),                                                                                                                                           button = "",                                                                    action = "sessionconnectfail",             forceAction = true},
            new DialogEntry() { id = DialogId.HostLostConnection,   title = UIManager.Instance.GetLocalDialogString("DIALOG_TITLE_NOTICE"),     text = UIManager.Instance.GetLocalDialogString("DIALOG_CONNECTION_LOST"),                                                                                                                                     button = UIManager.Instance.GetLocalDialogString("DIALOG_OPTION_YES"),          action = "selfdisconnect",                 forceAction = false},
            new DialogEntry() { id = DialogId.SceneLoadFail,        title = UIManager.Instance.GetLocalDialogString("DIALOG_TITLE_NOTICE"),     text = UIManager.Instance.GetLocalDialogString("DIALOG_LOAD_FAIL"),                                                                                                                                           button = UIManager.Instance.GetLocalDialogString("DIALOG_OPTION_YES"),          action = "selfdisconnect",                 forceAction = false},
            new DialogEntry() { id = DialogId.MapClear,             title = UIManager.Instance.GetLocalDialogString("DIALOG_TITLE_NOTICE"),     text = UIManager.Instance.GetLocalDialogString("DIALOG_MAP_CLEAR"),                                                                                                                                           button = UIManager.Instance.GetLocalDialogString("DIALOG_OPTION_YES"),          action = "mapclear",                       forceAction = false},
            new DialogEntry() { id = DialogId.SharedMapClear,       title = UIManager.Instance.GetLocalDialogString("DIALOG_TITLE_NOTICE"),     text = UIManager.Instance.GetLocalDialogString("DIALOG_MAP_CLEAR"),                                                                                                                                           button = UIManager.Instance.GetLocalDialogString("DIALOG_OPTION_YES"),          action = "sharedmapclear",                 forceAction = false},
            new DialogEntry() { id = DialogId.PlayerTutorial,       title = UIManager.Instance.GetLocalDialogString("DIALOG_TITLE_TUTORIAL"),   text = UIManager.Instance.GetLocalDialogString("DIALOG_TUTORIAL_MOVEMENT"),                                                                                                                                   button = "",                                                                    action = "",                               forceAction = false,                imageId = "TutorialPlayer"},
            new DialogEntry() { id = DialogId.TreasureReached,      title = UIManager.Instance.GetLocalDialogString("DIALOG_TITLE_END"),        text = UIManager.Instance.GetLocalDialogString("DIALOG_TREASURE_REACH") + " " + GameManager.Instance.currentTravelCost.Value + " " + UIManager.Instance.GetLocalUIString("MISC_HOURS_SUBSTRING"),             button = "",                                                                    action = "",                               forceAction = false},
            new DialogEntry() { id = DialogId.EditorOpen,           title = UIManager.Instance.GetLocalDialogString("DIALOG_TITLE_NOTICE"),     text = UIManager.Instance.GetLocalDialogString("DIALOG_OPEN_EDITOR"),                                                                                                                                         button = UIManager.Instance.GetLocalDialogString("DIALOG_OPTION_YES"),          action = "openeditor",                     forceAction = false},
            new DialogEntry() { id = DialogId.MapTutorial,          title = UIManager.Instance.GetLocalDialogString("DIALOG_TITLE_TUTORIAL"),   text = UIManager.Instance.GetLocalDialogString("DIALOG_TUTORIAL_MAP"),                                                                                                                                        button = UIManager.Instance.GetLocalDialogString("DIALOG_OPTION_YES"),          action = "maptutorial1",                   forceAction = false},
            new DialogEntry() { id = DialogId.MapTutorial1,         title = UIManager.Instance.GetLocalDialogString("DIALOG_TITLE_TUTORIAL"),   text = UIManager.Instance.GetLocalDialogString("DIALOG_TUTORIAL_MAP_1"),                                                                                                                                      button = UIManager.Instance.GetLocalDialogString("DIALOG_OPTION_CONTINUE"),     action = "maptutorial2",                   forceAction = false,                imageId = "BookIcon"},
            new DialogEntry() { id = DialogId.MapTutorial2,         title = UIManager.Instance.GetLocalDialogString("DIALOG_TITLE_TUTORIAL"),   text = UIManager.Instance.GetLocalDialogString("DIALOG_TUTORIAL_MAP_2"),                                                                                                                                      button = UIManager.Instance.GetLocalDialogString("DIALOG_OPTION_CONTINUE"),     action = "maptutorial3",                   forceAction = false,                imageId = "PencilSprite"},
            new DialogEntry() { id = DialogId.MapTutorial3,         title = UIManager.Instance.GetLocalDialogString("DIALOG_TITLE_TUTORIAL"),   text = UIManager.Instance.GetLocalDialogString("DIALOG_TUTORIAL_MAP_3"),                                                                                                                                      button = UIManager.Instance.GetLocalDialogString("DIALOG_OPTION_CONTINUE"),     action = "maptutorial4",                   forceAction = false,                imageId = "MapIconSprite"},
            new DialogEntry() { id = DialogId.MapTutorial4,         title = UIManager.Instance.GetLocalDialogString("DIALOG_TITLE_TUTORIAL"),   text = UIManager.Instance.GetLocalDialogString("DIALOG_TUTORIAL_MAP_4"),                                                                                                                                      button = UIManager.Instance.GetLocalDialogString("DIALOG_OPTION_CONTINUE"),     action = "maptutorial5",                   forceAction = false,                imageId = "SelectSprite"},
            new DialogEntry() { id = DialogId.MapTutorial5,         title = UIManager.Instance.GetLocalDialogString("DIALOG_TITLE_TUTORIAL"),   text = UIManager.Instance.GetLocalDialogString("DIALOG_TUTORIAL_MAP_5"),                                                                                                                                      button = "",                                                                    action = "",                               forceAction = false,                imageId = "SaveSprite"},
        };
    }

    public DialogEntry GetEntry(DialogId id)
    {
        var targetEntry = dialogs.FirstOrDefault(d => d.id == id);
        if (!string.IsNullOrEmpty(targetEntry.text)) return targetEntry;

        Debug.Log("No dialog text found for id: " + id);
        return null;
    }
}