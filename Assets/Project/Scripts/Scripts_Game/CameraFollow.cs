using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float distance = 9f;
    public float sensibiliteSouris = 500f;
    public float hauteurMinSol = 0.2f;
    public float offsetVertical = 3f;


    private float rotationX = 0f;  // yaw (horizontal)
    private float rotationY = 0f;  // pitch (vertical)
    private float hauteurFixe;     // hauteur constante de la camera

    void Start()
    {
        // On garde en memoire la hauteur initiale de la camera
        hauteurFixe = transform.position.y;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Mouvement souris
        float sourisX = Input.GetAxis("Mouse X") * sensibiliteSouris * Time.deltaTime;
        float sourisY = Input.GetAxis("Mouse Y") * sensibiliteSouris * Time.deltaTime;

        rotationX += sourisX;
        rotationY -= sourisY;

        // Limiter l'angle vertical
        rotationY = Mathf.Clamp(rotationY, -80f, 80f);

        // Creer une rotation a partir des mouvements de souris
        Quaternion rotation = Quaternion.Euler(rotationY, rotationX, 0f);

        // Calculer la position desiree de la camera autour du joueur
        Vector3 offset = rotation * new Vector3(0f, 0f, -distance);

        // On garde la hauteur (Y) de la camera fixe
        Vector3 positionCible = new Vector3(target.position.x, target.position.y + offsetVertical, target.position.z) + offset;

        // Ne pas passer sous le sol
        if (positionCible.y < hauteurMinSol)
            positionCible.y = hauteurMinSol;

        // Appliquer la position finale
        transform.position = positionCible;

        // Regarder le joueur (en ignorant son Y pour eviter que la camera regarde vers le bas quand il saute)
        Vector3 pointRegard = new Vector3(target.position.x, hauteurFixe, target.position.z);
        transform.LookAt(pointRegard);
    }
}