using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Sequence de fin du boss de Rush Hour IV, calee sur l'explosion du dernier cercle.
/// Branchement : BossCircleExploder.onLastCircleExploded -> Play.
/// Apres "Delay" (compte depuis l'envol des ampoules) :
/// 1. les spheres du coeur (TestSpheres) retrecissent jusqu'a zero ;
/// 2. le cylindre grossit jusqu'a "Grow Scale" pendant que son emission monte de "Ramp Intensity" crans
///    (comme l'Intensity du selecteur HDR : +1 = x2), pendant "Ramp Duration" ;
/// 3. il se retracte jusqu'a zero en "Retract Duration" (0 = d'un coup) : onRetract au debut de la retraction
///    (ex. GameObject.SetActive pour changer de Global Volume).
/// La reaction au cycle (CycleReactiveRenderer) est coupee : on part du materiau et de l'echelle en place.
/// </summary>
public class BossEndSequence : MonoBehaviour
{
    [Header("Coeur de la sentinelle")]
    [Tooltip("Renderer du cylindre (ConcentricCircles Manager > Cylinder).")]
    [SerializeField] private Renderer cylinder;
    [Tooltip("Racine des spheres (CentralLightTest > TestSpheres) : son echelle descend a zero, toutes les spheres avec.")]
    [SerializeField] private Transform spheres;

    [Tooltip("Attente avant de commencer (s), depuis Play (envol des ampoules du dernier cercle).")]
    [Min(0f)] [SerializeField] private float delay = 1f;

    [Header("Spheres qui retrecissent")]
    [Tooltip("Duree du retrecissement des spheres jusqu'a zero (s), avant la montee du cylindre.")]
    [Min(0f)] [SerializeField] private float spheresShrinkDuration = 1f;

    [Header("Cylindre qui grossit et s'illumine")]
    [Tooltip("Duree de la montee (s) : echelle et emission ensemble.")]
    [Min(0.01f)] [SerializeField] private float rampDuration = 2f;
    [Tooltip("Crans d'emission gagnes, comme l'Intensity du selecteur HDR (+1 = x2, +6 = x64).")]
    [SerializeField] private float rampIntensity = 6f;
    [Tooltip("Echelle en fin de montee, en multiple de l'echelle de depart (5 = 500 %).")]
    [Min(0f)] [SerializeField] private float growScale = 5f;

    [Header("Retraction")]
    [Tooltip("Duree de la retraction jusqu'a zero (s). 0 = d'un coup.")]
    [Min(0f)] [SerializeField] private float retractDuration = 0.05f;
    [Tooltip("Debut de la retraction. Ex : GameObject.SetActive pour eteindre le Global Volume du niveau et allumer " +
             "celui de la transition room.")]
    [SerializeField] private UnityEvent onRetract;

    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
    private bool played;

    [ContextMenu("Play")]
    public void Play()
    {
        if (played) return;
        played = true;
        StartCoroutine(Sequence());
    }

    private IEnumerator Sequence()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        // 1. Spheres : echelle jusqu'a zero, puis cachees
        if (spheres != null)
        {
            Vector3 sphereScale = spheres.localScale;
            float s0 = Time.time;
            while (Time.time - s0 < spheresShrinkDuration)
            {
                float p = (Time.time - s0) / spheresShrinkDuration;
                spheres.localScale = sphereScale * (1f - Mathf.SmoothStep(0f, 1f, p));
                yield return null;
            }
            spheres.localScale = Vector3.zero;
            spheres.gameObject.SetActive(false);
        }

        if (cylinder == null)
        {
            Debug.LogWarning("[BOSS END] Pas de cylindre : montee sautee", this);
            onRetract?.Invoke();
            yield break;
        }

        // Son OnDisable efface le property block et remet l'echelle : on lit materiau et echelle apres
        var reactive = cylinder.GetComponent<CycleReactiveRenderer>();
        if (reactive != null) reactive.enabled = false;

        Transform cyl = cylinder.transform;
        Vector3 baseScale = cyl.localScale;

        Material mat = cylinder.sharedMaterial;
        Color baseEmission = Color.black;
        if (mat != null && mat.HasProperty(EmissionColorID))
        {
            baseEmission = mat.GetColor(EmissionColorID);
            if (!mat.IsKeywordEnabled("_EMISSION"))
                Debug.LogWarning($"[BOSS END] {mat.name} : Emission decochee, la montee ne se verra pas", this);
        }
        // Emission noire : x64 reste noir, on part d'un gris pour que la montee se voie
        if (Mathf.Max(baseEmission.r, baseEmission.g, baseEmission.b) < 0.01f)
            baseEmission = new Color(0.1f, 0.1f, 0.1f);

        // 2. Montee : echelle et emission ensemble
        var block = new MaterialPropertyBlock();
        float start = Time.time;
        while (Time.time - start < rampDuration)
        {
            float p = (Time.time - start) / rampDuration;
            SetEmission(block, baseEmission * Mathf.Pow(2f, rampIntensity * p));
            cyl.localScale = baseScale * Mathf.Lerp(1f, growScale, p);
            yield return null;
        }
        SetEmission(block, baseEmission * Mathf.Pow(2f, rampIntensity));
        cyl.localScale = baseScale * growScale;

        // 3. Retraction jusqu'a zero
        Debug.Log("[BOSS END] Retraction du cylindre");
        onRetract?.Invoke();
        Vector3 bigScale = cyl.localScale;
        float r0 = Time.time;
        while (Time.time - r0 < retractDuration)
        {
            float p = (Time.time - r0) / retractDuration;
            cyl.localScale = bigScale * (1f - p);
            yield return null;
        }
        cyl.localScale = Vector3.zero;
        cylinder.enabled = false;
    }

    private void SetEmission(MaterialPropertyBlock block, Color c)
    {
        cylinder.GetPropertyBlock(block);
        block.SetColor(EmissionColorID, c);
        cylinder.SetPropertyBlock(block);
    }
}
