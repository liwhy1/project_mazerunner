using UnityEngine;
using UnityEngine.Video;

public class VideoPlayerManager : MonoBehaviour
{
    [SerializeField] private GameObject playToggleButton;
    [SerializeField] private VideoPlayer videoPlayer;

    private void Awake()
    {
        OnResetVideo();
    }

    private void Update()
    {
        // update ui
        playToggleButton.transform.Find("Sprite").gameObject.SetActive(!videoPlayer.isPlaying);
        playToggleButton.transform.Find("Sprite2").gameObject.SetActive(videoPlayer.isPlaying);
    }

    public void OnTogglePause()
    {
        if (videoPlayer.isPlaying)
        {
            videoPlayer.Pause();
        }
        else
        {
            videoPlayer.Play();
        }
    }

    public void OnResetVideo()
    {
        videoPlayer.frame = 0;
        videoPlayer.Pause();
    }
}
