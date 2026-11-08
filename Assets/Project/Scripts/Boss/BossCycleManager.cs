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
        [Tooltip("Debut de cette phase apres la pause de fin de la precedente : le cycle reprend apres ce delai (s). " +
                 "0 = pas de reprise auto (ResumeCycle a appeler depuis un evenement, une Timeline...). Ignore pour la premiere phase.")]
        [Min(0f)] public float resumeDelay = 3f;
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

    private bool isPaused = false;
    private bool wasStartedBeforePause = false;

    public bool IsPaused => isPaused;

    /// <summary>
    /// Coupe tout, quel que soit l'etat (vert, alerte, rouge, release) : etat interne remis au vert,
    /// timer / alerte / sons arretes, visuels au vert, suivis de la sentinelle effaces, lumieres remises.
    /// La sentinelle ne scanne plus (GameManager ne la fait tourner que jeu lance + rouge).
    /// </summary>
    [ContextMenu("Pause du cycle")]
    public void PauseCycle()
    {
        if (isPaused) return;
        isPaused = true;
        wasStartedBeforePause = IsGameStarted();

        // Avant l'evenement de StopCycleForVictory, pour que ceux qui lisent l'etat voient deja le vert
        currentState = GameState.GreenLight;
        StopCycleForVictory();

        if (gameManager != null)
            gameManager.ResetAllTracking();

        if (sentinelCentralLight != null)
            sentinelCentralLight.TurnOff();
        if (playerSpotLight != null)
            playerSpotLight.enabled = false;
        if (lightsToDisableInRedLight != null)
            foreach (Light l in lightsToDisableInRedLight)
                if (l != null) l.enabled = true;

        Debug.Log("[BOSS CYCLE] Pause");
    }

    /// <summary>Relance un feu vert apres PauseCycle. Sans effet si le cycle n'est pas en pause.</summary>
    [ContextMenu("Reprendre le cycle")]
    public void ResumeCycle()
    {
        if (!isPaused) return;
        isPaused = false;

        if (!wasStartedBeforePause)
        {
            Debug.Log("[BOSS CYCLE] Reprise ignoree : le cycle n'avait pas encore demarre");
            return;
        }

        Debug.Log("[BOSS CYCLE] Reprise");
        base.StartGameCycle();
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
