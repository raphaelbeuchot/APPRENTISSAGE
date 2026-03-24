using UnityEngine;

public class CorpseBroomLowPhysics : MonoBehaviour
{
    [Header("Physique BroomLow")]
    [SerializeField] private float broomLowMass = 1f;
    [SerializeField] private float broomLowDrag = 0.5f;

    [Header("Physique Normal")]
    [SerializeField] private float normalMass = 5f;
    [SerializeField] private float normalDrag = 3f;

    private Rigidbody[] boneRbs;
    private bool wasBroomLowActive = false;

    private void Start()
    {
        boneRbs = GetComponentsInChildren<Rigidbody>();
        ApplyProperties(normalMass, normalDrag);
    }

    private void Update()
    {
        bool broomLowActive = PlayerInputManager.Instance.BroomLowActive;

        if (broomLowActive == wasBroomLowActive) return;

        wasBroomLowActive = broomLowActive;

        if (broomLowActive)
            ApplyProperties(broomLowMass, broomLowDrag);
        else
            ApplyProperties(normalMass, normalDrag);
    }

    private void ApplyProperties(float mass, float drag)
    {
        foreach (Rigidbody bone in boneRbs)
        {
            bone.mass = mass;
            bone.linearDamping = drag;
        }
    }
}