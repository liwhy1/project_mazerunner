using System.Collections;
using UnityEngine;

public class ChainliftController : MonoBehaviour
{
    public GameObject chainliftObject;
    private Coroutine liftMoveRoutine;
    private float lowLimit = 20.35f; // CHANGEME
    private float highLimit = -17.07f; // CHANGEME
    private float movementSpeed = 10f;

    public void OnMoveLift()
    {
        // ignore requests while the lift is already moving
        if (!chainliftObject || GameManager.Instance.isChainliftMoving.Value || liftMoveRoutine != null) return;
        liftMoveRoutine = StartCoroutine(LiftMoveRoutine());
    }

    private IEnumerator LiftMoveRoutine()
    {
        GameManager.Instance.SetChainliftStateServerRpc(true);
        Vector3 startPosition = chainliftObject.transform.localPosition;

        // pick the opposite end of the track
        float targetPositionX = startPosition.x > lowLimit - highLimit ? highLimit : lowLimit;
        Vector3 targetPosition = new Vector3(targetPositionX, startPosition.y, startPosition.z);
        float time = 0f;
        while (time < movementSpeed)
        {
            time += Time.deltaTime;
            Vector3 newPosition = Vector3.Lerp(startPosition, targetPosition, time / movementSpeed);

            // stream the position so every client sees the same lift
            GameManager.Instance.SyncLiftPositionRpc(newPosition);
            yield return new WaitForEndOfFrame();
        }
        liftMoveRoutine = null;
        GameManager.Instance.SetChainliftStateServerRpc(false);
    }
}
