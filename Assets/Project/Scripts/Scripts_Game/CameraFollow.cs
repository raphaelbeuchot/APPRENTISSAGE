using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public TargetLockSystem lockSystem;

    [Header("Normal Mode")]
    public float distance = 9f;
    public float sensibiliteSouris = 500f;
    public float hauteurMinSol = 0.2f;
    public float offsetVertical = 3f;

    [Header("Lock-On Mode")]
    public float lockBlendSpeed = 5f;
    public float lockMouseInfluence = 0.3f;

    private float rotationX = 0f;
    private float rotationY = 0f;
    private float hauteurFixe;

    void Start()
    {
        hauteurFixe = transform.position.y;
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (lockSystem != null && lockSystem.IsLocked)
        {
            UpdateNormalCamera();
            
        }
        else
        {
            UpdateNormalCamera();
        }
    }

    private void UpdateNormalCamera()
    {
        // === NOUVEAU : Utiliser PlayerInputManager ===
        Vector2 lookInput = PlayerInputManager.Instance.LookInput;
        float sourisX = lookInput.x * sensibiliteSouris * Time.deltaTime;
        float sourisY = lookInput.y * sensibiliteSouris * Time.deltaTime;

        rotationX += sourisX;
        rotationY -= sourisY;
        rotationY = Mathf.Clamp(rotationY, -80f, 80f);

        Quaternion rotation = Quaternion.Euler(rotationY, rotationX, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -distance);
        Vector3 positionCible = new Vector3(target.position.x, target.position.y + offsetVertical, target.position.z) + offset;

        if (positionCible.y < hauteurMinSol)
            positionCible.y = hauteurMinSol;

        transform.position = positionCible;
        Vector3 pointRegard = new Vector3(target.position.x, hauteurFixe, target.position.z);
        transform.LookAt(pointRegard);
    }

    private void UpdateLockedCamera()
    {
        Transform lockedTarget = lockSystem.CurrentTarget;
        if (lockedTarget == null)
        {
            UpdateNormalCamera();
            return;
        }

        // === NOUVEAU : Utiliser PlayerInputManager ===
        Vector2 lookInput = PlayerInputManager.Instance.LookInput;
        float sourisX = lookInput.x * sensibiliteSouris * lockMouseInfluence * Time.deltaTime;
        float sourisY = lookInput.y * sensibiliteSouris * lockMouseInfluence * Time.deltaTime;

        Vector3 directionToTarget = lockedTarget.position - target.position;
        directionToTarget.y = 0f;
        float targetYaw = Mathf.Atan2(directionToTarget.x, directionToTarget.z) * Mathf.Rad2Deg;

        rotationX = Mathf.LerpAngle(rotationX, targetYaw + sourisX, lockBlendSpeed * Time.deltaTime);
        rotationY -= sourisY;
        rotationY = Mathf.Clamp(rotationY, -80f, 80f);

        Quaternion rotation = Quaternion.Euler(rotationY, rotationX, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -distance);
        Vector3 positionCible = new Vector3(target.position.x, target.position.y + offsetVertical, target.position.z) + offset;

        if (positionCible.y < hauteurMinSol)
            positionCible.y = hauteurMinSol;

        transform.position = positionCible;
        Vector3 lookPoint = Vector3.Lerp(target.position, lockedTarget.position, 0.5f);
        lookPoint.y = hauteurFixe;
        transform.LookAt(lookPoint);
    }
}