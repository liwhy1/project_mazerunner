using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LandingManager : MonoBehaviour
{
    [SerializeField] private GameObject LoadIcon;
    void Start()
    {
        LoadIcon.SetActive(false);
        LoadScene(1);
    }

    public void LoadScene(int sceneIndex) => StartCoroutine(AsynchronousLevelLoad(sceneIndex));

    private IEnumerator AsynchronousLevelLoad(int sceneIndex)
    {
        Time.timeScale = 1f;
        yield return new WaitForSeconds(2f);
        LoadIcon.SetActive(true);

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
