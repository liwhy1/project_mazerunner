using UnityEngine;

public enum InteractableType { Chest, Bell, Lever, Log, Chainlift, River, Mist, PurpleMist}
public class InteractableObject : MonoBehaviour
{
    public InteractableType interactableType;
    public void OnInteract()
    {
        // run the interaction based on the object type
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