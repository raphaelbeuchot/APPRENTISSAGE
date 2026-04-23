using UnityEngine;
using UnityEngine.Splines;

public class SplinePitZone : MonoBehaviour
{
    [Header("Pit Settings")]
    public float depth = 2f;

    [Header("Mesh Settings")]
    public int splineSampleCount = 32;

    [Header("Rim Settings")]
    public float rimMargin = 0.5f;

    [Header("Materials")]
    public Material wallMaterial;
    public Material floorMaterial;
    public Material rimMaterial;

    private SplineContainer splineContainer;

    public SplineContainer GetSplineContainer()
    {
        if (splineContainer == null)
            splineContainer = GetComponent<SplineContainer>();
        if (splineContainer == null)
            splineContainer = GetComponentInChildren<SplineContainer>();
        return splineContainer;
    }

    public void GenerateMeshes()
    {
        SplineContainer sc = GetSplineContainer();
        if (sc == null)
        {
            Debug.LogError("[SplinePitZone] No SplineContainer found on " + name + ". Add one manually.");
            return;
        }

        if (sc.Spline.Count < 3)
        {
            Debug.LogError("[SplinePitZone] Spline needs at least 3 knots.");
            return;
        }

        ClearMeshes();

        Vector3[] contour = SplinePitMeshGenerator.SampleSpline(sc, splineSampleCount);

        // Walls
        GameObject wallGO = new GameObject("SplinePit_Walls");
        wallGO.transform.SetParent(transform);
        wallGO.transform.localPosition = Vector3.zero;
        MeshFilter wallMF = wallGO.AddComponent<MeshFilter>();
        MeshRenderer wallMR = wallGO.AddComponent<MeshRenderer>();
        wallMF.sharedMesh = SplinePitMeshGenerator.GenerateWallMesh(contour, depth);
        if (wallMaterial != null) wallMR.sharedMaterial = wallMaterial;

        // Floor
        GameObject floorGO = new GameObject("SplinePit_Floor");
        floorGO.transform.SetParent(transform);
        floorGO.transform.localPosition = Vector3.zero;
        MeshFilter floorMF = floorGO.AddComponent<MeshFilter>();
        MeshRenderer floorMR = floorGO.AddComponent<MeshRenderer>();
        floorMF.sharedMesh = SplinePitMeshGenerator.GenerateFloorMeshFan(contour, depth);
        if (floorMaterial != null) floorMR.sharedMaterial = floorMaterial;

        // Rim
        GameObject rimGO = new GameObject("SplinePit_Rim");
        rimGO.transform.SetParent(transform);
        rimGO.transform.localPosition = Vector3.zero;
        rimGO.layer = LayerMask.NameToLayer("Ground");
        MeshFilter rimMF = rimGO.AddComponent<MeshFilter>();
        MeshRenderer rimMR = rimGO.AddComponent<MeshRenderer>();
        Mesh rimMesh = SplinePitMeshGenerator.GenerateRimMesh(contour, rimMargin);
        rimMF.sharedMesh = rimMesh;
        MeshCollider rimMC = rimGO.AddComponent<MeshCollider>();
        rimMC.sharedMesh = rimMesh;
        if (rimMaterial != null) rimMR.sharedMaterial = rimMaterial;
    }

    public void ClearMeshes()
    {
        string[] names = { "SplinePit_Walls", "SplinePit_Floor", "SplinePit_Rim" };
        foreach (string n in names)
        {
            Transform t = transform.Find(n);
            if (t != null) DestroyImmediate(t.gameObject);
        }
    }

    public float GetDepth()
    {
        return depth;
    }
}