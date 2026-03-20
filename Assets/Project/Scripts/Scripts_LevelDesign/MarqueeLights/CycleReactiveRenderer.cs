using UnityEngine;
using System.Collections;

public class CycleReactiveRenderer : MonoBehaviour
{
    [Header("Materials par etat")]
    [SerializeField] private Material materialNeutral;
    [SerializeField] private Material materialGreenLight;
    [SerializeField] private Material materialAlert;
    [SerializeField] private Material materialRedLight;
    [SerializeField] private Material materialRelease;

    [Header("Pulse - GreenLight")]
    [SerializeField] private bool pulseEmissionGreen = false;
    [SerializeField] private float pulseSpeedGreen = 1f;
    [SerializeField] private float pulseSizeGreen = 0.5f;
    [SerializeField] private bool pulseScaleGreen = false;

    [Header("Pulse - Alert")]
    [SerializeField] private bool pulseEmissionAlert = true;
    [SerializeField] private float pulseSpeedAlert = 3f;
    [SerializeField] private float pulseSizeAlert = 1f;
    [SerializeField] private bool pulseScaleAlert = false;

    [Header("Pulse - RedLight")]
    [SerializeField] private bool pulseEmissionRed = false;
    [SerializeField] private float pulseSpeedRed = 1f;
    [SerializeField] private float pulseSizeRed = 0.3f;
    [SerializeField] private bool pulseScaleRed = false;

    [Header("Pulse - Release")]
    [SerializeField] private bool pulseEmissionRelease = false;
    [SerializeField] private float pulseSpeedRelease = 1f;
    [SerializeField] private float pulseSizeRelease = 0.3f;
    [SerializeField] private bool pulseScaleRelease = false;

    private MeshRenderer meshRenderer;
    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Vector3 baseScale;
    private Coroutine pulseCoroutine;

    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        propertyBlock = new MaterialPropertyBlock();
        baseScale = transform.localScale;

        ApplyMaterial(materialNeutral);
    }

    void OnEnable()
    {
        SentinelCycleManager.OnCycleChanged += OnCycleChanged;
    }

    void OnDisable()
    {
        SentinelCycleManager.OnCycleChanged -= OnCycleChanged;
        StopPulse();
        transform.localScale = baseScale;
    }

    private void OnCycleChanged(SentinelCycleManager.GameState newState)
    {
        StopPulse();
        transform.localScale = baseScale;

        switch (newState)
        {
            case SentinelCycleManager.GameState.GreenLight:
                ApplyMaterial(materialGreenLight);
                if (pulseEmissionGreen || pulseScaleGreen)
                    pulseCoroutine = StartCoroutine(PulseCoroutine(pulseSpeedGreen, pulseSizeGreen, pulseEmissionGreen, pulseScaleGreen));
                break;

            case SentinelCycleManager.GameState.Alert:
                ApplyMaterial(materialAlert);
                if (pulseEmissionAlert || pulseScaleAlert)
                    pulseCoroutine = StartCoroutine(PulseCoroutine(pulseSpeedAlert, pulseSizeAlert, pulseEmissionAlert, pulseScaleAlert));
                break;

            case SentinelCycleManager.GameState.RedLight:
                ApplyMaterial(materialRedLight);
                if (pulseEmissionRed || pulseScaleRed)
                    pulseCoroutine = StartCoroutine(PulseCoroutine(pulseSpeedRed, pulseSizeRed, pulseEmissionRed, pulseScaleRed));
                break;

            case SentinelCycleManager.GameState.Release:
                ApplyMaterial(materialRelease);
                if (pulseEmissionRelease || pulseScaleRelease)
                    pulseCoroutine = StartCoroutine(PulseCoroutine(pulseSpeedRelease, pulseSizeRelease, pulseEmissionRelease, pulseScaleRelease));
                break;
        }
    }

    private void ApplyMaterial(Material mat)
    {
        if (mat == null) return;

        if (meshRenderer != null)
            meshRenderer.sharedMaterial = mat;
        else if (spriteRenderer != null)
            spriteRenderer.sharedMaterial = mat;
    }

    private IEnumerator PulseCoroutine(float speed, float size, bool affectEmission, bool affectScale)
    {
        // Recupere la couleur emission du material actif
        Color baseEmission = Color.black;
        Material currentMat = GetCurrentMaterial();

        if (affectEmission && currentMat != null && currentMat.HasProperty(EmissionColorID))
            baseEmission = currentMat.GetColor(EmissionColorID);

        while (true)
        {
            float t = (Mathf.Sin(Time.time * speed * Mathf.PI * 2f) + 1f) / 2f; // 0 a 1

            if (affectEmission)
            {
                if (meshRenderer != null)
                {
                    meshRenderer.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor(EmissionColorID, baseEmission * (1f + t * size));
                    meshRenderer.SetPropertyBlock(propertyBlock);
                }
                else if (spriteRenderer != null)
                {
                    spriteRenderer.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor(EmissionColorID, baseEmission * (1f + t * size));
                    spriteRenderer.SetPropertyBlock(propertyBlock);
                }
            }

            if (affectScale)
            {
                transform.localScale = baseScale * (1f + t * size * 0.2f);
            }

            yield return null;
        }
    }

    private void StopPulse()
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        // Reset property block
        if (meshRenderer != null)
        {
            meshRenderer.SetPropertyBlock(null);
        }
        else if (spriteRenderer != null)
        {
            spriteRenderer.SetPropertyBlock(null);
        }
    }

    private Material GetCurrentMaterial()
    {
        if (meshRenderer != null) return meshRenderer.sharedMaterial;
        if (spriteRenderer != null) return spriteRenderer.sharedMaterial;
        return null;
    }
}