using Pathfinding;
using UnityEngine;
using UnityEngine.Splines;

public class SplinePitZone : MonoBehaviour
{
    [Header("Pit Settings")]
    public float depth = 2f;

    [Header("Mesh Settings")]
    public int splineSampleCount = 32;

    public enum RimShape { Rectangle, Ellipse }

    [Header("Rim Settings")]
    [Tooltip("Contour exterieur du rim. Rectangle : rectangle englobant de la fosse + marge. " +
             "Ellipse : sol de salle ronde, centre et rayons ci-dessous.")]
    public RimShape rimShape = RimShape.Rectangle;
    [Tooltip("Rectangle : marge autour de la fosse.")]
    public float rimMargin = 0.5f;
    // Ellipse : centre du contour exterieur, en X / Z locaux de la fosse (0 = pivot de la fosse). Pas affiche :
    // l'Inspector montre a la place le "Decalage de la fosse" (= -centre), qui deplace la fosse et garde l'ellipse en place.
    [HideInInspector] public Vector2 rimEllipseCenter = Vector2.zero;
    [Tooltip("Ellipse : rayons en X et en Z locaux. Egaux = cercle.")]
    public Vector2 rimEllipseRadii = new Vector2(6f, 6f);
    [Tooltip("Ellipse : nombre de facettes sur le tour complet du contour exterieur.")]
    [Range(8, 256)] public int rimEllipseSegments = 64;

    [Header("Materials")]
    public Material wallMaterial;
    public Material floorMaterial;
    public Material rimMaterial;

    private SplineContainer splineContainer;

    // Le contenu (SO) decide de tout : pas de SO ou SO Empty = fosse vide
    [Header("Fill Settings")]
    public float fillLevel = 0.5f;
    [Tooltip("Comportement du contenu (eau, lave, acide...). Pas de SO ou SO Empty = fosse vide")]
    public PitContentType fillContentType;
    [Tooltip("Look de la surface, propre a cette fosse (le SO ne decide que du comportement)")]
    public Material fillMaterial;

    // Contenu Spikes : pas de surface plate, des triangles verticaux poses au fond, face a -Z (fixes, pas billboard).
    // Le Fill Level regle toujours la hauteur du trigger qui tue, independamment de la hauteur des pics.
    [Header("Spikes (contenu Spikes seulement)")]
    [Tooltip("Nombre de pics par m2 de fond : la fosse garde le meme aspect si on l'agrandit")]
    public float spikeDensity = 4f;
    [Tooltip("Placement : 0 = grille reguliere, 1 = chaque pic n'importe ou dans sa case de la grille")]
    [Range(0f, 1f)] public float spikeRandomness = 1f;
    [Tooltip("Largeur de la base des pics (m), tiree au hasard entre min et max")]
    public Vector2 spikeBaseRange = new Vector2(0.15f, 0.4f);
    [Tooltip("Hauteur des pics (m), tiree au hasard entre min et max")]
    public Vector2 spikeHeightRange = new Vector2(0.2f, 0.6f);
    [Tooltip("Graine du tirage : changer pour une autre disposition")]
    public int spikeSeed = 0;

    // Reglages de la forme rapide "rectangle arrondi" (dessines par SplinePitZoneEditor) :
    // memorises par fosse, ce sont ceux de la derniere creation, pas un reflet de la spline actuelle.
    [HideInInspector] public float quickRectWidth = 6f;   // axe X local de la spline
    [HideInInspector] public float quickRectLength = 4f;  // axe Z local de la spline
    [HideInInspector] public float quickRectRadius = 1f;
    [HideInInspector] public float quickRectBulgeX = 0f;  // fleche au milieu des cotes paralleles a X (gonflent vers -Z / +Z)
    [HideInInspector] public float quickRectBulgeZ = 0f;  // fleche au milieu des cotes paralleles a Z (gonflent vers -X / +X)

    public PitContentType.ContentCategory FillCategory =>
        fillContentType != null ? fillContentType.category : PitContentType.ContentCategory.Empty;

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
        wallGO.layer = LayerMask.NameToLayer("PitWall");
        MeshFilter wallMF = wallGO.AddComponent<MeshFilter>();
        MeshRenderer wallMR = wallGO.AddComponent<MeshRenderer>();
        Mesh wallMesh = SplinePitMeshGenerator.GenerateWallMesh(contour, depth);
        wallMF.sharedMesh = wallMesh;
        MeshCollider wallMC = wallGO.AddComponent<MeshCollider>();
        wallMC.sharedMesh = wallMesh;
        if (wallMaterial != null) wallMR.sharedMaterial = wallMaterial;

