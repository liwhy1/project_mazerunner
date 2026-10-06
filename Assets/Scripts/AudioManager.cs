using System.Linq;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;
    [SerializeField] private AudioMixer audioMixer;
    private AudioSource uiAudioSource;
    private bool IsGameStarted;

    [Header("Environment")]
    [SerializeField] private AudioSource environmentAudioSource;
    [SerializeField] private AudioSource riverAudioSource;
    [SerializeField] private GameObject riverObject;
    [SerializeField] private AudioClip riverSound;
    [SerializeField] private AudioClip footstepSound;

    [Header("UI")]
    [SerializeField] private AudioClip dialogOpenSound;
    [SerializeField] private AudioClip dialogCloseSound;
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip elementPlaceSound;
    [SerializeField] private AudioClip pencilDrawSound;

    private void Start()
    {
        Instance = this;
        uiAudioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        UpdateRiverSound();
    }

    public void OnGameStarted()
    {
        IsGameStarted = true;

        // setup environment sound
        environmentAudioSource = FindObjectsByType<AudioSource>().FirstOrDefault(o => o.name.Contains("Environment Source"))?.GetComponent<AudioSource>();
        environmentAudioSource?.Play();

        // setup river sound
        riverObject = GameManager.Instance.terrainObject.transform.parent.Find("River").gameObject;
        riverAudioSource = riverObject.GetComponentInChildren<AudioSource>();
        riverAudioSource?.PlayOneShot(riverSound);
    }

    private void UpdateRiverSound()
    {
        if (!IsGameStarted || !riverObject) return;
        MeshCollider riverCollider = riverObject.transform.Find("ConvexRiver")?.GetComponent<MeshCollider>();
        if (!riverCollider) return;

        Vector3 closestPoint = riverCollider.ClosestPoint(PlayerController.Instance.transform.position);
        float currentDistance = Vector3.Distance(PlayerController.Instance.transform.position, closestPoint);

        riverAudioSource.volume = Mathf.InverseLerp(6f, 0f, currentDistance);
    }

    private void PlaySoundClip(AudioClip targetClip, bool randomPitch = true)
    {
        uiAudioSource.pitch = randomPitch ? Random.Range(.5f, 1f) : 1f;
        uiAudioSource.PlayOneShot(targetClip);
    }

    public bool IsPlaying() { return uiAudioSource.isPlaying; }
    public void SetMixerGroupVolume(string targetGroup, float targetVolume) => audioMixer.SetFloat(targetGroup, targetVolume);

    public void OnDialogOpen() => PlaySoundClip(dialogOpenSound);
    public void OnDialogClose() => PlaySoundClip(dialogCloseSound);
    public void OnButtonClick() => PlaySoundClip(buttonClickSound, false);
    public void OnElementPlace() => PlaySoundClip(elementPlaceSound);
    public void OnFootstep() => PlaySoundClip(footstepSound);
    public void OnDraw() => PlaySoundClip(pencilDrawSound, true);
}