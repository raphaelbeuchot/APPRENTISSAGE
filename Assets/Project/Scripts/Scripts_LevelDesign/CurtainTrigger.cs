using UnityEngine;

/// <summary>
/// Trigger qui ouvre un SlidingCurtain pose a distance (quelques metres plus loin).
/// Un trigger = un rideau. Se declenche une seule fois, a l'entree du joueur (tag Player).
///
/// Setup : ce script + un Collider en Is Trigger sur un GameObject a part, champ Curtain rempli.
/// Selectionne, il trace une ligne vers son rideau dans la vue Scene.
/// </summary>
public class CurtainTrigger : MonoBehaviour
{
    [SerializeField] private SlidingCurtain curtain;

    private bool triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        triggered = true;
        if (curtain != null)
            curtain.Open();
        else
            Debug.LogWarning("[CurtainTrigger] Pas de rideau assigne : " + name, this);
    }

    private void OnDrawGizmosSelected()
    {
        if (curtain == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, curtain.transform.position);
        Gizmos.DrawWireSphere(curtain.transform.position, 0.3f);
    }
}