        // Floor
        GameObject floorGO = new GameObject("SplinePit_Floor");
        floorGO.transform.SetParent(transform);
        floorGO.transform.localPosition = Vector3.zero;
        MeshFilter floorMF = floorGO.AddComponent<MeshFilter>();
        MeshRenderer floorMR = floorGO.AddComponent<MeshRenderer>();
        Mesh floorMesh = SplinePitMeshGenerator.GenerateFloorMeshFan(contour, depth);
        floorMF.sharedMesh = floorMesh;
        MeshCollider floorMC = floorGO.AddComponent<MeshCollider>();
        floorMC.sharedMesh = floorMesh;
        if (floorMaterial != null) floorMR.sharedMaterial = floorMaterial;


        // Rim
        GameObject rimGO = new GameObject("SplinePit_Rim");
        rimGO.transform.SetParent(transform);
        rimGO.transform.localPosition = Vector3.zero;
        rimGO.layer = LayerMask.NameToLayer("Ground");
        MeshFilter rimMF = rimGO.AddComponent<MeshFilter>();
        MeshRenderer rimMR = rimGO.AddComponent<MeshRenderer>();
        Mesh rimMesh = rimShape == RimShape.Ellipse
            ? SplinePitMeshGenerator.GenerateRimMeshEllipse(contour, rimEllipseCenter, rimEllipseRadii, rimEllipseSegments)
            : SplinePitMeshGenerator.GenerateRimMesh(contour, rimMargin);
        rimMF.sharedMesh = rimMesh;
        MeshCollider rimMC = rimGO.AddComponent<MeshCollider>();
        rimMC.sharedMesh = rimMesh;
        if (rimMaterial != null) rimMR.sharedMaterial = rimMaterial;

        // Calcul bounding box commun
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        for (int i = 0; i < contour.Length; i++)
        {
            if (contour[i].x < minX) minX = contour[i].x;
            if (contour[i].x > maxX) maxX = contour[i].x;
            if (contour[i].z < minZ) minZ = contour[i].z;
            if (contour[i].z > maxZ) maxZ = contour[i].z;
        }
        float sizeX = maxX - minX;
        float sizeZ = maxZ - minZ;
        float centerX = (minX + maxX) * 0.5f;
        float centerZ = (minZ + maxZ) * 0.5f;

        // Trigger entry
        float sinkBelowRim = 0.3f;
        float triggerY = contour[0].y - sinkBelowRim - depth * 0.5f;

        GameObject triggerGO = new GameObject("SplinePit_Trigger");
        triggerGO.transform.SetParent(transform);
        triggerGO.transform.localPosition = new Vector3(centerX, triggerY, centerZ);
        BoxCollider bc = triggerGO.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(sizeX, depth, sizeZ);
        triggerGO.AddComponent<SplinePitTriggerZone>();

        // Fill
        if (FillCategory != PitContentType.ContentCategory.Empty && fillLevel > 0f)
        {
            float fillY = contour[0].y - depth + fillLevel;

            bool spikes = FillCategory == PitContentType.ContentCategory.Spikes;
            GameObject fillGO = new GameObject(spikes ? "SplinePit_Spikes" : "SplinePit_Fill");
            fillGO.transform.SetParent(transform);
            fillGO.transform.localPosition = Vector3.zero;
            fillGO.transform.localRotation = Quaternion.identity;
            fillGO.transform.localScale = Vector3.one;
            MeshFilter fillMF = fillGO.AddComponent<MeshFilter>();
            MeshRenderer fillMR = fillGO.AddComponent<MeshRenderer>();
            fillMF.sharedMesh = spikes
                ? GenerateSpikesMesh(contour, contour[0].y - depth)
                : SplinePitMeshGenerator.GenerateFillMesh(contour, depth, fillLevel);
            if (fillMaterial != null) fillMR.sharedMaterial = fillMaterial;

            GameObject fillTriggerGO = new GameObject("SplinePit_FillTrigger");
            fillTriggerGO.transform.SetParent(transform);
            fillTriggerGO.transform.localPosition = new Vector3(centerX, fillY - fillLevel * 0.5f, centerZ);
            BoxCollider fillBC = fillTriggerGO.AddComponent<BoxCollider>();
            fillBC.isTrigger = true;
            fillBC.size = new Vector3(sizeX, fillLevel, sizeZ);
            SplinePitFillController fillCtrl = fillTriggerGO.AddComponent<SplinePitFillController>();
            fillCtrl.fillContentType = fillContentType;
        }

