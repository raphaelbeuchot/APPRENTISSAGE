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

            // Chercher dans la scene
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
    }


}