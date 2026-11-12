using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Cache des decors (ex. grands panneaux du fond) quand la camera depasse leur Z monde, pour qu'ils ne
/// bouchent pas la vue. A poser sur le parent : prend tous les Renderers enfants. Chaque renderer est
/// compare a son propre Z (centre de ses bounds) + "Z Offset".
/// Par defaut l'ombre reste (Shadows Only) : on ne voit plus le panneau mais le sol garde son ombre.
/// </summary>
public class HideWhenCameraPasses : MonoBehaviour
{
    [Tooltip("Cache quand la camera a un Z PLUS GRAND que le panneau. Decoche : quand elle a un Z plus petit.")]
    [SerializeField] private bool hideWhenCameraZGreater = true;
    [Tooltip("Decalage du seuil (m) : positif = le seuil recule dans le sens du test (cache plus tard).")]
    [SerializeField] private float zOffset = 0f;
    [Tooltip("Garder l'ombre du panneau quand il est cache.")]
    [SerializeField] private bool keepShadow = true;
    [Tooltip("Camera suivie. Vide = Camera.main.")]
    [SerializeField] private Camera targetCamera;

    private Renderer[] renderers;
    private float[] thresholds;
    private ShadowCastingMode[] baseShadowModes;
    private bool[] hidden;
    private bool[] baseEnabled;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        thresholds = new float[renderers.Length];
        baseShadowModes = new ShadowCastingMode[renderers.Length];
        hidden = new bool[renderers.Length];
        baseEnabled = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            thresholds[i] = renderers[i].bounds.center.z;
            baseShadowModes[i] = renderers[i].shadowCastingMode;
            baseEnabled[i] = renderers[i].enabled;
        }
    }

    private void LateUpdate()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        float camZ = targetCamera.transform.position.z;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;

            bool hide = hideWhenCameraZGreater
                ? camZ > thresholds[i] + zOffset
                : camZ < thresholds[i] - zOffset;
            if (hide == hidden[i]) continue;
            hidden[i] = hide;

            if (keepShadow)
                r.shadowCastingMode = hide ? ShadowCastingMode.ShadowsOnly : baseShadowModes[i];
            else
                r.enabled = !hide && baseEnabled[i];
        }
    }

    private void OnDisable()
    {
        // Remet tout visible si on coupe le composant
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].shadowCastingMode = baseShadowModes[i];
            renderers[i].enabled = baseEnabled[i];
            hidden[i] = false;
        }
    }
}
