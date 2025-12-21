using UnityEngine;

public class MarqueeLightBulb : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Light bulbLight;
    [SerializeField] private MeshRenderer bulbRenderer;

    [Header("Settings")]
    [SerializeField] private float maxIntensity = 2f;
    [SerializeField] private Color lightColor = Color.white;

    private Material bulbMaterial;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private bool isOn = false;

    void Awake()
    {
        // Auto-detect si pas assigne
        if (bulbLight == null)
            bulbLight = GetComponentInChildren<Light>();

        if (bulbRenderer == null)
            bulbRenderer = GetComponentInChildren<MeshRenderer>();

        // Creer instance material pour modifier emission
        if (bulbRenderer != null)
        {
            bulbMaterial = bulbRenderer.material;
        }

        // Setup initial
        if (bulbLight != null)
        {
            bulbLight.color = lightColor;
            bulbLight.intensity = 0f;
        }

        TurnOff();
    }

    public void TurnOn()
    {
        isOn = true;
        SetIntensity(1f);
    }

    public void TurnOff()
    {
        isOn = false;
        SetIntensity(0f);
    }

    public void SetIntensity(float normalizedIntensity)
    {
        normalizedIntensity = Mathf.Clamp01(normalizedIntensity);

        // Light component
        if (bulbLight != null)
        {
            bulbLight.intensity = normalizedIntensity * maxIntensity;
        }

        // Material emission
        if (bulbMaterial != null)
        {
            Color emissionColor = lightColor * normalizedIntensity * 2f;
            bulbMaterial.SetColor(EmissionColor, emissionColor);
        }
    }

    public bool IsOn()
    {
        return isOn;
    }
}