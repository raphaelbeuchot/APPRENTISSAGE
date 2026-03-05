using UnityEngine;

public class DeadBodyPhysics : MonoBehaviour
{
    [Header("Valeurs mort normal")]
    [SerializeField] private float deadMass = 50f;
    [SerializeField] private float deadDrag = 3f;

    [Header("Valeurs broom basse")]
    [SerializeField] private float broomLowMass = 5f;
    [SerializeField] private float broomLowDrag = 0.5f;

    private Rigidbody rb;
    private bool isActive = false;
    private bool isBroomLowState = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (!isActive) return;

        bool broomLow = PlayerInputManager.Instance.BroomLowActive;

        if (broomLow && !isBroomLowState)
        {
            rb.mass = broomLowMass;
            rb.linearDamping = broomLowDrag;
            isBroomLowState = true;
        }
        else if (!broomLow && isBroomLowState)
        {
            rb.mass = deadMass;
            rb.linearDamping = deadDrag;
            isBroomLowState = false;
        }
    }

    public void Activate()
    {
        isActive = true;
        isBroomLowState = false;
        rb.mass = deadMass;
        rb.linearDamping = deadDrag;
    }
}