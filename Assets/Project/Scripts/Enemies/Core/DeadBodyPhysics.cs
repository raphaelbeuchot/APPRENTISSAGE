using UnityEngine;

public class DeadBodyPhysics : MonoBehaviour
{
    private Rigidbody rootRb;
    private Rigidbody[] boneRigidbodies;
    private bool isActive = false;
    private bool isBroomLowState = false;
    private EnemyStats.CorpseData corpseData;

    void Awake()
    {
        rootRb = GetComponent<Rigidbody>();
        boneRigidbodies = GetComponentsInChildren<Rigidbody>();
    }

    void Update()
    {
        // TEST - corpse toujours en resting stats, broomLow ignore
        /*
        if (!isActive) return;
        bool broomLow = PlayerInputManager.Instance.BroomLowActive;
        if (broomLow && !isBroomLowState)
        {
            SetBoneProperties(
                corpseData.restingMass * corpseData.broomLowMassMultiplier,
                corpseData.restingDrag * corpseData.broomLowDragMultiplier
            );
            isBroomLowState = true;
        }
        else if (!broomLow && isBroomLowState)
        {
            SetBoneProperties(corpseData.restingMass, corpseData.restingDrag);
            isBroomLowState = false;
        }
        */
    }

    public void Activate(EnemyStats.CorpseData data)
    {
        corpseData = data;
        isActive = true;
        isBroomLowState = false;
        SetBoneProperties(corpseData.restingMass, corpseData.restingDrag);
    }
    private void SetBoneProperties(float mass, float drag)
    {
        foreach (Rigidbody bone in boneRigidbodies)
        {
            if (bone == rootRb) continue;
            bone.mass = mass / Mathf.Max(1, boneRigidbodies.Length - 1);
            bone.linearDamping = drag;
        }
    }
}