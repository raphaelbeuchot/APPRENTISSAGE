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
/// Version simple : au debut d'une phase, ses pupitres sont actives d'un coup (remplacant provisoire
/// de la descente des cintres, a brancher plus tard dans les effets de debut). Les pupitres des
/// phases suivantes sont desactives au lancement de la scene.
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
        [Tooltip("Feux de la sentinelle pendant cette phase (durees et pression).")]
        public BossCycleManager.PhaseCycle cycle = new BossCycleManager.PhaseCycle();
        public PhaseEffects effets = new PhaseEffects();
    }

    [Header("Cycle")]
    [SerializeField] private BossCycleManager cycleManager;

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
        foreach (BossPhase phase in phases)
        {
            foreach (BossPupitre pupitre in phase.pupitres)
            {
                if (pupitre == null) continue;
                pupitre.Activated += OnPupitreActivated;
                pupitre.gameObject.SetActive(false);
            }
        }
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
    }

    private Coroutine resumeCoroutine;

    private IEnumerator ResumeAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        resumeCoroutine = null;
        cycleManager.ResumeCycle();
    }

    private void StartPhase(int index)
    {
        currentPhaseIndex = index;
        BossPhase phase = phases[index];

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

    /// <summary>Termine la phase en cours (appele quand tous ses pupitres sont actives, ou a la main).</summary>
    [ContextMenu("Terminer la phase en cours")]
    public void CompleteCurrentPhase()
    {
        if (currentPhaseIndex < 0 || currentPhaseIndex >= phases.Count) return;

        BossPhase phase = phases[currentPhaseIndex];
        Debug.Log($"[BOSS PHASES] Fin {phase.label}");

        // Le dernier pupitre coupe tout, quel que soit l'etat du cycle
        if (cycleManager != null) cycleManager.PauseCycle();

        phase.effets.onPhaseComplete?.Invoke();

        if (currentPhaseIndex < phases.Count - 1)
        {
            StartPhase(currentPhaseIndex + 1);

            float delay = phases[currentPhaseIndex].cycle != null ? phases[currentPhaseIndex].cycle.resumeDelay : 0f;
            if (cycleManager != null && delay > 0f)
            {
                if (resumeCoroutine != null) StopCoroutine(resumeCoroutine);
                resumeCoroutine = StartCoroutine(ResumeAfterDelay(delay));
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
