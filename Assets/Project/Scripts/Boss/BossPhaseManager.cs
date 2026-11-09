using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Deroule des phases du boss de Rush Hour IV.
/// Chaque phase : une liste de pupitres (nombre libre, ranges n'importe ou dans la scene).
/// Quand tous sont actives, la phase est finie : effets de fin, puis debut de la phase suivante
/// (ses reglages de feux sont envoyes au BossCycleManager).
/// Pupitres et zombies des phases suivantes sont desactives au lancement de la scene ; ils descendent
/// pendant la transition (BringInCurrentPhasePupitres / BringInCurrentPhaseZombies, branches dans le
/// flow depuis les evenements de la BossTransition). Ceux de la phase 1 sont la des le lancement.
/// Le nombre de phases = le nombre d elements de la liste, rien d autre a aligner.
/// </summary>
public class BossPhaseManager : MonoBehaviour
{
    [System.Serializable]
    public class PhaseEffects
    {
        [Tooltip("Debut de la phase (mise en place, descente des pupitres, texte...).")]
        public UnityEvent onPhaseStart;
        [Tooltip("Fin de la phase (explosion d'un morceau de sentinelle, cercle abattu...).")]
        public UnityEvent onPhaseComplete;
    }

    [System.Serializable]
    public class BossPhase
    {
        [Tooltip("Nom affiche dans la liste et la Console. Laisse en \"Phase N\", il se renumerote tout seul.")]
        public string label = "Phase";
        [Tooltip("Pupitres a activer pour finir la phase. Liste vide : la phase ne se termine que par \"Terminer la phase en cours\".")]
        public List<BossPupitre> pupitres = new List<BossPupitre>();
        [Tooltip("Ennemis de la phase, poses ou ils doivent arriver. Inutile en phase 1 : tout zombie range " +
                 "hors des phases suivantes est present au lancement. Phases suivantes : desactives au lancement, " +
                 "ils descendent avec \"Faire entrer les zombies de la phase\".")]
        public List<GameObject> zombies = new List<GameObject>();
        [Tooltip("Feux de la sentinelle pendant cette phase (durees et pression).")]
        public BossCycleManager.PhaseCycle cycle = new BossCycleManager.PhaseCycle();
        public PhaseEffects effets = new PhaseEffects();
    }

    [Header("Cycle")]
    [SerializeField] private BossCycleManager cycleManager;
    [Tooltip("Entre-phases (joueur fige, ennemis enleves) : lancee au dernier pupitre de chaque phase. Vide = rien.")]
    [SerializeField] private BossTransition transition;

    [Header("Phases")]
    [SerializeField] private List<BossPhase> phases = new List<BossPhase>();

    [Header("Fin du boss")]
    [Tooltip("Toutes les phases terminees.")]
    [SerializeField] private UnityEvent onAllPhasesComplete;

    [Header("Debug")]
    [Tooltip("Phase en cours (0 = phase 1, -1 = pas commence).")]
    [SerializeField] private int currentPhaseIndex = -1;

    public int CurrentPhaseIndex => currentPhaseIndex;

    private static readonly Regex AutoLabel = new Regex(@"^Phase( \d+)?$");

    private void OnValidate()
    {
        if (phases == null) return;
        for (int i = 0; i < phases.Count; i++)
        {
            BossPhase phase = phases[i];
            if (phase == null) continue;
            if (string.IsNullOrEmpty(phase.label) || AutoLabel.IsMatch(phase.label))
                phase.label = "Phase " + (i + 1);
        }
    }

