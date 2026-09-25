using TMPro;
using UnityEngine;

public class TimerManager : MonoBehaviour
{
    [SerializeField] private GameObject playToggleButton;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private float timeRemaining = 0f;
    private bool isPlaying;

    private void Awake()
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
        playToggleButton.transform.Find("Sprite").gameObject.SetActive(!isPlaying);
        playToggleButton.transform.Find("Sprite2").gameObject.SetActive(isPlaying);
    }

    public void OnTogglePause()
    {
        isPlaying = !isPlaying;
    }

    public void OnResetTimer()
    {
        isPlaying = false;
        timeRemaining = 900f; // 15 mins
    }

    public void OnModifyTime(int targetModifier)
    {
        timeRemaining = Mathf.Max(timeRemaining += targetModifier, 0);
    }
}
