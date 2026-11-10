using UnityEngine;

/// <summary>
/// Pics d'une SplinePitZone (contenu Spikes) toujours tournes vers la camera, autour de leur axe vertical
/// seulement (ils ne se penchent pas). Pour le boss de Rush Hour IV, ou la camera tourne autour de la fosse :
/// generes face a -Z, on les verrait de profil sur les cotes et plus du tout de l'autre cote.
/// A poser sur l'objet de la SplinePitZone (le mesh SplinePit_Spikes est regenere par l'outil : un composant
/// pose dessus serait perdu). Ne touche ni a SplinePitZone ni au materiau : au lancement, copie le mesh des pics
/// et retrouve chaque pic a partir de ses 3 sommets (coin gauche, pointe, coin droit, comme les genere
/// SplinePitZone) ; puis le fait tourner chaque frame ou la camera bouge.
/// Une seule camera (Camera.main) : pas de split-screen.
/// </summary>
public class SpikesFaceCamera : MonoBehaviour
{
    [Tooltip("Mesh des pics. Vide = l'enfant SplinePit_Spikes de cet objet.")]
    [SerializeField] private MeshFilter spikesFilter;

    private Mesh mesh;
    private Vector3[] vertices;
    private Vector3[] normals;

    // Par pic, dans le repere du mesh : centre de la base ; en monde : demi-base et hauteur
    private Vector3[] localCenters;
    private float[] halfBases;
    private float[] heights;

    private Camera cam;
    private Vector3 lastCamPos;
    private Matrix4x4 lastMatrix;
    private bool ready;

    private void Start()
    {
        if (spikesFilter == null)
        {
            Transform child = transform.Find("SplinePit_Spikes");
            if (child != null) spikesFilter = child.GetComponent<MeshFilter>();
        }
        if (spikesFilter == null || spikesFilter.sharedMesh == null)
        {
            Debug.LogWarning("[SpikesFaceCamera] Pas de mesh de pics (SplinePit_Spikes) sur " + name, this);
            enabled = false;
            return;
        }

        // Copie : le mesh de la scene n'est pas modifie
        mesh = Instantiate(spikesFilter.sharedMesh);
        mesh.name = spikesFilter.sharedMesh.name + "_FaceCamera";
        mesh.MarkDynamic();
        spikesFilter.sharedMesh = mesh;

        vertices = mesh.vertices;
        normals = new Vector3[vertices.Length];
        int count = vertices.Length / 3;
        localCenters = new Vector3[count];
        halfBases = new float[count];
        heights = new float[count];

        Transform t = spikesFilter.transform;
        float maxHalfBase = 0f;
        for (int i = 0; i < count; i++)
        {
            Vector3 left = vertices[i * 3];
            Vector3 top = vertices[i * 3 + 1];
            Vector3 right = vertices[i * 3 + 2];
            Vector3 center = (left + right) * 0.5f;
            localCenters[i] = center;
            halfBases[i] = Vector3.Distance(t.TransformPoint(left), t.TransformPoint(right)) * 0.5f;
            heights[i] = Vector3.Distance(t.TransformPoint(top), t.TransformPoint(center));
            maxHalfBase = Mathf.Max(maxHalfBase, Vector3.Distance(left, right) * 0.5f);
        }

        // Les pics tournent sur place : on elargit les bounds une fois pour toutes (pas de recalcul par frame)
        mesh.RecalculateBounds();
        Bounds b = mesh.bounds;
        b.Expand(maxHalfBase * 2f);
        mesh.bounds = b;

        ready = true;
    }

    private void LateUpdate()
    {
        if (!ready) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Transform t = spikesFilter.transform;
        Vector3 camPos = cam.transform.position;
        // Rien a refaire si ni la camera ni la fosse n'ont bouge
        if (camPos == lastCamPos && t.localToWorldMatrix == lastMatrix) return;
        lastCamPos = camPos;
        lastMatrix = t.localToWorldMatrix;

        Matrix4x4 toWorld = t.localToWorldMatrix;
        Matrix4x4 toLocal = t.worldToLocalMatrix;
        Vector3 up = Vector3.up;

        for (int i = 0; i < localCenters.Length; i++)
        {
            Vector3 center = toWorld.MultiplyPoint3x4(localCenters[i]);

            // Direction vers la camera, a plat : rotation autour de Y seulement
            Vector3 toCam = camPos - center;
            toCam.y = 0f;
            if (toCam.sqrMagnitude < 0.0001f) continue;
            toCam.Normalize();

            // Meme sens que la generation (face a -Z, droite = +X) : droite = toCam x up
            Vector3 right = Vector3.Cross(toCam, up);

            vertices[i * 3] = toLocal.MultiplyPoint3x4(center - right * halfBases[i]);
            vertices[i * 3 + 1] = toLocal.MultiplyPoint3x4(center + up * heights[i]);
            vertices[i * 3 + 2] = toLocal.MultiplyPoint3x4(center + right * halfBases[i]);

            Vector3 n = toLocal.MultiplyVector(toCam).normalized;
            normals[i * 3] = n;
            normals[i * 3 + 1] = n;
            normals[i * 3 + 2] = n;
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
