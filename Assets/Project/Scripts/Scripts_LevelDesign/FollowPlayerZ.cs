using UnityEngine;

public class FollowPlayerZ : MonoBehaviour
{
    private Transform _player;

    public float zOffset = 4f;
    public float zMin = 0f;
    public float zMax = 26f;
    public float zLerpSpeed = 2f;

    void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            _player = playerObj.transform;
        else
            Debug.LogWarning("FollowPlayerZ : aucun objet avec le tag Player trouve.");
    }

    void LateUpdate()
    {
        if (_player == null) return;

        Vector3 pos = transform.position;
        float targetZ = Mathf.Clamp(_player.position.z + zOffset, zMin, zMax);
        pos.z = Mathf.Lerp(pos.z, targetZ, Time.deltaTime * zLerpSpeed);
        transform.position = pos;

        Vector3 direction = _player.position - transform.position;
        direction.y = 0f;
        if (direction != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(direction);
    }
}