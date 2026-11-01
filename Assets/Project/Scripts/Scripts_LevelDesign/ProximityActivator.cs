using System.Collections.Generic;
using UnityEngine;

// Active des GO quand le joueur s'en approche. Chaque cible a sa propre distance au joueur : une guirlande s'allume
// ampoule par ampoule a son passage. Les cibles sont desactivees au lancement.
// Cibles : tous les GO de la scene qui portent un ProximityTarget (le poser sur le prefab), ou la liste si elle est
// remplie. Sans liste, un seul ProximityActivator par scene (sinon deux se disputeraient les memes cibles).
// Ne pas le poser sur une cible (un GO desactive n'execute plus ses scripts : il ne pourrait pas se rallumer).
// Reste allume : une fois activee, une cible le reste.
// Suit la distance : la cible s'eteint quand le joueur s'eloigne au-dela de distance + marge (la marge evite
// le clignotement quand le joueur reste pile a la limite).
//
// Fondu : la cible grandit de 0 a son echelle d'origine selon la courbe (une courbe qui depasse 1 fait un "pop"),
// et retrecit de meme avant d'etre desactivee. Si elle se rallume en cours de retrecissement, elle repart de sa taille.
// Gresillement : a l'allumage depuis l'etat eteint, pendant que la cible grandit, ses renderers et lumieres (enfants
// compris) se coupent et se rallument au hasard, puis restent allumes. Ne touche pas aux materials.
public class ProximityActivator : MonoBehaviour
{
    public enum Mode { ResteAllume, SuitLaDistance }

    [Header("Cibles")]
    [Tooltip("GO a activer. Vide = tous les GO de la scene qui portent un ProximityTarget.")]
    [SerializeField] private List<GameObject> targets = new List<GameObject>();

    [Header("Declenchement")]
    [SerializeField] private Mode mode = Mode.ResteAllume;
    [Tooltip("Distance au joueur (mesuree depuis chaque cible) en dessous de laquelle la cible s'active.")]
    [Min(0f)] [SerializeField] private float activationDistance = 5f;
    [Tooltip("Suit la distance : la cible s'eteint au-dela de distance + marge.")]
    [Min(0f)] [SerializeField] private float hysteresis = 0.5f;
    [Tooltip("Coche : distance mesuree a plat (X / Z), la hauteur de la cible ne compte pas (ex. enseigne en haut d'un mur).")]
    [SerializeField] private bool ignoreHeight = true;
    [Tooltip("Intervalle entre deux verifications de distance, en secondes. 0 = a chaque frame.")]
    [Min(0f)] [SerializeField] private float checkInterval = 0.1f;

    [Header("Fondu (echelle)")]
    [Tooltip("Duree pour passer de 0 a l'echelle d'origine (et inversement a l'extinction). 0 = instantane.")]
    [Min(0f)] [SerializeField] private float fadeDuration = 0.25f;
    [Tooltip("Echelle (0 = rien, 1 = echelle d'origine) selon l'avancement du fondu. Depasser 1 = effet pop.")]
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Gresillement")]
    [SerializeField] private bool flicker = false;
    [Tooltip("Probabilite qu'une cible gresille a l'allumage (les autres s'allument proprement).")]
    [Range(0f, 1f)] [SerializeField] private float flickerChance = 1f;
    [Tooltip("Duree du gresillement, en secondes (min / max, tire au hasard pour chaque allumage).")]
    [SerializeField] private Vector2 flickerDuration = new Vector2(0.3f, 0.8f);
    [Tooltip("Duree d'un flash allume, en secondes (min / max).")]
    [SerializeField] private Vector2 flickerOnTime = new Vector2(0.02f, 0.12f);
    [Tooltip("Duree d'une coupure, en secondes (min / max).")]
    [SerializeField] private Vector2 flickerOffTime = new Vector2(0.03f, 0.15f);

    private class Target
    {
        public GameObject go;
        public Transform tr;
        public Vector3 baseScale;
        public Renderer[] renderers;
        public Light[] lights;
        public bool on;
        public float progress;      // avancement du fondu, 0 = eteint, 1 = allume
        public bool flickering;
        public bool flickerVisible;
        public float flickerEnd;
        public float nextToggle;
    }

    private readonly List<Target> activeTargets = new List<Target>();
    private Transform[] players = new Transform[0];
    private float nextCheck;
    private float nextPlayerSearch;

