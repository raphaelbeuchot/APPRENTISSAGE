using UnityEngine;
using System.Collections;

public class TargetCircle : MonoBehaviour
{
    [Header("Circle Settings")]
    public Material redCircleMaterial;
    public Material whiteCircleMaterial;
    public float circleSize = 1f;
    public float whiteFlashDuration = 0.5f;

    private GameObject circleObject;
    private MeshRenderer circleRenderer;
    private bool isVisible = false;

    void Start()
    {
        CreateCircle();
    }

    void CreateCircle()
    {
        circleObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        circleObject.name = $"TargetCircle_{gameObject.name}";
        circleObject.transform.SetParent(null); // CHANGE : pas de parent pour position indépendante

        // NOUVEAU : Trouver le sol sous l'objet
        Vector3 groundPosition = GetGroundPosition();
        circleObject.transform.position = groundPosition + Vector3.up * 0.1f;

        circleObject.transform.rotation = Quaternion.Euler(90, 0, 0);
        circleObject.transform.localScale = Vector3.one * circleSize;

        circleRenderer = circleObject.GetComponent<MeshRenderer>();
        circleRenderer.material = redCircleMaterial;

        Destroy(circleObject.GetComponent<Collider>());

        circleObject.SetActive(false);
    }

    Vector3 GetGroundPosition()
    {
        // Raycast vers le bas pour trouver le sol
        RaycastHit hit;
        int layerMask = LayerMask.GetMask("Default", "Ground");

        if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out hit, 10f, layerMask))
        {
            return hit.point;
        }

        // Fallback : position de l'objet
        return transform.position;

    }

    // AJOUTE Update pour suivre la cible
    void Update()
    {
        if (circleObject != null && circleObject.activeSelf)
        {
            Vector3 groundPosition = GetGroundPosition();
            circleObject.transform.position = groundPosition + Vector3.up * 0.1f;
        }
    }

    public void ShowRedCircle()
    {
        Debug.Log($"ShowRedCircle called on {gameObject.name}");

        if (circleObject == null) CreateCircle();

        circleRenderer.material = redCircleMaterial;
        circleObject.SetActive(true);
        isVisible = true;

        Debug.Log($"Circle position: {circleObject.transform.position}");
    }

    public void HideCircle()
    {
        if (circleObject != null)
        {
            circleObject.SetActive(false);
            isVisible = false;
        }
    }

    public void FlashWhite()
    {
        if (circleObject == null) return;
        StartCoroutine(WhiteFlashSequence());
    }

    IEnumerator WhiteFlashSequence()
    {
        // Flash blanc
        circleRenderer.material = whiteCircleMaterial;
        circleObject.SetActive(true);

        yield return new WaitForSeconds(whiteFlashDuration);

        // Disparaître
        circleObject.SetActive(false);
        isVisible = false;
    }

    public IEnumerator FadeOut(float duration)
    {
        if (circleObject == null || !circleObject.activeSelf) yield break;

        Material mat = circleRenderer.material;
        Color startColor = mat.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / duration);

            Color col = startColor;
            col.a = alpha;
            mat.color = col;

            yield return null;
        }

        HideCircle();

        // Reset alpha
        Color resetCol = startColor;
        resetCol.a = 1f;
        mat.color = resetCol;
    }

    void OnDestroy()
    {
        if (circleObject != null)
            Destroy(circleObject);
    }
}