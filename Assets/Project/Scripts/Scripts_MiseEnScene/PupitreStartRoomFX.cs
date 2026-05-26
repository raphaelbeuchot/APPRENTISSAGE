using System.Collections;
using UnityEngine;

public class PupitreStartRoomFX : MonoBehaviour
{
    [Header("Emission")]
    [SerializeField] private Renderer sphereRenderer;
    [SerializeField] private Color couleurEmission = Color.white;
    [SerializeField] private float intervalleEntrePulses = 3.5f;
    [SerializeField] private float pulseDuration = 1f;
    [SerializeField] private float intensiteMax = 0.8f;
    [SerializeField] private float intensiteMin = 0.1f;

    [Header("Son")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sonBip;
    [SerializeField] private bool pulseAuDemarrage = true;

    [Header("Lumiere")]
    [SerializeField] private Light pointLight;
    [SerializeField] private float intensiteLumiereMax = 1.5f;
    [SerializeField] private float intensiteLumiereMin = 0.1f;

    [Header("Materials")]
    [SerializeField] private Material materialApresClick;
    [SerializeField] private Material materialPorteFermee;

    private Material mat;
    private Coroutine fxCoroutine;
    private bool isRestart;

    private void Awake()
    {
        isRestart = PlayerPrefs.GetInt("AutoStartCountdown", 0) == 1;
    }

    private void Start()
    {
        if (isRestart)
        {
            if (pointLight != null) pointLight.intensity = 0f;
            ApplyMaterial(materialPorteFermee);
            return;
        }

        if (sphereRenderer != null)
        {
            mat = sphereRenderer.material;
            mat.EnableKeyword("_EMISSION");
            SetEmission(intensiteMin);
        }

        fxCoroutine = StartCoroutine(BoucleIntermittente());
    }

    private IEnumerator BoucleIntermittente()
    {
        if (pulseAuDemarrage)
            StartCoroutine(Pulse());

        while (true)
        {
            yield return new WaitForSeconds(intervalleEntrePulses);
            StartCoroutine(Pulse());
        }
    }

    private IEnumerator Pulse()
    {
        if (audioSource != null && sonBip != null)
            audioSource.PlayOneShot(sonBip);

        float elapsed = 0f;
        while (elapsed < pulseDuration)
        {
            elapsed += Time.deltaTime;
            SetEmission(Mathf.Lerp(intensiteMax, intensiteMin, elapsed / pulseDuration));
            yield return null;
        }

        SetEmission(intensiteMin);
    }

    private void SetEmission(float intensite)
    {
        if (mat != null)
            mat.SetColor("_EmissionColor", couleurEmission * intensite);

        if (pointLight != null)
        {
            float t = (intensite - intensiteMin) / Mathf.Max(intensiteMax - intensiteMin, 0.001f);
            pointLight.intensity = Mathf.Lerp(intensiteLumiereMin, intensiteLumiereMax, t);
        }
    }

    private void ApplyMaterial(Material m)
    {
        if (m != null && sphereRenderer != null)
            sphereRenderer.material = m;
    }

    // Appele quand le joueur presse X sur le bouton
    public void Stop()
    {
        if (fxCoroutine != null) StopCoroutine(fxCoroutine);
        if (audioSource != null) audioSource.Stop();
        if (pointLight != null) pointLight.intensity = 0f;
        ApplyMaterial(materialApresClick);
    }

    // Appele quand la porte se referme
    public void OnPorteFermee()
    {
        ApplyMaterial(materialPorteFermee);
    }
}
