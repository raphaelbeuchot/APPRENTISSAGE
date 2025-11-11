using UnityEngine;

[ExecuteInEditMode]
public class PitZone : MonoBehaviour
{
    [Header("Grid Data")]
    public PitGridData gridData;

    [Header("Mesh References")]
    public MeshFilter wallsMeshFilter;
    public MeshRenderer wallsRenderer;
    public MeshFilter floorMeshFilter;
    public MeshRenderer floorRenderer;

    [Header("Materials")]
    public Material wallMaterial;
    public Material floorMaterial;

    private void OnValidate()
    {
        if (wallsMeshFilter == null)
        {
            wallsMeshFilter = GetComponentInChildren<MeshFilter>();
        }
    }

    public void GenerateMeshes()
    {
        if (gridData == null)
        {
            Debug.LogWarning("PitZone: No grid data assigned!");
            return;
        }

        SetupMeshObjects();

        Mesh wallsMesh = PitMeshGenerator.GenerateWallsMesh(gridData);
        Mesh floorMesh = PitMeshGenerator.GenerateFloorMesh(gridData);

        if (wallsMesh != null && wallsMeshFilter != null)
        {
            wallsMeshFilter.sharedMesh = wallsMesh;
        }

        if (floorMesh != null && floorMeshFilter != null)
        {
            floorMeshFilter.sharedMesh = floorMesh;
        }

        Debug.Log("PitZone: Meshes generated successfully!");
    }

    private void SetupMeshObjects()
    {
        // Cree ou trouve le child object pour les walls
        Transform wallsTransform = transform.Find("Walls");
        if (wallsTransform == null)
        {
            GameObject wallsObj = new GameObject("Walls");
            wallsObj.transform.SetParent(transform);
            wallsObj.transform.localPosition = Vector3.zero;
            wallsObj.transform.localRotation = Quaternion.identity;
            wallsTransform = wallsObj.transform;

            wallsMeshFilter = wallsObj.AddComponent<MeshFilter>();
            wallsRenderer = wallsObj.AddComponent<MeshRenderer>();
        }
        else
        {
            if (wallsMeshFilter == null)
                wallsMeshFilter = wallsTransform.GetComponent<MeshFilter>();
            if (wallsRenderer == null)
                wallsRenderer = wallsTransform.GetComponent<MeshRenderer>();
        }

        // Cree ou trouve le child object pour les floors
        Transform floorTransform = transform.Find("Floor");
        if (floorTransform == null)
        {
            GameObject floorObj = new GameObject("Floor");
            floorObj.transform.SetParent(transform);
            floorObj.transform.localPosition = Vector3.zero;
            floorObj.transform.localRotation = Quaternion.identity;
            floorTransform = floorObj.transform;

            floorMeshFilter = floorObj.AddComponent<MeshFilter>();
            floorRenderer = floorObj.AddComponent<MeshRenderer>();
        }
        else
        {
            if (floorMeshFilter == null)
                floorMeshFilter = floorTransform.GetComponent<MeshFilter>();
            if (floorRenderer == null)
                floorRenderer = floorTransform.GetComponent<MeshRenderer>();
        }

        // Assigne les materiaux par defaut si pas deja assignes
        if (wallMaterial != null && wallsRenderer != null)
        {
            wallsRenderer.sharedMaterial = wallMaterial;
        }

        if (floorMaterial != null && floorRenderer != null)
        {
            floorRenderer.sharedMaterial = floorMaterial;
        }
    }

    public void ClearMeshes()
    {
        if (wallsMeshFilter != null)
        {
            wallsMeshFilter.sharedMesh = null;
        }

        if (floorMeshFilter != null)
        {
            floorMeshFilter.sharedMesh = null;
        }

        Debug.Log("PitZone: Meshes cleared");
    }
}