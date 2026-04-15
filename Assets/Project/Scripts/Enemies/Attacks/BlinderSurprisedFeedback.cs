using UnityEngine;
using System.Collections;

public class BlinderSurprisedFeedback : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Sprite exclamationSprite;
    [SerializeField] private Vector3 offset = new Vector3(0f, 2.5f, 0f);

    [Header("Pop Settings")]
    [SerializeField] private float popScale = 1.8f;
    [SerializeField] private float popDuration = 0.15f;
    [SerializeField] private float settleDuration = 0.1f;
    [SerializeField] private float finalScale = 1f;

    [Header("Audio")]
    [SerializeField] private AudioClip surprisedSound;
    private AudioSource audioSource;

    private GameObject iconGO;
    private SpriteRenderer spriteRenderer;
    private Camera mainCamera;
    private bool isSurprised = false;


    void Start()
    {
        mainCamera = Camera.main;
        CreateIcon();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
    }

    void CreateIcon()
    {
        iconGO = new GameObject("BlinderExclamation");
        iconGO.transform.SetParent(transform);
        iconGO.transform.localPosition = offset;
        iconGO.transform.localScale = Vector3.zero;

        spriteRenderer = iconGO.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = exclamationSprite;
        spriteRenderer.sortingLayerName = "UI";
        spriteRenderer.sortingOrder = 10;

        iconGO.SetActive(false);
    }

    void LateUpdate()
    {
        if (iconGO != null && iconGO.activeSelf && mainCamera != null)
        {
            iconGO.transform.rotation = mainCamera.transform.rotation;
        }
    }

    public void ShowExclamation()
    {
        if (isSurprised) return;
        isSurprised = true;
        Debug.Log("ShowExclamation called");
        if (iconGO == null) return;
        StopAllCoroutines();
        iconGO.SetActive(true);
        if (surprisedSound != null)
            audioSource.PlayOneShot(surprisedSound);
        StartCoroutine(PopIn());
    }

    public void HideExclamation()
    {
        isSurprised = false;
        if (iconGO == null) return;
        StopAllCoroutines();
        iconGO.SetActive(false);
        iconGO.transform.localScale = Vector3.zero;
    }

    IEnumerator PopIn()
    {
        // Overshoot : 0 -> popScale
        float elapsed = 0f;
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / popDuration;
            float scale = Mathf.Lerp(0f, popScale, t);
            iconGO.transform.localScale = Vector3.one * scale;
            yield return null;
        }

        // Settle : popScale -> finalScale
        elapsed = 0f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / settleDuration;
            float scale = Mathf.Lerp(popScale, finalScale, t);
            iconGO.transform.localScale = Vector3.one * scale;
            yield return null;
        }

        iconGO.transform.localScale = Vector3.one * finalScale;

        yield return new WaitForSeconds(1f);
        isSurprised = false;
        HideExclamation();
    }
}