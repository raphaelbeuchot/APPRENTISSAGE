using UnityEngine;

public class CorpseRagdoll : MonoBehaviour
{
    private Rigidbody hipsRb;
    private Rigidbody[] allBoneRbs;

    private void Awake()
    {
        allBoneRbs = GetComponentsInChildren<Rigidbody>();

        Transform hips = transform.Find("TPose/Armature/mixamorig:Hips");
        if (hips != null)
            hipsRb = hips.GetComponent<Rigidbody>();

        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null)
            anim.enabled = false;

        foreach (Rigidbody bone in allBoneRbs)
            bone.isKinematic = true;
    }

    public void Launch(Vector3 force)
    {
        foreach (Rigidbody bone in allBoneRbs)
            bone.isKinematic = false;

        if (hipsRb != null)
            hipsRb.AddForce(force, ForceMode.Impulse);
    }
}