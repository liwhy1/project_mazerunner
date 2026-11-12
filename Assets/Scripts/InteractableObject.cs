using UnityEngine;

public enum InteractableType { Chest, Bell, Lever, Log, Chainlift, River, Mist, PurpleMist}
public class InteractableObject : MonoBehaviour
{
    public InteractableType interactableType;
    public void OnInteract()
    {
        switch (interactableType)
        {
            case InteractableType.Lever:
                GetComponent<ChainliftController>().OnMoveLift();
                break;
            case InteractableType.Chest:
                GameManager.Instance.TreasureAnimationRpc();
                break;
            case InteractableType.Bell:
                AudioManager.Instance.OnBellHit();
                break;
            case InteractableType.PurpleMist:
                GameManager.Instance.RespawnPlayer();
                break;
        }
    }
}