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

        // Si damageController existe, lui deleguer la gestion
        if (damageController != null)
        {
            Debug.Log("[PitFloor DEBUG] Calling OnEntityHitFloor");
            damageController.OnEntityHitFloor(interactable, 0f);
        }
        else
        {
            Debug.LogError("[PitFloor ERROR] NO DAMAGECONTROLLER FOUND!");
        }

        // NOUVEAU : Switch vers PitMode pour les zombies
        EnemyAI_AStar enemyAI = collision.gameObject.GetComponent<EnemyAI_AStar>();
        if (enemyAI != null)
        {
            Debug.Log($"[PitFloor] {collision.gameObject.name} switching to PitMode");

            // Desactiver AI normale
            enemyAI.enabled = false;

            // Desactiver Seeker
            Pathfinding.Seeker seeker = collision.gameObject.GetComponent<Pathfinding.Seeker>();
            if (seeker != null)
            {
                Destroy(seeker);
            }

            // DETRUIRE AIPath (au lieu de disable)
            Pathfinding.AIPath aiPath = collision.gameObject.GetComponent<Pathfinding.AIPath>();
            if (aiPath != null)
            {
                Destroy(aiPath);
                Debug.Log($"[PitFloor] Destroyed AIPath component");
            }

            // Activer/Creer PitMode
            EnemyAI_PitMode pitMode = collision.gameObject.GetComponent<EnemyAI_PitMode>();
            if (pitMode == null)
            {
                pitMode = collision.gameObject.AddComponent<EnemyAI_PitMode>();
            }
            pitMode.enabled = true;
        }



    }
}