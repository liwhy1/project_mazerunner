using System.Collections;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

public class LandingManager : MonoBehaviour
{
    [SerializeField] private GameObject loadIcon;

    private async void Start()
    {
        loadIcon.SetActive(true);

        // wait for settings initialization
        await LocalizationSettings.InitializationOperation.Task;

        // preload localization tables
        await LocalizationSettings.StringDatabase.GetTableAsync("UI_Strings").Task;
        await LocalizationSettings.AssetDatabase.GetTableAsync("Dialog_Strings").Task;
        await LocalizationSettings.AssetDatabase.GetTableAsync("Landmark_Strings").Task;

        // load scene
        LoadScene(1);
    }

    public void LoadScene(int sceneIndex) => StartCoroutine(AsynchronousLevelLoad(sceneIndex));

    private IEnumerator AsynchronousLevelLoad(int sceneIndex)
    {
        Time.timeScale = 1f;
        yield return new WaitForSeconds(2f);
        loadIcon.SetActive(true);

        AsyncOperation ao = SceneManager.LoadSceneAsync(sceneIndex);
        ao.allowSceneActivation = false;
        Debug.Log("LandingManager: Loading scene " + SceneManager.GetSceneByBuildIndex(sceneIndex).name);
        while (!ao.isDone)
        {
            if (ao.progress == 0.9f)
            {
                ao.allowSceneActivation = true;
            }
            yield return null;
        }
    }
}
