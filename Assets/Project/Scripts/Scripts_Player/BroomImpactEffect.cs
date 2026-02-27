using UnityEngine;

public class BroomImpactEffect : MonoBehaviour
{
    private Animator animator;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        animator = GetComponent<Animator>();

        // Random rotation Z (0-180)
        transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 180f));

        // Random scale (ajuste les valeurs min/max selon ton goût)
        float randomScale = Random.Range(0.1f, 0.2f);
        transform.localScale = Vector3.one * randomScale;

        AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
        if (clips.Length > 0)
        {
            float duration = clips[0].clip.length;
            Destroy(gameObject, duration);
        }
    }

    void LateUpdate()
    {
        if (mainCamera != null)
        {
            transform.rotation = mainCamera.transform.rotation;
        }
    }
}