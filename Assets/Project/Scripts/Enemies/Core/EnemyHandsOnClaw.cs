using UnityEngine;

// Colle les mains du zombie (rig Humanoid) sur deux cibles, par IK, apres l'animation.
// Cibles = objets vides enfants des machoires de la pince (voir EnemyClaw) : les mains suivent le claquement.
// A poser sur le meme GameObject que l'Animator. Un calque au moins du controller doit avoir IK Pass coche.
[RequireComponent(typeof(Animator))]
public class EnemyHandsOnClaw : MonoBehaviour
{
    [Header("Cibles des mains")]
    public Transform leftHandTarget;
    public Transform rightHandTarget;

    [Header("Coudes (optionnel)")]
    [Tooltip("Objet vers lequel le coude gauche se plie")]
    public Transform leftElbowHint;
    public Transform rightElbowHint;

    [Header("Poids")]
    [Range(0f, 1f)] public float positionWeight = 1f;
    [Range(0f, 1f)] public float rotationWeight = 1f;
    [Range(0f, 1f)] public float hintWeight = 1f;

    Animator animator;
    EnemyHealth health;

    void Awake()
    {
        animator = GetComponent<Animator>();
        health = GetComponent<EnemyHealth>();
    }

    void OnAnimatorIK(int layerIndex)
    {
        bool active = enabled && (health == null || !health.IsDead());
        SetHand(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, leftHandTarget, leftElbowHint, active);
        SetHand(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, rightHandTarget, rightElbowHint, active);
    }

    void SetHand(AvatarIKGoal goal, AvatarIKHint hint, Transform target, Transform elbow, bool active)
    {
        float pw = (active && target != null) ? positionWeight : 0f;
        float rw = (active && target != null) ? rotationWeight : 0f;
        animator.SetIKPositionWeight(goal, pw);
        animator.SetIKRotationWeight(goal, rw);
        if (target != null)
        {
            animator.SetIKPosition(goal, target.position);
            animator.SetIKRotation(goal, target.rotation);
        }

        float hw = (active && target != null && elbow != null) ? hintWeight : 0f;
        animator.SetIKHintPositionWeight(hint, hw);
        if (elbow != null) animator.SetIKHintPosition(hint, elbow.position);
    }
}
