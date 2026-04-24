using Pathfinding;
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

    private void Start()
    {
        if (AstarPath.active != null)
            ApplyAStarWalkability();
    }

    public SplineContainer GetSplineContainer()
    {
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

        Vector3[] contourWorld = SplinePitMeshGenerator.SampleSpline(sc, splineSampleCount);
        Vector3[] contour = new Vector3[contourWorld.Length];
        for (int i = 0; i < contourWorld.Length; i++)
            contour[i] = transform.InverseTransformPoint(contourWorld[i]);

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

        // Trigger entry
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        for (int i = 0; i < contour.Length; i++)
        {
            if (contour[i].x < minX) minX = contour[i].x;
            if (contour[i].x > maxX) maxX = contour[i].x;
            if (contour[i].z < minZ) minZ = contour[i].z;
            if (contour[i].z > maxZ) maxZ = contour[i].z;
        }

        float triggerY = contour[0].y - depth * 0.5f;
        float sizeX = maxX - minX;
        float sizeZ = maxZ - minZ;
        float centerX = (minX + maxX) * 0.5f;
        float centerZ = (minZ + maxZ) * 0.5f;

        GameObject triggerGO = new GameObject("SplinePit_Trigger");
        triggerGO.transform.SetParent(transform);
        triggerGO.transform.localPosition = new Vector3(centerX, triggerY, centerZ);
        BoxCollider bc = triggerGO.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(sizeX, depth, sizeZ);
        triggerGO.AddComponent<SplinePitTriggerZone>();

        Debug.Log("[SplinePitZone] Meshes generated for " + name);
    }

    public void ClearMeshes()
    {
        string[] names = { "SplinePit_Walls", "SplinePit_Floor", "SplinePit_Rim", "SplinePit_Trigger" };
        foreach (string n in names)
        {
            Transform t = transform.Find(n);
            if (t != null) DestroyImmediate(t.gameObject);
        }
    }

    public void ApplyAStarWalkability()
    {
        Vector3[] contourWorld = SplinePitMeshGenerator.SampleSpline(GetSplineContainer(), splineSampleCount);

        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        float y = contourWorld[0].y;

        for (int i = 0; i < contourWorld.Length; i++)
        {
            if (contourWorld[i].x < minX) minX = contourWorld[i].x;
            if (contourWorld[i].x > maxX) maxX = contourWorld[i].x;
            if (contourWorld[i].z < minZ) minZ = contourWorld[i].z;
            if (contourWorld[i].z > maxZ) maxZ = contourWorld[i].z;
        }

        Bounds pitBounds = new Bounds(
            new Vector3((minX + maxX) * 0.5f, y - depth * 0.5f, (minZ + maxZ) * 0.5f),
            new Vector3(maxX - minX, depth, maxZ - minZ)
        );

        AstarPath.active.AddWorkItem(new Pathfinding.AstarWorkItem(ctx =>
        {
            GraphUpdateObject guo = new GraphUpdateObject(pitBounds);
            guo.modifyWalkability = true;
            guo.setWalkability = false;
            AstarPath.active.UpdateGraphs(guo);
        }));
    }

    public float GetDepth()
    {
        return depth;
    }
}