using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class MaterialScreenPosUpdater : MonoBehaviour
{
    private Renderer rend;
    private Camera cam;

    void Start()
    {
        rend = GetComponent<Renderer>();
        cam = Camera.main;
    }

    void Update()
    {
        if (cam == null) return;
        Vector3 vp = cam.WorldToViewportPoint(transform.position);
        rend.material.SetVector("_ObjectScreenPos", new Vector4(vp.x, vp.y, 0f, 0f));
    }
}