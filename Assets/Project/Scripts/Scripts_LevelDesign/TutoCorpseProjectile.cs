using UnityEngine;

public class TutoCorpseProjectile : MonoBehaviour
{
    private TutoFreezeTile tile;

    public void Init(TutoFreezeTile sourceTile, Vector3 force)
    {
        tile = sourceTile;
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.AddForce(force, ForceMode.Impulse);
    }

    void OnCollisionEnter(Collision col)
    {
        if (tile == null) return;
        if (col.gameObject.CompareTag("Player"))
        {
            tile.OnCorpseHitPlayer();
            tile = null;
        }
    }
}