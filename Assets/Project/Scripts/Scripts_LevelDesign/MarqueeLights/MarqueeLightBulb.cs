using UnityEngine;

public class MarqueeLightBulb : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MeshRenderer bulbRenderer;
    [SerializeField] private Light pointLight;

    [Header("Materials")]
    [SerializeField] private Material redLightMaterial;
    [SerializeField] private Material alertMaterial;
    [SerializeField] private Material greenLightMaterial;
    [SerializeField] private Material lightOffMaterial;

    [Header("Light Settings - RedLight")]
    [SerializeField] private float redLightIntensity = 2f;
    [SerializeField] private Color redLightColor = Color.red;
    [SerializeField] private float redEmissionIntensity = 2f;

    [Header("Light Settings - Alert")]
    [SerializeField] private float alertIntensity = 3f;
    [SerializeField] private Color alertColor = Color.yellow;
    [SerializeField] private float alertEmissionIntensity = 3f;

    [Header("Light Settings - GreenLight")]
    [SerializeField] private float greenLightIntensity = 1f;
    [SerializeField] private Color greenLightColor = Color.green;
    [SerializeField] private float greenEmissionIntensity = 1f;

    private Material currentMaterialInstance;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private Color currentBaseEmissionColor;

    void Awake()
    {
        if (bulbRenderer == null)
            bulbRenderer = GetComponentInChildren<MeshRenderer>();

        if (pointLight == null)
            pointLight = GetComponentInChildren<Light>();

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

        if (pointLight != null)
        {
            pointLight.intensity = redLightIntensity;
            pointLight.color = redLightColor;
            pointLight.enabled = true;
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

        if (pointLight != null)
        {
            pointLight.intensity = alertIntensity;
            pointLight.color = alertColor;
            pointLight.enabled = true;
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

        if (pointLight != null)
        {
            pointLight.intensity = greenLightIntensity;
            pointLight.color = greenLightColor;
            pointLight.enabled = true;
        }
    }

    public void TurnOff()
    {
        if (bulbRenderer != null && lightOffMaterial != null)
        {
            bulbRenderer.material = lightOffMaterial;
        }

        if (pointLight != null)
        {
            pointLight.enabled = false;
        }
    }

    // Pour le fade
    public void SetLightIntensity(float intensity)
    {
        if (pointLight != null)
        {
            pointLight.intensity = intensity;
        }
    }

    public void SetLightColor(Color color)
    {
        if (pointLight != null)
        {
            pointLight.color = color;
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
    public float GetRedLightIntensity() => redLightIntensity;
    public float GetGreenLightIntensity() => greenLightIntensity;
    public Color GetRedLightColor() => redLightColor;
    public Color GetGreenLightColor() => greenLightColor;
    public float GetRedEmissionIntensity() => redEmissionIntensity;
    public float GetGreenEmissionIntensity() => greenEmissionIntensity;
}