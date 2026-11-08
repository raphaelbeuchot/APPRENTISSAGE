using UnityEngine;

/// <summary>
/// Cycle de la sentinelle pour le boss de Rush Hour IV.
/// Herite de SentinelCycleManager : pour le reste du jeu (joueur, ennemis, lasers, visuels),
/// c'est toujours un SentinelCycleManager. Seuls changent, des qu'un reglage de phase a ete recu :
/// - les durees des feux : tirees dans la fourchette de la phase (pas de raccourcissement par la pression) ;
/// - la pression : celle de la phase, sans distance ni ennemis (pitch musique, pitch alerte, jauge).
/// Les reglages viennent du BossPhaseManager (bloc Cycle de chaque phase), via SetPhaseCycle.
/// Sans reglage recu : comportement normal de la sentinelle.
/// Regle : ne pas declarer OnEnable ni Update ici (Unity n'appellerait plus ceux de la base).
/// </summary>
public class BossCycleManager : SentinelCycleManager
{
    [System.Serializable]
    public class PhaseCycle
    {
        [Min(0.1f)] public float greenMin = 5f;
        [Min(0.1f)] public float greenMax = 8f;
        [Min(0.1f)] public float redMin = 4f;
        [Min(0.1f)] public float redMax = 6f;
        [Tooltip("0 = calme, 1 = max. Regle le pitch de la musique, celui de l'alerte (donc sa duree) et la jauge.")]
        [Range(0f, 1f)] public float pressure = 0f;
    }

    private PhaseCycle currentCycle;
    private string currentLabel = "";

    /// <summary>
    /// Reglages de la phase en cours. Pris en compte au prochain feu : le feu en cours garde sa duree.
    /// </summary>
    public void SetPhaseCycle(PhaseCycle cycle, string label)
    {
        currentCycle = cycle;
        currentLabel = label;
        if (cycle != null)
            Debug.Log($"[BOSS CYCLE] -> {label} (pression {cycle.pressure:F2}), applique au prochain feu");
    }

    protected override float GetPressureFactor()
    {
        return currentCycle != null ? currentCycle.pressure : base.GetPressureFactor();
    }

    protected override float GetDynamicGreenLightDuration()
    {
        if (currentCycle == null) return base.GetDynamicGreenLightDuration();

        float duration = Random.Range(currentCycle.greenMin, Mathf.Max(currentCycle.greenMin, currentCycle.greenMax));
        Debug.Log($"[BOSS CYCLE] {currentLabel} - GreenLight {duration:F1}s");
        return duration;
    }

    protected override float GetDynamicRedLightDuration()
    {
        if (currentCycle == null) return base.GetDynamicRedLightDuration();

        float duration = Random.Range(currentCycle.redMin, Mathf.Max(currentCycle.redMin, currentCycle.redMax));
        Debug.Log($"[BOSS CYCLE] {currentLabel} - RedLight {duration:F1}s");
        return duration;
    }
}
