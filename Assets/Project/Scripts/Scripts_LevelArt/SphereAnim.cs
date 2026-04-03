using UnityEngine;

public class SphereAnim : MonoBehaviour
{
    [Header("Rayon")]
    public float rayonDebut = 0.5f;
    public float rayonFin = 1.5f;

    [Header("Alpha")]
    public float alphaDebut = 0.8f;
    public float alphaFin = 0.1f;

    [Header("Timing")]
    public float vitesseLerp = 1f;
    public float decalage = 0f;

    private Material mat;
    private float t = 0f;

    void Start()
    {
        mat = GetComponent<Renderer>().material;
        t = -decalage * vitesseLerp;
    }

    void Update()
    {
        t += Time.deltaTime * vitesseLerp;

        float cycle = Mathf.Repeat(t, 1f);

        float rayon = Mathf.Lerp(rayonDebut, rayonFin, cycle);
        transform.localScale = Vector3.one * rayon * 2f;

        Color c = mat.color;
        c.a = Mathf.Lerp(alphaDebut, alphaFin, cycle);
        mat.color = c;
    }
}