        Debug.Log("[SplinePitZone] Meshes generated for " + name);
    }

    public void ClearMeshes()
    {
        string[] names = { "SplinePit_Walls", "SplinePit_Floor", "SplinePit_Rim", "SplinePit_Trigger", "SplinePit_Fill", "SplinePit_Spikes", "SplinePit_FillTrigger" }; foreach (string n in names)
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

    // Pics : un par case d'une grille (pas = 1 / racine de la densite), decale dans sa case selon spikeRandomness,
    // garde s'il tombe dans le contour. Tailles tirees au hasard (graine fixe), base au fond de la fosse.
    // Construits en monde (face a -Z monde, axe Y monde) puis ramenes en local : la fosse peut etre tournee.
    // Une seule face, sens horaire vue depuis -Z (la camera regarde toujours vers +Z). UV en metres.
    Mesh GenerateSpikesMesh(Vector3[] contour, float floorY)
    {
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        for (int i = 0; i < contour.Length; i++)
        {
            minX = Mathf.Min(minX, contour[i].x); maxX = Mathf.Max(maxX, contour[i].x);
            minZ = Mathf.Min(minZ, contour[i].z); maxZ = Mathf.Max(maxZ, contour[i].z);
        }
        float step = 1f / Mathf.Sqrt(Mathf.Max(0.01f, spikeDensity));
        // Grille centree sur la fosse
        int cellsX = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / step));
        int cellsZ = Mathf.Max(1, Mathf.CeilToInt((maxZ - minZ) / step));
        float startX = (minX + maxX - (cellsX - 1) * step) * 0.5f;
        float startZ = (minZ + maxZ - (cellsZ - 1) * step) * 0.5f;

        Vector3 right = transform.InverseTransformDirection(Vector3.right);
        Vector3 up = transform.InverseTransformDirection(Vector3.up);

        var verts = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var tris = new System.Collections.Generic.List<int>();
        var rng = new System.Random(spikeSeed);
        float Range(Vector2 r) => Mathf.Lerp(r.x, r.y, (float)rng.NextDouble());

        for (int cell = 0; cell < cellsX * cellsZ; cell++)
        {
            // Tirages faits pour chaque case, meme rejetee : bouger un reglage ne rebat pas toute la fosse
            float x = startX + (cell % cellsX) * step + ((float)rng.NextDouble() - 0.5f) * step * spikeRandomness;
            float z = startZ + (cell / cellsX) * step + ((float)rng.NextDouble() - 0.5f) * step * spikeRandomness;
            float halfBase = Range(spikeBaseRange) * 0.5f;
            float height = Range(spikeHeightRange);

            Vector3 center = new Vector3(x, floorY, z);
            Vector3 left = center - right * halfBase;
            Vector3 rightPt = center + right * halfBase;
            // Les deux coins de la base dans la fosse : pas de pic qui traverse le mur
            if (!InsideContour(contour, left) || !InsideContour(contour, rightPt)) continue;

            int v = verts.Count;
            verts.Add(left);
            verts.Add(center + up * height);
            verts.Add(rightPt);
            Vector3 w = transform.TransformPoint(center);
            uvs.Add(new Vector2(w.x - halfBase, 0f));
            uvs.Add(new Vector2(w.x, height));
            uvs.Add(new Vector2(w.x + halfBase, 0f));
            tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
        }

        Mesh mesh = new Mesh();
        mesh.name = "SplinePit_Spikes";
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Point dans le polygone du contour (plan XZ local), par comptage de croisements
    static bool InsideContour(Vector3[] contour, Vector3 p)
    {
        bool inside = false;
        for (int i = 0, j = contour.Length - 1; i < contour.Length; j = i++)
        {
            Vector3 a = contour[i], b = contour[j];
            if ((a.z > p.z) != (b.z > p.z) &&
                p.x < (b.x - a.x) * (p.z - a.z) / (b.z - a.z) + a.x)
                inside = !inside;
        }
        return inside;
    }

    // Apercu du contour exterieur du rim en ellipse, pour le regler avant Generate Meshes.
    private void OnDrawGizmosSelected()
    {
        if (rimShape != RimShape.Ellipse) return;
        SplineContainer sc = GetSplineContainer();
        float y = sc != null ? transform.InverseTransformPoint(sc.transform.position).y : 0f;
        float rx = Mathf.Max(0.01f, rimEllipseRadii.x);
        float rz = Mathf.Max(0.01f, rimEllipseRadii.y);
        int seg = Mathf.Max(8, rimEllipseSegments);

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.yellow;
        Vector3 prev = new Vector3(rimEllipseCenter.x + rx, y, rimEllipseCenter.y);
        for (int i = 1; i <= seg; i++)
        {
            float a = Mathf.PI * 2f * i / seg;
            Vector3 p = new Vector3(rimEllipseCenter.x + rx * Mathf.Cos(a), y, rimEllipseCenter.y + rz * Mathf.Sin(a));
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }
}