    void Awake()
    {
        foreach (GameObject go in CollectTargets())
        {
            Target t = new Target { go = go, tr = go.transform, baseScale = go.transform.localScale };
            // Seuls les renderers et lumieres allumes au depart participent au gresillement.
            List<Renderer> rs = new List<Renderer>();
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) if (r.enabled) rs.Add(r);
            List<Light> ls = new List<Light>();
            foreach (Light l in go.GetComponentsInChildren<Light>(true)) if (l.enabled) ls.Add(l);
            t.renderers = rs.ToArray();
            t.lights = ls.ToArray();
            go.SetActive(false);
            activeTargets.Add(t);
        }
    }

    List<GameObject> CollectTargets()
    {
        List<GameObject> list = new List<GameObject>();
        if (targets.Count > 0)
        {
            foreach (GameObject t in targets)
                if (t != null && t != gameObject) list.Add(t);
        }
        else
        {
            foreach (ProximityTarget m in FindObjectsByType<ProximityTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (m.gameObject != gameObject) list.Add(m.gameObject);
        }
        return list;
    }

    void Update()
    {
        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + checkInterval;
            CheckDistances();
        }

        foreach (Target t in activeTargets)
        {
            if (t.go == null) continue;
            Animate(t);
        }
    }

    void CheckDistances()
    {
        if (!HasPlayers()) return;

        float onSqr = activationDistance * activationDistance;
        float off = activationDistance + hysteresis;
        float offSqr = off * off;

        foreach (Target t in activeTargets)
        {
            if (t.go == null) continue;
            if (t.on && mode == Mode.ResteAllume) continue;

            float d = NearestPlayerSqrDistance(t.tr.position);
            bool wanted = t.on ? d <= offSqr : d <= onSqr;
            if (wanted != t.on) SetOn(t, wanted);
        }
    }

    void SetOn(Target t, bool on)
    {
        t.on = on;
        if (on)
        {
            bool fromOff = !t.go.activeSelf;
            t.go.SetActive(true);
            if (fromOff)
            {
                t.progress = 0f;
                ApplyScale(t);
                if (flicker && Random.value < flickerChance) StartFlicker(t);
            }
        }
        else if (t.flickering)
        {
            t.flickering = false;
            SetVisible(t, true);
        }
    }

    void Animate(Target t)
    {
        if (!t.go.activeSelf) return;

        float goal = t.on ? 1f : 0f;
        if (t.progress != goal)
        {
            t.progress = fadeDuration > 0f ? Mathf.MoveTowards(t.progress, goal, Time.deltaTime / fadeDuration) : goal;
            if (!t.on && t.progress <= 0f)
            {
                t.tr.localScale = t.baseScale;
                t.go.SetActive(false);
                return;
            }
            ApplyScale(t);
        }

        if (t.flickering)
        {
            if (Time.time >= t.flickerEnd)
            {
                t.flickering = false;
                SetVisible(t, true);
            }
            else if (Time.time >= t.nextToggle)
            {
                t.flickerVisible = !t.flickerVisible;
                SetVisible(t, t.flickerVisible);
                t.nextToggle = Time.time + RandomIn(t.flickerVisible ? flickerOnTime : flickerOffTime);
            }
        }
    }

    void ApplyScale(Target t)
    {
        float k = fadeDuration > 0f ? fadeCurve.Evaluate(t.progress) : (t.progress > 0f ? 1f : 0f);
        t.tr.localScale = t.baseScale * k;
    }

    void StartFlicker(Target t)
    {
        t.flickering = true;
        t.flickerVisible = true;
        t.flickerEnd = Time.time + RandomIn(flickerDuration);
        t.nextToggle = Time.time + RandomIn(flickerOnTime);
        SetVisible(t, true);
    }

    static void SetVisible(Target t, bool visible)
    {
        foreach (Renderer r in t.renderers) if (r != null) r.enabled = visible;
        foreach (Light l in t.lights) if (l != null) l.enabled = visible;
    }

    static float RandomIn(Vector2 range)
    {
        return Random.Range(Mathf.Max(0f, Mathf.Min(range.x, range.y)), Mathf.Max(0f, Mathf.Max(range.x, range.y)));
    }

    // Joueurs trouves au lancement ; recherche relancee (au plus une fois par seconde) si l'un a disparu.
    bool HasPlayers()
    {
        bool missing = players.Length == 0;
        foreach (Transform p in players)
            if (p == null) missing = true;

        if (missing && Time.time >= nextPlayerSearch)
        {
            nextPlayerSearch = Time.time + 1f;
            PlayerPhysicsMovement[] found = FindObjectsOfType<PlayerPhysicsMovement>();
            players = new Transform[found.Length];
            for (int i = 0; i < found.Length; i++) players[i] = found[i].transform;
            missing = players.Length == 0;
        }
        return !missing;
    }

    float NearestPlayerSqrDistance(Vector3 from)
    {
        float best = float.MaxValue;
        foreach (Transform p in players)
        {
            if (p == null) continue;
            Vector3 d = p.position - from;
            if (ignoreHeight) d.y = 0f;
            best = Mathf.Min(best, d.sqrMagnitude);
        }
        return best;
    }

#if UNITY_EDITOR
    // Rayon d'activation autour de chaque cible.
    void OnDrawGizmosSelected()
    {
        List<GameObject> list = CollectTargets();

        UnityEditor.Handles.color = new Color(1f, 0.8f, 0.2f, 0.8f);
        Gizmos.color = UnityEditor.Handles.color;
        foreach (GameObject t in list)
        {
            if (t == null) continue;
            if (ignoreHeight) UnityEditor.Handles.DrawWireDisc(t.transform.position, Vector3.up, activationDistance);
            else Gizmos.DrawWireSphere(t.transform.position, activationDistance);
        }
    }
#endif
}
