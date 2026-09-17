using System.Collections;
using System.Text.RegularExpressions;
using MHUtils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("Inventory Data")]
    public bool isInventoryActive;
    [SerializeField] private GameObject inventoryObject;
    public GameObject buttonLayout;
    [SerializeField] private GameObject journalObject;
    [SerializeField] private GameObject mapObject;
    [SerializeField] private GameObject noteObject;

    [Header("Journal Data")]
    private bool isJournalGenerated;
    [SerializeField] private ScrollRect journalPageView;
    [SerializeField] private GameObject journalText;
    [SerializeField] private GameObject journalImage;
    [SerializeField] private GameObject journalPage1Layout;
    [SerializeField] private GameObject journalPage2Layout;

    private void Awake()
    {
        Instance = this;
    }

    public void OnSetup()
    {
        Debug.Log("InventoryManager: Setting up");
        isInventoryActive = false;

        // setup map
        mapObject.GetComponent<MapManager>().OnSetup();

        // fetch active story from host
        if (!GameManager.Instance.isOffline)
        {
            GameManager.Instance.FetchActiveStoryServerRpc();            
        }

        // reset inventory
        ResetInventoryState();
    }

    public void ResetInventoryState()
    {
        inventoryObject.SetActive(false);
        buttonLayout.SetActive(true);
        journalObject.SetActive(false);
        mapObject.SetActive(false);
        noteObject.transform.parent.gameObject.SetActive(true);
        noteObject.SetActive(false);
    }

    public void OnToggleInventory()
    {
        isInventoryActive = !isInventoryActive;
        inventoryObject.SetActive(isInventoryActive);
    }

    public void OnOpenJournal()
    {
        buttonLayout.gameObject.SetActive(false);
        if (!isJournalGenerated)
        {
            journalPage1Layout.SetActive(true);
            journalPage2Layout.SetActive(false);
            journalPageView.content = journalPage1Layout.GetComponent<RectTransform>();

            // generate pages
            GenerateJournalPage(journalPage1Layout, 1);
            GenerateJournalPage(journalPage2Layout, 2);

            isJournalGenerated = true;
        }
        journalObject.SetActive(true);
    }

    private void GenerateJournalPage(GameObject targetView, int targetPage)
    {
        string activeStory = !GameManager.Instance.isOffline ? GameManager.Instance.activeStory : "Prototype2";
        int persistentId = !GameManager.Instance.isOffline ? GameManager.Instance.FetchPersistentPlayerId() : 0;
        string textTargetPath = "Information/" + activeStory + "/info" + persistentId.ToString();
        string imageTargetPath = "Information/" + activeStory + "/image" + persistentId.ToString();
        string loadedText = Resources.Load<TextAsset>(textTargetPath + "_" + targetPage).text;
        string[] loadedTextBlocks = Regex.Split(loadedText, @"(\[image[12345]\])");

        foreach (string block in loadedTextBlocks)
        {
            // generate image blocks
            if (block.Contains("[image"))
            {
                GameObject newComponent = Instantiate(journalImage, targetView.transform);
                newComponent.SetActive(true);
                newComponent.GetComponent<Image>().sprite = Resources.Load<Sprite>(imageTargetPath+ "_" + block[block.Length - 2]);

                newComponent.GetComponent<Image>().preserveAspect = true;
                newComponent.GetComponent<Image>().SetNativeSize();
            }
            // generate text blocks
            else
            {
                GameObject newComponent = Instantiate(journalText, targetView.transform);
                newComponent.SetActive(true);
                TMP_Text textComponent = newComponent.GetComponent<TMP_Text>();
                textComponent.text = block;
                textComponent.ForceMeshUpdate();
            }
        }
    }

    public void OnJournalNextPage()
    {
        journalPage1Layout.SetActive(!journalPage1Layout.activeSelf);
        journalPage2Layout.SetActive(!journalPage2Layout.activeSelf);
        journalPageView.content = journalPage1Layout.activeSelf ? journalPage1Layout.GetComponent<RectTransform>() : journalPage2Layout.GetComponent<RectTransform>();
    }

    public void OnOpenMap() 
    {
        mapObject.SetActive(true);
        buttonLayout.gameObject.SetActive(false);
        MapManager.Instance.SetMapPage(MapManager.Instance.activeMapPage);
    }

    public void OnToggleNote() => noteObject.SetActive(!noteObject.activeSelf);

    public void OnInventoryBack()
    {
        GameManager.Instance.mapCamera.gameObject.SetActive(false);
        buttonLayout.SetActive(true);
        journalObject.SetActive(false);
        mapObject.SetActive(false);
    }

    public void OnJournalDisable() => buttonLayout.transform.Find("JournalIcon").GetComponent<UIElement>().OnElementDisable();
}
