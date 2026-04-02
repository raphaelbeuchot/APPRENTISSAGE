using UnityEngine;
public class CorpseRagdoll : MonoBehaviour
{
    [Header("Scene Placement")]
    [SerializeField] private bool startActive = false;
    [SerializeField] private float settleDrag = 8f;
    [SerializeField] private float settleMass = 2f;
    private Rigidbody hipsRb;
    private Rigidbody[] allBoneRbs;
    private bool isRegistered = false;

    private void Awake()
    {
        allBoneRbs = GetComponentsInChildren<Rigidbody>();
        Transform hips = transform.Find("Hunged/Armature/mixamorig:Hips");
        if (hips != null)
            hipsRb = hips.GetComponent<Rigidbody>();
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null)
            anim.enabled = false;
        if (startActive)
        {
            foreach (Rigidbody bone in allBoneRbs)
            {
                bone.isKinematic = false;
                bone.linearDamping = settleDrag;
                bone.mass = settleMass / Mathf.Max(1, allBoneRbs.Length);
            }
            RegisterSelf();
        }
        else
        {
            foreach (Rigidbody bone in allBoneRbs)
                bone.isKinematic = true;
        }
    }
    public void Launch(Vector3 force, float hipsMass = 5f)
    {
        CorpseBroomLowPhysics broomLow = GetComponent<CorpseBroomLowPhysics>();
        if (broomLow != null)
            broomLow.NotifyLaunched();
        foreach (Rigidbody bone in allBoneRbs)
        {
            bone.isKinematic = false;
            bone.linearDamping = 0.5f;
        }
        if (hipsRb != null)
        {
            hipsRb.mass = hipsMass;
            hipsRb.AddForce(force, ForceMode.Impulse);
        }
        //RegisterSelf(); TO DO : activer quand gestion corpses dynamique
    }
    private void RegisterSelf()
    {
        if (isRegistered) return;
        CorpsePitHandler handler = GetComponent<CorpsePitHandler>();
        if (handler != null && CleaningBonusManager.Instance != null)
        {
            CleaningBonusManager.Instance.RegisterCorpse(handler);
            isRegistered = true;
        }
    }
}