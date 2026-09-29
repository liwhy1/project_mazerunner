using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [SerializeField] private AudioClip riverSound;

    private void Start()
    {
        Instance = this;
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
}