using UnityEngine;

public class BroomVisuals : MonoBehaviour
{
    [Header("Materials")]
    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material bonusMaterial;

    private Renderer broomRenderer;

    void Start()
    {
        broomRenderer = GetComponent<Renderer>();
        if (broomRenderer == null) return;

        bool hasBroomBonus = ModifierApplier.Instance != null
            && ModifierApplier.Instance.broomKnockbackMultiplier > 1f;

        if (hasBroomBonus && bonusMaterial != null)
            broomRenderer.material = bonusMaterial;
        else if (normalMaterial != null)
            broomRenderer.material = normalMaterial;
    }

}