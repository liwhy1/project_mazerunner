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
    [SerializeField] private GameObject journalContent1;
    [SerializeField] private GameObject journalContent2;

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
        if (!journalContent1.GetComponent<VerticalLayoutGroup>() && !GameManager.Instance.isOffline)
        {
            journalContent1.transform.GetChild(0).GetComponent<TMP_Text>().text = Resources.Load<TextAsset>("Information/" + GameManager.Instance.activeStory + "/info" + GameManager.Instance.FetchPersistentPlayerId().ToString() + "_1").text;
            journalContent2.transform.GetChild(0).GetComponent<TMP_Text>().text = Resources.Load<TextAsset>("Information/" + GameManager.Instance.activeStory + "/info" + GameManager.Instance.FetchPersistentPlayerId().ToString() + "_2").text;

            journalContent1.AddComponent<VerticalLayoutGroup>();
            journalContent2.AddComponent<VerticalLayoutGroup>();
        }
        journalObject.SetActive(true);
    }

    public void OnJournalNextPage()
    {
        GameObject page1View = journalObject.transform.Find("ContentLayout").transform.Find("Page1View").gameObject;
        GameObject page2View = journalObject.transform.Find("ContentLayout").transform.Find("Page2View").gameObject;
        page1View.SetActive(!page1View.activeSelf);
        page2View.SetActive(!page2View.activeSelf);
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
