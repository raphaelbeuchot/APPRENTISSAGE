using UnityEngine;

public class PitFloorCollisionDetector : MonoBehaviour
{

    private PitFillDamageController damageController;
    private PitZone pitZone;

    public void Initialize(PitZone zone, PitFillDamageController controller)
    {
        pitZone = zone;
        damageController = controller;
    }

    void OnCollisionEnter(Collision collision)
    {
        IPitInteractable interactable = collision.gameObject.GetComponent<IPitInteractable>();
        if (interactable == null) return;
        if (!interactable.CanTakePitDamage()) return;

        Debug.Log(string.Format("[PitFloor] {0} hit floor", collision.gameObject.name));

        // FALLBACK : Trouver le damageController si null
        if (damageController == null)
        {
            Debug.Log("[PitFloor DEBUG] damageController NULL, searching...");

            // Essayer dans le parent d'abord
            damageController = transform.GetComponentInParent<PitFillDamageController>();

            // Si toujours null, chercher partout
            if (damageController == null)
            {
                PitFillDamageController[] controllers = FindObjectsOfType<PitFillDamageController>();
                if (controllers.Length > 0)
                {
                    damageController = controllers[0];
                    Debug.Log("[PitFloor DEBUG] Found damageController via FindObjectsOfType!");
                }
            }
            else
            {
                Debug.Log("[PitFloor DEBUG] Found damageController in parent!");
            }
        }

        // Si toujours null après tous les fallbacks
        if (damageController == null)
        {
            Debug.LogError("[PitFloor ERROR] NO DAMAGECONTROLLER FOUND ANYWHERE!");
            return;  // IMPORTANT : Sortir pour éviter le crash
        }

        // Appeler le damageController (ligne manquante ?)
        float fallHeight = Mathf.Abs(pitZone != null ? pitZone.GetMaxDepth() : 0f);
        damageController.OnEntityHitFloor(interactable, fallHeight);

        EnemyAI_AStar enemyAI = collision.gameObject.GetComponent<EnemyAI_AStar>();
        if (enemyAI != null)
        {
            Debug.Log($"[PitFloor] {collision.gameObject.name} switching to PitMode");
            enemyAI.EnablePitMode();
            enemyAI.enabled = true;

            Rigidbody rbRestore = collision.gameObject.GetComponent<Rigidbody>();
            EnemyPitInteractable pitInt = collision.gameObject.GetComponent<EnemyPitInteractable>();
            if (rbRestore != null && pitInt != null)
            {
                rbRestore.linearDamping = 5f;
                Debug.Log($"[PitFloor] Damping restored to 5 after AIPath disabled");
            }
        }
    }



}
