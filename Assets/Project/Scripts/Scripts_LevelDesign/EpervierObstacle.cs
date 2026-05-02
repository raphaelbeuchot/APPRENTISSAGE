using UnityEngine;

public class EpervierObstacle : MonoBehaviour
{
    public EpervierManager manager;
    private int quilleLayer;

    void Start()
    {
        quilleLayer = LayerMask.NameToLayer("Quille");
    }

    void OnCollisionEnter(Collision col)
    {
        if (col.gameObject.layer == quilleLayer)
            manager.OnQuillImpact();
    }
}