using UnityEngine;

[RequireComponent(typeof(ZombieHealth))]
public class DamageVisualFeedback : MonoBehaviour
{
    [Header("Color Progression")]
    [SerializeField] private Color healthyColor = Color.white;
    [SerializeField] private Color minorDamageColor = Color.yellow;
    [SerializeField] private Color moderateDamageColor = new Color(1f, 0.5f, 0f); // Orange
    [SerializeField] private Color severeDamageColor = Color.red;
    [SerializeField] private Color criticalColor = Color.black;

    [Header("Mesh References")]
    [SerializeField] private Renderer[] bodyRenderers; // Tous les mesh renderers du corps

    private ZombieHealth health;
    private Color currentColor;

    void Start()
    {
        health = GetComponent<ZombieHealth>();

        // Si pas de renderers assignés, les trouver automatiquement
        if (bodyRenderers == null || bodyRenderers.Length == 0)
        {
            bodyRenderers = GetComponentsInChildren<Renderer>();
        }

        // Sauvegarder la couleur initiale si pas définie
        if (bodyRenderers.Length > 0 && healthyColor == Color.white)
        {
            Material mat = bodyRenderers[0].material;
            if (mat.HasProperty("_Color"))
            {
                healthyColor = mat.color;
            }
        }

        // S'abonner aux événements de dégâts
        if (health != null)
        {
            health.OnLimbLost += OnLimbLost;
            health.OnHeadshot += OnHeadshot;
        }

        UpdateColor(0);
    }

    void OnDestroy()
    {
        if (health != null)
        {
            health.OnLimbLost -= OnLimbLost;
            health.OnHeadshot -= OnHeadshot;
        }
    }

    void OnLimbLost(string limbName)
    {
        // Compter les membres perdus
        int damageLevel = 0;
        if (!health.HasLeftArm()) damageLevel++;
        if (!health.HasRightArm()) damageLevel++;
        if (!health.HasLeftLeg()) damageLevel++;
        if (!health.HasRightLeg()) damageLevel++;

        UpdateColor(damageLevel);
    }

    void OnHeadshot()
    {
        // Headshot = noir immédiat
        UpdateColor(4);
    }

    void UpdateColor(int damageLevel)
    {
        Color targetColor;

        switch (damageLevel)
        {
            case 0:
                targetColor = healthyColor;
                break;
            case 1:
                targetColor = minorDamageColor; // Jaune
                break;
            case 2:
                targetColor = moderateDamageColor; // Orange
                break;
            case 3:
                targetColor = severeDamageColor; // Rouge
                break;
            case 4:
            default:
                targetColor = criticalColor; // Noir
                break;
        }

        currentColor = targetColor;
        ApplyColorToRenderers(targetColor);

        Debug.Log($"{gameObject.name} damage level: {damageLevel} -> Color: {targetColor}");
    }

    void ApplyColorToRenderers(Color color)
    {
        foreach (Renderer rend in bodyRenderers)
        {
            if (rend != null && rend.material != null)
            {
                // Pour les materials standards
                if (rend.material.HasProperty("_Color"))
                {
                    rend.material.color = color;
                }

                // Pour les materials URP/HDRP avec BaseColor
                if (rend.material.HasProperty("_BaseColor"))
                {
                    rend.material.SetColor("_BaseColor", color);
                }
            }
        }
    }
}