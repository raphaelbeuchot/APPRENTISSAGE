using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Reflet des personnages (joueur, ennemis) sur un sol glace, en silhouette d'aplat.
/// Avant le rendu de chaque camera de jeu (split-screen compris), dessine les meshes des personnages
/// retournes sous le plan de la glace dans une texture (blanc = silhouette, noir = rien), publiee en
/// texture globale _FloorReflectionTex. Le shader du sol la lit a la position ecran et choisit la couleur.
/// Personnages = objets portant PlayerHealth ou EnemyHealth (tous leurs MeshRenderer / SkinnedMeshRenderer),
/// quels que soient les layers. Pas de reglage URP.
/// Setup : sur le sol glace (ou un objet a sa hauteur, plan = position + normale "up" de cet objet).
/// Un seul plan par niveau : la glace a une seule hauteur.
/// </summary>
public class FloorCharacterReflection : MonoBehaviour
{
    public const string TextureName = "_FloorReflectionTex";
    private const string PlaneName = "_FloorReflectionPlane";
    private const string MatrixName = "_FloorReflectionVP";

    [Tooltip("Shader Hidden/FloorReflectionSilhouette (Project/Shaders)")]
    [SerializeField] private Shader silhouetteShader;
    [Tooltip("Decalage du plan du reflet le long de la normale (m), si le pivot n'est pas exactement a la surface")]
    [SerializeField] private float planeOffset = 0f;
    [Tooltip("Resolution de la texture par rapport a l'ecran (0.5 = moitie)")]
    [Range(0.1f, 1f)] [SerializeField] private float resolutionScale = 0.5f;
    [Tooltip("Layers jamais reflechis (pips de vie, etc.)")]
    [SerializeField] private LayerMask excludedLayers = 0;
    [Tooltip("Frequence de recherche des personnages (s) : ennemis qui apparaissent, meshes actives")]
    [SerializeField] private float refreshInterval = 1f;
    [Header("Debug")]
    [Tooltip("En jeu : derniere texture du reflet (double-clic pour la voir). Blanc = silhouette")]
    [SerializeField] private RenderTexture debugTexture;
    private int lastRendererCount = -1;

    private Material silhouetteMaterial;
    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly Dictionary<Camera, RenderTexture> textures = new Dictionary<Camera, RenderTexture>();
    private CommandBuffer cmd;
    private float refreshTimer;

    // Valeur par defaut pour tous les niveaux : pas de reflet (le shader du sol lit du noir)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetGlobal()
    {
        Shader.SetGlobalTexture(TextureName, Texture2D.blackTexture);
    }

    private void OnEnable()
    {
        if (silhouetteShader == null) silhouetteShader = Shader.Find("Hidden/FloorReflectionSilhouette");
        if (silhouetteShader == null)
        {
            Debug.LogError("[FloorCharacterReflection] Shader de silhouette manquant sur " + name);
            enabled = false;
            return;
        }
        silhouetteMaterial = new Material(silhouetteShader) { hideFlags = HideFlags.HideAndDontSave };
        cmd = new CommandBuffer { name = "FloorCharacterReflection" };
        refreshTimer = 0f;
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        Shader.SetGlobalTexture(TextureName, Texture2D.blackTexture);
        foreach (RenderTexture rt in textures.Values)
            if (rt != null) { rt.Release(); Destroy(rt); }
        textures.Clear();
        if (cmd != null) { cmd.Release(); cmd = null; }
        if (silhouetteMaterial != null) { Destroy(silhouetteMaterial); silhouetteMaterial = null; }
    }

    private void Update()
    {
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f) return;
        refreshTimer = refreshInterval;
        CollectRenderers();
    }

    private void CollectRenderers()
    {
        renderers.Clear();
        foreach (PlayerHealth p in FindObjectsOfType<PlayerHealth>()) AddRenderers(p.gameObject);
        foreach (EnemyHealth e in FindObjectsOfType<EnemyHealth>()) AddRenderers(e.gameObject);
        if (renderers.Count != lastRendererCount)
        {
            lastRendererCount = renderers.Count;
            Debug.Log("[FloorCharacterReflection] " + renderers.Count + " meshes de personnages reflechis");
        }
    }

    private void AddRenderers(GameObject root)
    {
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if ((excludedLayers.value & (1 << r.gameObject.layer)) != 0) continue;
            renderers.Add(r);
        }
    }

    private void OnBeginCamera(ScriptableRenderContext context, Camera cam)
    {
        if (cam.cameraType != CameraType.Game) return;
        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        if (data != null && data.renderType == CameraRenderType.Overlay) return;

        RenderTexture rt = GetTexture(cam);
        if (rt == null) return;

        // Plan de la glace
        Vector3 normal = transform.up;
        Vector3 point = transform.position + normal * planeOffset;
        Vector4 plane = new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, point));

        Matrix4x4 view = cam.worldToCameraMatrix * ReflectionMatrix(plane);
        Matrix4x4 proj = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);

        cmd.Clear();
        cmd.SetRenderTarget(rt);
        cmd.ClearRenderTarget(false, true, Color.clear);
        cmd.SetGlobalMatrix(MatrixName, proj * view);
        cmd.SetGlobalVector(PlaneName, plane);
        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            int subMeshes = r.sharedMaterials.Length;
            for (int s = 0; s < subMeshes; s++)
                cmd.DrawRenderer(r, silhouetteMaterial, s, 0);
        }
        cmd.SetGlobalTexture(TextureName, rt);
        Graphics.ExecuteCommandBuffer(cmd);
        debugTexture = rt;
    }

    private RenderTexture GetTexture(Camera cam)
    {
        int w = Mathf.Max(1, Mathf.RoundToInt(cam.pixelWidth * resolutionScale));
        int h = Mathf.Max(1, Mathf.RoundToInt(cam.pixelHeight * resolutionScale));
        RenderTexture rt;
        if (textures.TryGetValue(cam, out rt) && rt != null && rt.width == w && rt.height == h) return rt;
        if (rt != null) { rt.Release(); Destroy(rt); }
        RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8)
            ? RenderTextureFormat.R8 : RenderTextureFormat.ARGB32;
        rt = new RenderTexture(w, h, 0, format) { name = "FloorReflection_" + cam.name };
        rt.Create();
        textures[cam] = rt;
        return rt;
    }

    // Symetrie par rapport au plan (normale unitaire, d)
    private static Matrix4x4 ReflectionMatrix(Vector4 p)
    {
        Matrix4x4 m = Matrix4x4.identity;
        m.m00 = 1f - 2f * p.x * p.x; m.m01 = -2f * p.x * p.y;     m.m02 = -2f * p.x * p.z;     m.m03 = -2f * p.w * p.x;
        m.m10 = -2f * p.y * p.x;     m.m11 = 1f - 2f * p.y * p.y; m.m12 = -2f * p.y * p.z;     m.m13 = -2f * p.w * p.y;
        m.m20 = -2f * p.z * p.x;     m.m21 = -2f * p.z * p.y;     m.m22 = 1f - 2f * p.z * p.z; m.m23 = -2f * p.w * p.z;
        return m;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.6f);
        Vector3 c = transform.position + transform.up * planeOffset;
        Gizmos.matrix = Matrix4x4.TRS(c, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(4f, 0f, 4f));
    }
}
