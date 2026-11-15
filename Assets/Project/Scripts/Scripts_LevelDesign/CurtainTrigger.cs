using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trigger qui ouvre un ou plusieurs SlidingCurtain poses a distance.
/// Se declenche une seule fois, a l'entree du joueur (tag Player) : Open() sur chaque rideau de la liste.
/// Cascade : plusieurs rideaux dans la liste, avec des Delay croissants sur les rideaux (0 ; 0,4 ; 0,8...).
///
/// Setup : ce script + un Collider en Is Trigger sur un GameObject a part, liste Curtains remplie.
/// Selectionne, il trace une ligne vers chacun de ses rideaux dans la vue Scene.
/// </summary>
public class CurtainTrigger : MonoBehaviour
{
    [SerializeField] private List<SlidingCurtain> curtains = new List<SlidingCurtain>();

    // Ancien champ (un trigger = un rideau) : repris dans la liste au chargement, puis vide.
    [SerializeField, HideInInspector] private SlidingCurtain curtain;

    private bool triggered = false;

    private void OnValidate()
    {
        MigrateOldField();
    }

    private void Awake()
    {
        MigrateOldField();
    }

    private void MigrateOldField()
    {
        if (curtain == null) return;
        if (curtains == null) curtains = new List<SlidingCurtain>();
        if (!curtains.Contains(curtain)) curtains.Insert(0, curtain);
        curtain = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        triggered = true;
        if (curtains == null || curtains.Count == 0)
        {
            Debug.LogWarning("[CurtainTrigger] Pas de rideau assigne : " + name, this);
            return;
        }

        foreach (SlidingCurtain c in curtains)
            if (c != null) c.Open();
    }

    private void OnDrawGizmosSelected()
    {
        if (curtains == null) return;
        Gizmos.color = Color.cyan;
        foreach (SlidingCurtain c in curtains)
        {
            if (c == null) continue;
            Gizmos.DrawLine(transform.position, c.transform.position);
            Gizmos.DrawWireSphere(c.transform.position, 0.3f);
        }
    }
}
