using UnityEngine;

public class CorpseBroomLowPhysics : MonoBehaviour
{
    [Header("Physique BroomLow")]
    [SerializeField] private float broomLowMass = 0.01f;
    [SerializeField] private float broomLowDrag = 0f;
    [Header("Physique Normal")]
    [SerializeField] private float normalMass = 5f;
    [SerializeField] private float normalDrag = 0f;
    [Header("Delai repos apres lancement")]
    [SerializeField] private float restDelay = 1.5f;
    [Header("Scene Placement")]
    [SerializeField] private bool startActive = false;

    private Rigidbody[] boneRbs;
    private bool hasBeenLaunched = false;
    private bool isAtRest = false;
    private float launchTime = 0f;

    private void Start()
    {
        boneRbs = GetComponentsInChildren<Rigidbody>();
        ApplyProperties(normalMass, normalDrag);

        if (startActive)
        {
            hasBeenLaunched = true;
            isAtRest = true;
        }
    }

    public void NotifyLaunched()
    {
        hasBeenLaunched = true;
        isAtRest = false;
        launchTime = Time.time;
        ApplyProperties(normalMass, normalDrag);
    }

    private void Update()
    {
        if (!hasBeenLaunched) return;
        if (!isAtRest)
        {
            if (Time.time - launchTime >= restDelay)
                isAtRest = true;
            else
                return;
        }
        if (PlayerInputManager.Instance.BroomLowActive)
            ApplyProperties(broomLowMass, broomLowDrag);
        else
            ApplyProperties(normalMass, normalDrag);
    }

    public void ForceNormal()
    {
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