using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;
    private AudioSource audioSource;

    [Header("Environment")]
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
        audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        UpdateRiverSound();
    }

    private void UpdateRiverSound()
    {
        if (!PlayerController.Instance) return;
        GameObject riverObject = GameManager.Instance.terrainObject.transform.parent.Find("River").gameObject;
        AudioSource riverSource = riverObject.GetComponent<AudioSource>();
        if (!riverSource.isPlaying) riverSource.PlayOneShot(riverSound);
        MeshCollider riverCollider = riverObject.transform.Find("ConvexRiver").GetComponent<MeshCollider>();

        Vector3 closestPoint = riverCollider.ClosestPoint(PlayerController.Instance.transform.position);
        float currentDistance = Vector3.Distance(PlayerController.Instance.transform.position, closestPoint);

        riverSource.volume = Mathf.InverseLerp(6f, 0f, currentDistance);
    }

    private void PlaySoundClip(AudioClip targetClip, bool randomPitch = true)
    {
        audioSource.pitch = randomPitch ? Random.Range(.5f, 1f) : 1f;
        audioSource.PlayOneShot(targetClip);
    }

    public bool IsPlaying() { return audioSource.isPlaying; }

    public void OnDialogOpen() => PlaySoundClip(dialogOpenSound);
    public void OnDialogClose() => PlaySoundClip(dialogCloseSound);
    public void OnButtonClick() => PlaySoundClip(buttonClickSound);
    public void OnElementPlace() => PlaySoundClip(elementPlaceSound);
    public void OnFootstep() => PlaySoundClip(footstepSound);
    public void OnDraw() => PlaySoundClip(pencilDrawSound, true);
}