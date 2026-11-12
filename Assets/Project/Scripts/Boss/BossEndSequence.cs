using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Sequence de fin du boss de Rush Hour IV, calee sur l'explosion du dernier cercle.
/// Branchement : BossCircleExploder.onLastCircleExploded -> Play.
/// Apres "Delay" (compte depuis l'envol des ampoules) :
/// 1. les spheres du coeur (TestSpheres) retrecissent jusqu'a zero ;
/// 2. le cylindre grossit jusqu'a "Grow Scale" pendant que son emission monte de "Ramp Intensity" crans
///    (comme l'Intensity du selecteur HDR : +1 = x2), pendant "Ramp Duration" ; le couvercle du pit glisse en X
///    en meme temps (duree a part) ;
/// 3. il se retracte jusqu'a zero en "Retract Duration" (0 = d'un coup) : onRetract au debut de la retraction
///    (ex. GameObject.SetActive pour changer de Global Volume) ;
/// 4. camera de sortie (CM_Exit), puis evacuation du decor (DecorExitSequencer) et rideau (EndCurtainRise) en
///    parallele -> onDecorEvacuated (ex. BossTransition.EndPlayerFreeze : le joueur reprend la main).
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

    [Header("Couvercle du pit")]
    [Tooltip("Couvercle deplace pendant la montee du cylindre (part en meme temps). Vide = rien.")]
    [SerializeField] private Transform pitLid;
    [Tooltip("Deplacement du couvercle en X monde (m).")]
    [SerializeField] private float pitLidMoveX = 10f;
    [Tooltip("Deplacement du couvercle en Z monde (m).")]
    [SerializeField] private float pitLidMoveZ = 0f;
    [Tooltip("Duree du deplacement (s).")]
    [Min(0.01f)] [SerializeField] private float pitLidDuration = 2f;

    [Header("Retraction")]
    [Tooltip("Duree de la retraction jusqu'a zero (s). 0 = d'un coup.")]
    [Min(0f)] [SerializeField] private float retractDuration = 0.05f;
    [Tooltip("Debut de la retraction. Ex : GameObject.SetActive pour eteindre le Global Volume du niveau et allumer " +
             "celui de la transition room.")]
    [SerializeField] private UnityEvent onRetract;

    [Header("Sortie")]
    [Tooltip("Camera de sortie, mise en avant a la fin de la retraction. Vide = pas de changement de plan.")]
    [SerializeField] private CinemachineCamera exitCamera;
    [Tooltip("Doit rester au-dessus de la camera sentinelle (30 par defaut dans BossTransition).")]
    [SerializeField] private int exitCameraPriority = 40;
    [Tooltip("Evacuation du decor (gauche / droite / haut), lancee a la fin de la retraction. Vide = pas d'evacuation.")]
    [SerializeField] private DecorExitSequencer decorExit;
    [Tooltip("Rideau leve en meme temps que l'evacuation. Vide = pas de rideau.")]
    [SerializeField] private EndCurtainRise curtain;
    [Tooltip("Securite : on n'attend jamais plus longtemps l'evacuation et le rideau (s).")]
    [Min(0f)] [SerializeField] private float maxEvacuationWait = 15f;
    [Tooltip("Decor evacue et rideau leve. Ex : BossTransition.EndPlayerFreeze (le joueur reprend la main).")]
    [SerializeField] private UnityEvent onDecorEvacuated;

    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
    private bool played;

    private void Awake()
    {
        if (exitCamera != null) exitCamera.Priority = 0;
    }

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
            yield return Exit();
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

        // 2. Montee : echelle et emission ensemble ; le couvercle part en meme temps
        if (pitLid != null) StartCoroutine(MovePitLid());
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

        yield return Exit();
    }

    // 4. Camera de sortie, puis evacuation du decor et rideau en parallele -> onDecorEvacuated
    private IEnumerator Exit()
    {
        if (exitCamera != null) exitCamera.Priority = exitCameraPriority;

        bool decorDone = decorExit == null;
        bool curtainDone = curtain == null;
        if (decorExit != null)
        {
            decorExit.OnDecorExitComplete += () => decorDone = true;
            decorExit.StartExit(0f); // la direction se calcule sur la position du joueur
        }
        if (curtain != null)
        {
            curtain.OnRiseComplete += () => curtainDone = true;
            curtain.Rise();
        }

        float t0 = Time.time;
        yield return new WaitUntil(() => (decorDone && curtainDone) || Time.time - t0 > maxEvacuationWait);
        if (!(decorDone && curtainDone))
            Debug.LogWarning($"[BOSS END] Evacuation non terminee apres {maxEvacuationWait} s (decor {decorDone}, rideau {curtainDone}) : on continue", this);

        Debug.Log("[BOSS END] Decor evacue");
        onDecorEvacuated?.Invoke();
    }

    private IEnumerator MovePitLid()
    {
        Vector3 from = pitLid.position;
        Vector3 to = from + new Vector3(pitLidMoveX, 0f, pitLidMoveZ);
        float t0 = Time.time;
        while (Time.time - t0 < pitLidDuration)
        {
            float p = (Time.time - t0) / pitLidDuration;
            pitLid.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, p));
            yield return null;
        }
        pitLid.position = to;
    }

    private void SetEmission(MaterialPropertyBlock block, Color c)
    {
        cylinder.GetPropertyBlock(block);
        block.SetColor(EmissionColorID, c);
        cylinder.SetPropertyBlock(block);
    }
}
