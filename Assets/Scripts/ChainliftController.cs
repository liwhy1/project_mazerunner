using System.Collections;
using UnityEngine;

public class ChainliftController : MonoBehaviour
{
    public GameObject chainliftObject;
    private Coroutine liftMoveRoutine;
    public float lowLimit = 20.35f; // CHANGEME
    public float highLimit = -17.07f; // CHANGEME
    public float movementSpeed = 10f;

    public void OnMoveLift()
    {
        if (!chainliftObject) return;
        if (liftMoveRoutine == null) liftMoveRoutine = StartCoroutine(LiftMoveRoutine());
    }

    private IEnumerator LiftMoveRoutine()
    {
        Vector3 startPosition = chainliftObject.transform.localPosition;
        float targetPositionX = startPosition.x > lowLimit - highLimit ? highLimit : lowLimit;
        Vector3 targetPosition = new Vector3(targetPositionX, startPosition.y, startPosition.z);
        float time = 0f;
        while (time < movementSpeed)
        {
            time += Time.deltaTime;
            Vector3 newPosition = Vector3.Lerp(startPosition, targetPosition, time / movementSpeed);
            GameManager.Instance.SyncLiftPositionClientRpc(newPosition);
            yield return new WaitForEndOfFrame();
        }
        liftMoveRoutine = null;
    }
}
