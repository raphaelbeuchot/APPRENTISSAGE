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

        // Calcule la hauteur de chute
        float fallHeight = CalculateFallHeight(interactable);

        // Si pas de fill ou fill empty/spikes, applique degats via damageController
        if (damageController != null)
        {
            damageController.OnEntityHitFloor(interactable, fallHeight);
        }
        else
        {
            // Pas de fill : applique fall damage par defaut
            ApplyDefaultFallDamage(interactable, fallHeight);
        }

        Debug.Log($"[PitFloor] {collision.gameObject.name} hit floor from {fallHeight:F2}m");
    }

    private float CalculateFallHeight(IPitInteractable interactable)
    {
        if (pitZone == null) return 0f;

        // Position d'entree = position actuelle (approximation)
        // Dans un systeme plus avance, on trackrait la position d'entree reelle
        Vector3 currentPos = interactable.GetCharacterCenter();
        float groundLevel = 0f; // Niveau du sol principal
        float pitBottom = pitZone.GetMaxDepth();

        // Hauteur de chute = distance entre niveau sol et fond du pit
        float fallHeight = Mathf.Abs(pitBottom);

        return fallHeight;
    }

    private void ApplyDefaultFallDamage(IPitInteractable interactable, float fallHeight)
    {
        // Valeurs par defaut si pas de PitContentType
        float immunityThreshold = 3f;
        float damageMultiplier = 10f;

        if (fallHeight > immunityThreshold)
        {
            float damage = (fallHeight - immunityThreshold) * damageMultiplier;
            interactable.TakePitDamage(damage, PitDamageType.Fall);
        }
    }
}