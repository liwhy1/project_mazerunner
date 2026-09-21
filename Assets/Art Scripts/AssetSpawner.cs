using UnityEngine;

public class AssetSpawner : MonoBehaviour
{
    [Header("Target & Prefabs")]
    [Tooltip("The mesh surface to spawn objects on.")]
    public MeshFilter targetMeshFilter;

    [Tooltip("Array of prefabs to pick from randomly.")]
    public GameObject[] prefabsToSpawn;

    [Header("Spawn Configuration")]
    public int spawnCount = 50;

    [Tooltip("Align the spawned object's Up direction with the mesh face normal.")]
    public bool alignWithNormals = true;

    [Tooltip("Offset applied along the face normal direction.")]
    public Vector3 spawnOffset = Vector3.zero;

    [ContextMenu("Spawn Objects")]
    public void SpawnObjects()
    {
        if (targetMeshFilter == null || prefabsToSpawn == null || prefabsToSpawn.Length == 0)
        {
            Debug.LogWarning("Please assign a MeshFilter and at least one prefab.");
            return;
        }

        Mesh mesh = targetMeshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        int[] triangles = mesh.triangles;
        Transform meshTransform = targetMeshFilter.transform;

        // Calculate surface areas of all triangles to sample uniformly
        int triangleCount = triangles.Length / 3;
        float[] cumulativeAreas = new float[triangleCount];
        float totalArea = 0f;

        for (int i = 0; i < triangleCount; i++)
        {
            Vector3 vA = vertices[triangles[i * 3]];
            Vector3 vB = vertices[triangles[i * 3 + 1]];
            Vector3 vC = vertices[triangles[i * 3 + 2]];

            float area = Vector3.Cross(vB - vA, vC - vA).magnitude * 0.5f;
            totalArea += area;
            cumulativeAreas[i] = totalArea;
        }

        for (int i = 0; i < spawnCount; i++)
        {
            // Pick a random triangle weighted by area
            int triIndex = SelectRandomTriangle(cumulativeAreas, totalArea);

            Vector3 vA = vertices[triangles[triIndex * 3]];
            Vector3 vB = vertices[triangles[triIndex * 3 + 1]];
            Vector3 vC = vertices[triangles[triIndex * 3 + 2]];

            Vector3 nA = normals[triangles[triIndex * 3]];
            Vector3 nB = normals[triangles[triIndex * 3 + 1]];
            Vector3 nC = normals[triangles[triIndex * 3 + 2]];

            // Generate random barycentric coordinates
            float u = Random.value;
            float v = Random.value;
            if (u + v > 1f)
            {
                u = 1f - u;
                v = 1f - v;
            }
            float w = 1f - u - v;

            // Interpolate local position and surface normal
            Vector3 localPos = u * vA + v * vB + w * vC;
            Vector3 localNormal = (u * nA + v * nB + w * nC).normalized;

            // Transform to world space
            Vector3 worldPos = meshTransform.TransformPoint(localPos);
            Vector3 worldNormal = meshTransform.TransformDirection(localNormal);

            // Determine rotation
            Quaternion rotation;
            if (alignWithNormals)
            {
                // Align UP with the face normal, with a random Y-axis spin
                Quaternion normalAlignment = Quaternion.FromToRotation(Vector3.up, worldNormal);
                Quaternion randomYRotation = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up);
                rotation = normalAlignment * randomYRotation;
            }
            else
            {
                // Keep world upright, with a random Y-axis spin
                rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }

            // Apply world-space position offset relative to rotation alignment
            Vector3 finalPosition = worldPos + (rotation * spawnOffset);

            // Instantiate
            GameObject prefab = prefabsToSpawn[Random.Range(0, prefabsToSpawn.Length)];
            Instantiate(prefab, finalPosition, rotation, transform);
        }
    }

    private int SelectRandomTriangle(float[] cumulativeAreas, float totalArea)
    {
        float randomPoint = Random.value * totalArea;
        int low = 0;
        int high = cumulativeAreas.Length - 1;

        while (low < high)
        {
            int mid = (low + high) / 2;
            if (cumulativeAreas[mid] < randomPoint)
                low = mid + 1;
            else
                high = mid;
        }

        return low;
    }
}
