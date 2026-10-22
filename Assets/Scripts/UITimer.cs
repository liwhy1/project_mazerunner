using TMPro;
using UnityEngine;

public class UITimer : MonoBehaviour
{
    [SerializeField] private GameObject playToggleButton;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private float timeRemaining = 0f;
    private bool isPlaying;

    private void Start()
    {
        // setup default states
        isPlaying = false;
        timeRemaining = 900f; // 15mins

        OnResetTimer();
    }

    private void Update()
    {
        if (timeRemaining > 0f)
        {
            if (isPlaying)
            {
                timeRemaining -= Time.deltaTime;
                timeRemaining = Mathf.Max(timeRemaining, 0f);
            }
        }
        else
        {
            // stop timer if it reaches 0
            isPlaying = false;
        }

        int minutes = Mathf.FloorToInt(timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(timeRemaining % 60f);

        // update ui
        timerText.text = $"{minutes:00}:{seconds:00}";
        timerText.color = timeRemaining > 0 ? Color.black : Color.red;
        if (!playToggleButton) return;
        playToggleButton.transform.Find("Sprite").gameObject.SetActive(!isPlaying);
        playToggleButton.transform.Find("Sprite2").gameObject.SetActive(isPlaying);
    }

    public void OnTogglePause()
    {
        isPlaying = !isPlaying;
        UIManager.Instance?.OnSyncTimerClientRpc(timeRemaining, isPlaying);
    }

    public void OnResetTimer()
    {
        isPlaying = false;
        timeRemaining = 900f; // 15 mins
        if (GameManager.Instance.isLobbyStarted.Value) UIManager.Instance?.OnSyncTimerClientRpc(timeRemaining, isPlaying, false);
    }

    public void OnModifyTime(int targetModifier)
    {
        timeRemaining = Mathf.Max(timeRemaining += targetModifier, 0);
        UIManager.Instance?.OnSyncTimerClientRpc(timeRemaining, isPlaying);
    }

    public void OnSetTime(float targetTime) => timeRemaining = targetTime;
    public void OnSetPlaying(bool targetState) => isPlaying = targetState;
}
