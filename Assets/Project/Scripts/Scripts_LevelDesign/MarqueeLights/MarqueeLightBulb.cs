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

    private MaterialPropertyBlock propertyBlock;
    private Color currentBaseEmissionColor;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        if (bulbRenderer == null)
            bulbRenderer = GetComponentInChildren<MeshRenderer>();

        propertyBlock = new MaterialPropertyBlock();
        TurnOff();
    }

    public void SetRedLightMode()
    {
        if (bulbRenderer == null || redLightMaterial == null) return;
        bulbRenderer.sharedMaterial = redLightMaterial;
        currentBaseEmissionColor = redLightColor;
        SetEmissionIntensity(redEmissionIntensity);
    }

    public void SetAlertMode()
    {
        if (bulbRenderer == null || alertMaterial == null) return;
        bulbRenderer.sharedMaterial = alertMaterial;
        currentBaseEmissionColor = alertColor;
        SetEmissionIntensity(alertEmissionIntensity);
    }

    public void SetGreenLightMode()
    {
        if (bulbRenderer == null || greenLightMaterial == null) return;
        bulbRenderer.sharedMaterial = greenLightMaterial;
        currentBaseEmissionColor = greenLightColor;
        SetEmissionIntensity(greenEmissionIntensity);
    }

    public void TurnOff()
    {
        if (bulbRenderer == null || lightOffMaterial == null) return;
        bulbRenderer.sharedMaterial = lightOffMaterial;
    }

    public void SetEmissionIntensity(float intensity)
    {
        if (bulbRenderer == null) return;
        bulbRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(EmissionColor, currentBaseEmissionColor * intensity);
        bulbRenderer.SetPropertyBlock(propertyBlock);
    }

    public float GetRedEmissionIntensity() => redEmissionIntensity;
    public float GetGreenEmissionIntensity() => greenEmissionIntensity;
    public float GetAlertEmissionIntensity() => alertEmissionIntensity;
}