    /// <summary>
    /// Remplit le bloc Cycle de chaque phase avec les valeurs de depart (celles de l'ancienne liste du
    /// BossCycleManager). Au-dela de 4 phases, les suivantes reprennent la phase 4. A equilibrer ensuite.
    /// </summary>
    [ContextMenu("Remplir les feux avec les valeurs de depart")]
    private void FillDefaultCycles()
    {
        float[,] values =
        {
            // greenMin, greenMax, redMin, redMax, pressure
            { 5f, 8f, 4f,   6f, 0f },
            { 4f, 6f, 3.5f, 5f, 0.33f },
            { 3f, 5f, 3f,   4f, 0.66f },
            { 2f, 4f, 2f,   3f, 1f },
        };

#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Remplir les feux");
#endif
        for (int i = 0; i < phases.Count; i++)
        {
            int row = Mathf.Min(i, values.GetLength(0) - 1);
            BossCycleManager.PhaseCycle c = phases[i].cycle ?? new BossCycleManager.PhaseCycle();
            c.greenMin = values[row, 0];
            c.greenMax = values[row, 1];
            c.redMin = values[row, 2];
            c.redMax = values[row, 3];
            c.pressure = values[row, 4];
            phases[i].cycle = c;
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private void Awake()
    {
        for (int i = 0; i < phases.Count; i++)
        {
            BossPhase phase = phases[i];
            foreach (BossPupitre pupitre in phase.pupitres)
            {
                if (pupitre == null) continue;
                pupitre.Activated += OnPupitreActivated;
                pupitre.gameObject.SetActive(false);
            }

            // Zombies des phases suivantes : hors scene jusqu'a leur arrivee. Desactives des Awake, avant
            // les Start qui recensent les ennemis (CleaningBonusManager, GameManager) : ils n'y comptent pas.
            if (i > 0)
                foreach (GameObject zombie in phase.zombies)
                    if (zombie != null) zombie.SetActive(false);
        }

        if (transition != null) transition.Ended += OnTransitionEnded;
    }

    private void Start()
    {
        if (cycleManager == null)
            Debug.LogWarning("[BOSS PHASES] Pas de BossCycleManager : les feux ne suivront pas les phases");

        if (phases.Count > 0)
            StartPhase(0);
    }

    private void OnDestroy()
    {
        foreach (BossPhase phase in phases)
            foreach (BossPupitre pupitre in phase.pupitres)
                if (pupitre != null) pupitre.Activated -= OnPupitreActivated;

        if (transition != null) transition.Ended -= OnTransitionEnded;
    }

    private Coroutine resumeCoroutine;
    // Avec une BossTransition : le cycle attend la fin de la transition (joueur relache) pour reprendre
    private bool waitingForTransitionEnd = false;

    private IEnumerator ResumeAfterDelay(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        resumeCoroutine = null;
        cycleManager.ResumeCycle();
    }

    private void StartResume(float delay)
    {
        if (cycleManager == null) return;
        if (resumeCoroutine != null) StopCoroutine(resumeCoroutine);
        resumeCoroutine = StartCoroutine(ResumeAfterDelay(delay));
    }

    private void OnTransitionEnded()
    {
        if (!waitingForTransitionEnd) return;
        waitingForTransitionEnd = false;

        float delay = CurrentResumeDelay();
        Debug.Log($"[BOSS PHASES] Fin de transition : reprise du cycle dans {delay:F1}s");
        StartResume(delay);
    }

    private float CurrentResumeDelay()
    {
        if (currentPhaseIndex < 0 || currentPhaseIndex >= phases.Count) return 0f;
        BossCycleManager.PhaseCycle c = phases[currentPhaseIndex].cycle;
        return c != null ? c.resumeDelay : 0f;
    }

    private void StartPhase(int index)
    {
        currentPhaseIndex = index;
        BossPhase phase = phases[index];

        // Phase 1 (ou pas de BossTransition) : pupitres la tout de suite. Phases suivantes : ils attendent
        // "Faire entrer les pupitres de la phase" (descente)
        if (index == 0 || transition == null)
            foreach (BossPupitre pupitre in phase.pupitres)
                if (pupitre != null) pupitre.gameObject.SetActive(true);

        if (cycleManager != null) cycleManager.SetPhaseCycle(phase.cycle, phase.label);

        Debug.Log($"[BOSS PHASES] Debut {phase.label} ({phase.pupitres.Count} pupitres)");
        phase.effets.onPhaseStart?.Invoke();
    }

    private void OnPupitreActivated(BossPupitre pupitre)
    {
        if (currentPhaseIndex < 0 || currentPhaseIndex >= phases.Count) return;
        BossPhase phase = phases[currentPhaseIndex];

        if (!phase.pupitres.Contains(pupitre))
        {
            Debug.LogWarning($"[BOSS PHASES] {pupitre.name} active hors de sa phase : ignore");
            return;
        }

        int done = 0;
        foreach (BossPupitre p in phase.pupitres)
            if (p == null || p.IsActivated) done++;

        Debug.Log($"[BOSS PHASES] {phase.label} : {done}/{phase.pupitres.Count}");

        if (done >= phase.pupitres.Count)
            CompleteCurrentPhase();
    }

    /// <summary>
    /// Fait descendre les zombies de la phase en cours (par la BossTransition). A brancher dans le flow,
    /// ex. sur BossTransition.onEnemiesRemoved : la phase suivante a deja commence a ce moment-la.
    /// </summary>
    [ContextMenu("Faire entrer les zombies de la phase")]
    public void BringInCurrentPhaseZombies()
    {
        if (currentPhaseIndex < 0 || currentPhaseIndex >= phases.Count) return;
        if (transition == null)
        {
            Debug.LogWarning("[BOSS PHASES] Pas de BossTransition : les zombies ne peuvent pas descendre");
            return;
        }

        BossPhase phase = phases[currentPhaseIndex];
        Debug.Log($"[BOSS PHASES] {phase.label} : entree de {phase.zombies.Count} zombie(s)");
        transition.BringInEnemies(phase.zombies);
    }

    /// <summary>
    /// Fait descendre les pupitres de la phase en cours (par la BossTransition). A brancher dans le flow,
    /// ex. sur BossTransition.onEnemiesRemoved. Les pupitres enfonces des phases finies restent en place.
    /// </summary>
    [ContextMenu("Faire entrer les pupitres de la phase")]
    public void BringInCurrentPhasePupitres()
    {
        if (currentPhaseIndex < 0 || currentPhaseIndex >= phases.Count) return;
        if (transition == null)
        {
            Debug.LogWarning("[BOSS PHASES] Pas de BossTransition : les pupitres ne peuvent pas descendre");
            return;
        }

        BossPhase phase = phases[currentPhaseIndex];
        List<GameObject> objects = new List<GameObject>();
        foreach (BossPupitre pupitre in phase.pupitres)
            if (pupitre != null) objects.Add(pupitre.gameObject);

        Debug.Log($"[BOSS PHASES] {phase.label} : entree de {objects.Count} pupitre(s)");
        transition.BringInPupitres(objects);
    }

    /// <summary>Termine la phase en cours (appele quand tous ses pupitres sont actives, ou a la main).</summary>
    [ContextMenu("Terminer la phase en cours")]
    public void CompleteCurrentPhase()
    {
        if (currentPhaseIndex < 0 || currentPhaseIndex >= phases.Count) return;

        BossPhase phase = phases[currentPhaseIndex];
        Debug.Log($"[BOSS PHASES] Fin {phase.label}");

        // Le dernier pupitre coupe tout, quel que soit l'etat du cycle
        if (cycleManager != null) cycleManager.PauseCycle();
        if (transition != null) transition.Begin();

        phase.effets.onPhaseComplete?.Invoke();

        if (currentPhaseIndex < phases.Count - 1)
        {
            StartPhase(currentPhaseIndex + 1);

            if (transition != null)
            {
                // Le cycle reste arrete toute la transition ; le delai part du relachement du joueur
                waitingForTransitionEnd = true;
            }
            else
            {
                // Sans transition : reprise apres le delai, compte depuis le dernier pupitre (0 = pas de reprise auto)
                float delay = CurrentResumeDelay();
                if (delay > 0f) StartResume(delay);
            }
        }
        else
        {
            currentPhaseIndex = phases.Count; // plus de phase en cours
            Debug.Log("[BOSS PHASES] Toutes les phases terminees");
            onAllPhasesComplete?.Invoke();
        }
    }
}
