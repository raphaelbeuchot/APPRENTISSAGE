using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Deroule des phases du boss de Rush Hour IV.
/// Chaque phase : une liste de pupitres (nombre libre, ranges n'importe ou dans la scene).
/// Quand tous sont actives, la phase est finie : effets de fin, puis BossCycleManager.NextPhase()
/// et debut de la phase suivante.
/// Version simple : au debut d'une phase, ses pupitres sont actives d'un coup (remplacant provisoire
/// de la descente des cintres, a brancher plus tard dans les effets de debut). Les pupitres des
/// phases suivantes sont desactives au lancement de la scene.
/// Le nombre de phases doit correspondre a celui du BossCycleManager.
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
        if (cycleManager != null && cycleManager.PhaseCount != phases.Count)
            Debug.LogWarning($"[BOSS PHASES] {phases.Count} phases ici, {cycleManager.PhaseCount} dans le BossCycleManager");

        if (phases.Count > 0)
            StartPhase(0);
    }

    private void OnDestroy()
    {
        foreach (BossPhase phase in phases)
            foreach (BossPupitre pupitre in phase.pupitres)
                if (pupitre != null) pupitre.Activated -= OnPupitreActivated;
    }

    private void StartPhase(int index)
    {
        currentPhaseIndex = index;
        BossPhase phase = phases[index];

        foreach (BossPupitre pupitre in phase.pupitres)
            if (pupitre != null) pupitre.gameObject.SetActive(true);

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
        phase.effets.onPhaseComplete?.Invoke();

        if (currentPhaseIndex < phases.Count - 1)
        {
            if (cycleManager != null) cycleManager.NextPhase();
            StartPhase(currentPhaseIndex + 1);
        }
        else
        {
            currentPhaseIndex = phases.Count; // plus de phase en cours
            Debug.Log("[BOSS PHASES] Toutes les phases terminees");
            onAllPhasesComplete?.Invoke();
        }
    }
}
