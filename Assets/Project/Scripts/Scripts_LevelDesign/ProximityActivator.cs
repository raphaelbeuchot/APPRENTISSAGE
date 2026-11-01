using System.Collections.Generic;
using UnityEngine;

// Active des GO quand le joueur s'en approche. Chaque cible a sa propre distance au joueur : une guirlande s'allume
// ampoule par ampoule a son passage. Les cibles sont desactivees au lancement.
// A poser sur un parent des cibles (un GO desactive n'execute plus ses scripts : il ne pourrait pas se rallumer).
// Reste allume : une fois activee, une cible le reste.
// Suit la distance : la cible se desactive quand le joueur s'eloigne au-dela de distance + marge (la marge evite
// le clignotement quand le joueur reste pile a la limite).
public class ProximityActivator : MonoBehaviour
{
    public enum Mode { ResteAllume, SuitLaDistance }

    [Header("Cibles")]
    [Tooltip("GO a activer. Vide = tous les enfants directs.")]
    [SerializeField] private List<GameObject> targets = new List<GameObject>();

    [Header("Declenchement")]
    [SerializeField] private Mode mode = Mode.ResteAllume;
    [Tooltip("Distance au joueur (mesuree depuis chaque cible) en dessous de laquelle la cible s'active.")]
    [Min(0f)] [SerializeField] private float activationDistance = 5f;
    [Tooltip("Suit la distance : la cible se desactive au-dela de distance + marge.")]
    [Min(0f)] [SerializeField] private float hysteresis = 0.5f;
    [Tooltip("Coche : distance mesuree a plat (X / Z), la hauteur de la cible ne compte pas (ex. enseigne en haut d'un mur).")]
    [SerializeField] private bool ignoreHeight = true;
    [Tooltip("Intervalle entre deux verifications, en secondes. 0 = a chaque frame.")]
    [Min(0f)] [SerializeField] private float checkInterval = 0.1f;

    private readonly List<GameObject> activeTargets = new List<GameObject>();
    private bool[] isOn;
    private Transform[] players = new Transform[0];
    private float nextCheck;
    private float nextPlayerSearch;

    void Awake()
    {
        if (targets.Count > 0)
        {
            foreach (GameObject t in targets)
                if (t != null && t != gameObject) activeTargets.Add(t);
        }
        else
        {
            foreach (Transform child in transform) activeTargets.Add(child.gameObject);
        }

        isOn = new bool[activeTargets.Count];
        foreach (GameObject t in activeTargets) t.SetActive(false);
    }

    void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + checkInterval;

        if (!HasPlayers()) return;

        float onSqr = activationDistance * activationDistance;
        float off = activationDistance + hysteresis;
        float offSqr = off * off;

        for (int i = 0; i < activeTargets.Count; i++)
        {
            GameObject t = activeTargets[i];
            if (t == null) continue;
            if (isOn[i] && mode == Mode.ResteAllume) continue;

            float d = NearestPlayerSqrDistance(t.transform.position);
            bool wanted = isOn[i] ? d <= offSqr : d <= onSqr;
            if (wanted == isOn[i]) continue;

            isOn[i] = wanted;
            t.SetActive(wanted);
        }
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
        List<GameObject> list = new List<GameObject>();
        if (targets.Count > 0) list.AddRange(targets);
        else foreach (Transform child in transform) list.Add(child.gameObject);

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
