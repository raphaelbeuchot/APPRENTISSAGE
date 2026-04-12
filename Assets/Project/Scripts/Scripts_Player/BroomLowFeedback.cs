using System.Collections;
using UnityEngine;

public class BroomLowFeedback : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip broomLowEnterSound;

    [Header("Pulse")]
    [SerializeField] private Material pulseMaterial;
    [SerializeField] private float fadeDuration = 0.6f;
    [SerializeField] private float pulseInterval = 1.5f;
    [SerializeField] private float startAlpha = 0.8f;
    [SerializeField] private float curveExponent = 0.3f;

    private AudioSource audioSource;
    private SkinnedMeshRenderer[] renderers;
    private Material pulseInstance;
    private bool isPulsing = false;
    private bool isFlashing = false;
    private Coroutine pulseCoroutine;
    private Material[][] snapshot;

    void Start()
    {
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null)
            audioSource = gm.GetComponent<AudioSource>();

        renderers = GetComponentsInChildren<SkinnedMeshRenderer>();

        if (pulseMaterial != null)
            pulseInstance = new Material(pulseMaterial);
        else
            Debug.LogWarning("[BroomLowFeedback] pulseMaterial non assigne !");
    }

    public void OnBroomLowEnter()
    {
        if (audioSource != null && broomLowEnterSound != null)
            audioSource.PlayOneShot(broomLowEnterSound);

        isPulsing = true;
        if (pulseCoroutine != null)
            StopCoroutine(pulseCoroutine);
        pulseCoroutine = StartCoroutine(PulseLoop());
    }

    public void OnBroomLowExit()
    {
        isPulsing = false;
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }
        if (isFlashing)
            RestoreSnapshot();
    }

    void RestoreSnapshot()
    {
        if (snapshot == null) return;
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].materials = snapshot[i];
        isFlashing = false;
    }

    IEnumerator PulseLoop()
    {
        while (isPulsing)
        {
            bool isImmobile = PlayerInputManager.Instance.MoveInput.magnitude < 0.1f;
            if (!isImmobile)
            {
                yield return null;
                continue;
            }

            yield return StartCoroutine(FlashAndFade());

            float waited = fadeDuration;
            while (waited < pulseInterval)
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }
    }

    IEnumerator FlashAndFade()
    {
        if (pulseInstance == null) yield break;

        // Snapshot juste avant d'ajouter
        snapshot = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
            snapshot[i] = renderers[i].materials;

        Color baseColor = pulseInstance.GetColor("_BaseColor");
        baseColor.a = startAlpha;
        pulseInstance.SetColor("_BaseColor", baseColor);

        foreach (SkinnedMeshRenderer r in renderers)
        {
            Material[] mats = new Material[r.materials.Length + 1];
            r.materials.CopyTo(mats, 0);
            mats[mats.Length - 1] = pulseInstance;
            r.materials = mats;
        }

        isFlashing = true;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            Color c = baseColor;
            c.a = Mathf.Lerp(startAlpha, 0f, Mathf.Pow(elapsed / fadeDuration, curveExponent));
            pulseInstance.SetColor("_BaseColor", c);
            yield return null;
        }

        RestoreSnapshot();
    }
}