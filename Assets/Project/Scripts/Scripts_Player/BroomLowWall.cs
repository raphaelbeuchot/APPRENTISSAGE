using UnityEngine;

public class BroomLowWall : MonoBehaviour
{
    [Header("Wall Collider")]
    [SerializeField] private float wallRadius = 0.4f;

    private GameObject wallObject;
    private CapsuleCollider wallCollider;
    private CapsuleCollider playerCollider;

    void Start()
    {
        playerCollider = GetComponent<CapsuleCollider>();

        // Creer le GO wall
        wallObject = new GameObject("BroomLowWall");
        wallObject.transform.SetParent(transform);
        wallObject.transform.localPosition = Vector3.zero;
        wallObject.transform.localRotation = Quaternion.identity;

        // Rigidbody kinematique
        Rigidbody wallRb = wallObject.AddComponent<Rigidbody>();
        wallRb.isKinematic = true;
        wallRb.useGravity = false;

        // CapsuleCollider
        wallCollider = wallObject.AddComponent<CapsuleCollider>();
        wallCollider.radius = wallRadius;

        if (playerCollider != null)
        {
            wallCollider.height = playerCollider.height;
            wallCollider.center = playerCollider.center;
        }

        // Ignorer collision avec le collider du player
        if (playerCollider != null)
            Physics.IgnoreCollision(wallCollider, playerCollider, true);

        wallObject.SetActive(false);
    }

    void Update()
    {
        bool shouldBeActive = PlayerInputManager.Instance.BroomLowActive
            && PlayerInputManager.Instance.MoveInput.magnitude < 0.1f;

        if (wallObject.activeSelf != shouldBeActive)
            wallObject.SetActive(shouldBeActive);
    }
}