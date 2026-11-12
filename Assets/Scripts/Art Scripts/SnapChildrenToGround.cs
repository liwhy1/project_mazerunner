using UnityEngine;

public class SnapChildrenToGround : MonoBehaviour
{
    [Header("Ground Settings")]
    public Transform groundTarget;
    public LayerMask groundLayerMask = ~0; // Set to Everything by default

    [Header("Placement Options")]
    public float offset = 0.0f;
    public bool alignToSlope = true;
    public float raycastStartHeight = -5f;
    public float raycastDistance = 200f;

    [ContextMenu("Snap Children To Ground")]
    public void SnapAllChildren()
    {
        int childCount = transform.childCount;

        if (childCount == 0)
        {
            Debug.LogWarning("[SnapToGround] No direct children found under this Empty object!", gameObject);
            return;
        }

        int snappedCount = 0;

        foreach (Transform child in transform)
        {
            Vector3 rayOrigin = child.position + (Vector3.up * raycastStartHeight);

            // Draw red debug ray in Scene View (visible for 5 seconds)
            Debug.DrawRay(rayOrigin, Vector3.down * raycastDistance, Color.red, 5.0f);

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDistance, groundLayerMask))
            {
                if (groundTarget != null && hit.transform != groundTarget && !hit.transform.IsChildOf(groundTarget))
                {
                    Debug.LogWarning($"[SnapToGround] Hit '{hit.transform.name}', but it isn't assigned as the target ground.", child);
                    continue;
                }

#if UNITY_EDITOR
                UnityEditor.Undo.RecordObject(child, "Snap to Ground");
#endif

                child.position = hit.point + (Vector3.up * offset);

                if (alignToSlope)
                {
                    child.rotation = Quaternion.FromToRotation(child.up, hit.normal) * child.rotation;
                }

                snappedCount++;
            }
            else
            {
                Debug.LogWarning($"[SnapToGround] Raycast completely missed ground under '{child.name}'. Is there a Collider on the ground?", child);
            }
        }

        Debug.Log($"[SnapToGround] Finished: Snapped {snappedCount} of {childCount} child object(s).");
    }
}