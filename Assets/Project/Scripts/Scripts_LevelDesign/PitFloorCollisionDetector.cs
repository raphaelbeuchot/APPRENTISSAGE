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
            PitFillDamageController[] controllers = FindObjectsOfType<PitFillDamageController>();
            if (controllers.Length > 0)
            {
                damageController = controllers[0];
                Debug.Log("[PitFloor DEBUG] Found damageController!");
            }
        }

        if (damageController == null)
        {
            Debug.Log("[PitFloor DEBUG] damageController NULL, searching in parent...");
            // Chercher dans le PARENT (PitZone) au lieu de FindObjectsOfType
            damageController = transform.GetComponentInParent<PitFillDamageController>();
            if (damageController != null)
                Debug.Log("[PitFloor DEBUG] Found damageController in parent!");
        }
        else
        {
            Debug.LogError("[PitFloor ERROR] NO DAMAGECONTROLLER FOUND!");
        }

        EnemyAI_AStar enemyAI = collision.gameObject.GetComponent<EnemyAI_AStar>();
        if (enemyAI != null)
        {
            Debug.Log($"[PitFloor] {collision.gameObject.name} switching to PitMode");

            // Activer PitMode AVANT de toucher au Rigidbody
            enemyAI.EnablePitMode();
            enemyAI.enabled = true; // FORCER RALLUMAGE


            // Restaurer le damping maintenant que AIPath est desactive
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