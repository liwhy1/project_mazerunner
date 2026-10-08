using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum DialogId
{
    InvalidPlayerName,
    InvalidJoinCode,
    FailedGameJoin,
    HostLostConnection,
    MapClear,
    PlayerTutorial,
    EditorOpen
}

[Serializable]
public class DialogEntry
{
    public DialogId id;
    public string title;
    public string text;
    public string button;
    public string action;
}

public class DialogDatabase : MonoBehaviour
{
    public static DialogDatabase Instance;
    private List<DialogEntry> dialogs;

    private void Awake()
    {
        Instance = this;
        dialogs = new List<DialogEntry>()
        { 
            new DialogEntry() { id = DialogId.InvalidPlayerName,    title = "Notice",   text = "Player name can't be empty!",                                                                                               button = "",        action = ""},
            new DialogEntry() { id = DialogId.InvalidJoinCode,      title = "Notice",   text = "Join code can't be empty!",                                                                                                 button = "",        action = "" },
            new DialogEntry() { id = DialogId.FailedGameJoin,       title = "Notice",   text = "Failed to join game!",                                                                                                      button = "",        action = "" },
            new DialogEntry() { id = DialogId.HostLostConnection,   title = "Notice",   text = "Connection to the game host has been lost. Would you like to return to the main menu?",                                     button = "Yes",     action = "selfdisconnect" },
            new DialogEntry() { id = DialogId.MapClear,             title = "Notice",   text = "Are you sure you want to clear the map?",                                                                                   button = "Yes",     action = "mapclear" },
            new DialogEntry() { id = DialogId.PlayerTutorial,       title = "Tutorial", text = "<b>Player movement:</b>\n(WASD) / (Point & Click)\n<b>Camera height control:</b>\n(Mouse Wheel) / (UI Plus & Minus icon)",  button = "",        action = "" },
            new DialogEntry() { id = DialogId.EditorOpen,           title = "Notice",   text = "Would you like to open the map editor?",                                                                                    button = "Yes",     action = "openeditor" }
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