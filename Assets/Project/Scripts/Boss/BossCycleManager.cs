using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cycle de la sentinelle pour le boss de Rush Hour IV.
/// Herite de SentinelCycleManager : pour le reste du jeu (joueur, ennemis, lasers, visuels),
/// c'est toujours un SentinelCycleManager. Seuls changent :
/// - les durees des feux : tirees dans la fourchette de la phase en cours (pas de raccourcissement par la pression) ;
/// - la pression : celle de la phase, sans distance ni ennemis (pitch musique, pitch alerte, jauge).
/// Regle : ne pas declarer OnEnable ni Update ici (Unity n'appellerait plus ceux de la base).
/// </summary>
public class BossCycleManager : SentinelCycleManager
{
    [System.Serializable]
    public class BossPhaseCycle
    {
        public string label = "Phase";
        [Min(0.1f)] public float greenMin = 5f;
        [Min(0.1f)] public float greenMax = 8f;
        [Min(0.1f)] public float redMin = 4f;
        [Min(0.1f)] public float redMax = 6f;
        [Tooltip("0 = calme, 1 = max. Regle le pitch de la musique, celui de l'alerte (donc sa duree) et la jauge.")]
        [Range(0f, 1f)] public float pressure = 0f;
    }

    [Header("Boss : phases")]
    [Tooltip("Une entree par phase. Valeurs de depart a equilibrer en jouant.")]
    [SerializeField] private List<BossPhaseCycle> phases = new List<BossPhaseCycle>
    {
        new BossPhaseCycle { label = "Phase 1", greenMin = 5f, greenMax = 8f, redMin = 4f,   redMax = 6f, pressure = 0f },
        new BossPhaseCycle { label = "Phase 2", greenMin = 4f, greenMax = 6f, redMin = 3.5f, redMax = 5f, pressure = 0.33f },
        new BossPhaseCycle { label = "Phase 3", greenMin = 3f, greenMax = 5f, redMin = 3f,   redMax = 4f, pressure = 0.66f },
        new BossPhaseCycle { label = "Phase 4", greenMin = 2f, greenMax = 4f, redMin = 2f,   redMax = 3f, pressure = 1f },
    };

    [Tooltip("Phase en cours (0 = phase 1). Visible pour le debug.")]
    [SerializeField] private int currentPhaseIndex = 0;

    public int CurrentPhaseIndex => currentPhaseIndex;
    public int PhaseCount => phases.Count;

    private BossPhaseCycle CurrentPhase
    {
        get
        {
            if (phases == null || phases.Count == 0) return null;
            return phases[Mathf.Clamp(currentPhaseIndex, 0, phases.Count - 1)];
        }
    }

    /// <summary>
    /// Passe a la phase suivante. Prise en compte au prochain feu : le feu en cours garde sa duree.
    /// </summary>
    [ContextMenu("Phase suivante")]
    public void NextPhase()
    {
        if (phases == null || phases.Count == 0) return;
        if (currentPhaseIndex >= phases.Count - 1)
        {
            Debug.Log("[BOSS CYCLE] Deja a la derniere phase");
            return;
        }
        currentPhaseIndex++;
        Debug.Log($"[BOSS CYCLE] -> {CurrentPhase.label} (pression {CurrentPhase.pressure:F2}), applique au prochain feu");
    }

    protected override float GetPressureFactor()
    {
        BossPhaseCycle phase = CurrentPhase;
        return phase != null ? phase.pressure : base.GetPressureFactor();
    }

    protected override float GetDynamicGreenLightDuration()
    {
        BossPhaseCycle phase = CurrentPhase;
        if (phase == null) return base.GetDynamicGreenLightDuration();

        float duration = Random.Range(phase.greenMin, Mathf.Max(phase.greenMin, phase.greenMax));
        Debug.Log($"[BOSS CYCLE] {phase.label} - GreenLight {duration:F1}s");
        return duration;
    }

    protected override float GetDynamicRedLightDuration()
    {
        BossPhaseCycle phase = CurrentPhase;
        if (phase == null) return base.GetDynamicRedLightDuration();

        float duration = Random.Range(phase.redMin, Mathf.Max(phase.redMin, phase.redMax));
        Debug.Log($"[BOSS CYCLE] {phase.label} - RedLight {duration:F1}s");
        return duration;
    }
}
