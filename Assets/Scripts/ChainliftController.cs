using System.Collections;
using UnityEngine;

public class ChainliftController : MonoBehaviour
{
    [SerializeField] private GameObject chainliftObject;
    private Coroutine liftMoveRoutine;
    public float lowLimit = 20.35f;
    public float highLimit = -17.07f;
    public float movementSpeed = 10f;

    public void OnMoveLift()
    {
        if (!chainliftObject) return;
        if (liftMoveRoutine == null) liftMoveRoutine = StartCoroutine(LiftMoveRoutine());
    }

    private IEnumerator LiftMoveRoutine()
    {
        Vector3 startPosition = chainliftObject.transform.localPosition;
        float targetPositionX = startPosition.x > 0 ? highLimit : lowLimit;
        Vector3 targetPosition = new Vector3(targetPositionX, startPosition.y, startPosition.z);
        float time = 0f;
        while (time < movementSpeed)
        {
            time += Time.deltaTime;
            chainliftObject.transform.localPosition = Vector3.Lerp(startPosition, targetPosition, time / movementSpeed);
            yield return new WaitForEndOfFrame();
        }
        liftMoveRoutine = null;
    }
}
