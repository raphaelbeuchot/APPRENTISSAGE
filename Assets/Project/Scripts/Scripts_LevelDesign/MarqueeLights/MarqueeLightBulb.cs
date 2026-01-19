using UnityEngine;

public class MarqueeLightBulb : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MeshRenderer bulbRenderer;

    [Header("Materials")]
    [SerializeField] private Material redLightMaterial;
    [SerializeField] private Material alertMaterial;
    [SerializeField] private Material greenLightMaterial;
    [SerializeField] private Material lightOffMaterial;

    [Header("Emission Settings - RedLight")]
    [SerializeField] private Color redLightColor = Color.red;
    [SerializeField] private float redEmissionIntensity = 2f;

    [Header("Emission Settings - Alert")]
    [SerializeField] private Color alertColor = Color.yellow;
    [SerializeField] private float alertEmissionIntensity = 3f;

    [Header("Emission Settings - GreenLight")]
    [SerializeField] private Color greenLightColor = Color.green;
    [SerializeField] private float greenEmissionIntensity = 1f;

    private Material currentMaterialInstance;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private Color currentBaseEmissionColor;

    void Awake()
    {
        if (bulbRenderer == null)
            bulbRenderer = GetComponentInChildren<MeshRenderer>();

        TurnOff();
    }

    public void SetRedLightMode()
    {
        if (bulbRenderer != null && redLightMaterial != null)
        {
            currentMaterialInstance = new Material(redLightMaterial);
            bulbRenderer.material = currentMaterialInstance;
            currentBaseEmissionColor = redLightColor;
            SetEmissionIntensity(redEmissionIntensity);
        }
    }

    public void SetAlertMode()
    {
        if (bulbRenderer != null && alertMaterial != null)
        {
            currentMaterialInstance = new Material(alertMaterial);
            bulbRenderer.material = currentMaterialInstance;
            currentBaseEmissionColor = alertColor;
            SetEmissionIntensity(alertEmissionIntensity);
        }
    }

    public void SetGreenLightMode()
    {
        if (bulbRenderer != null && greenLightMaterial != null)
        {
            currentMaterialInstance = new Material(greenLightMaterial);
            bulbRenderer.material = currentMaterialInstance;
            currentBaseEmissionColor = greenLightColor;
            SetEmissionIntensity(greenEmissionIntensity);
        }
    }

    public void TurnOff()
    {
        if (bulbRenderer != null && lightOffMaterial != null)
        {
            bulbRenderer.material = lightOffMaterial;
        }
    }

    public void SetEmissionIntensity(float intensity)
    {
        if (currentMaterialInstance != null)
        {
            Color emission = currentBaseEmissionColor * intensity;
            currentMaterialInstance.SetColor(EmissionColor, emission);
        }
    }

    // Getters
    public Color GetRedLightColor() => redLightColor;
    public Color GetGreenLightColor() => greenLightColor;
    public float GetRedEmissionIntensity() => redEmissionIntensity;
    public float GetGreenEmissionIntensity() => greenEmissionIntensity;
    public float GetAlertEmissionIntensity() => alertEmissionIntensity;
}