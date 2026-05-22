using System.Collections;
using UnityEngine;

public class DoorReturnTrigger : MonoBehaviour
{
    [SerializeField] private Transform door;
    [SerializeField] private float descentDuration = 1.2f;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sonDescente;

    private Vector3 initialDoorPosition;
    private bool triggered = false;

    private void Start()
    {
        if (door != null)
            initialDoorPosition = door.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered || !other.CompareTag("Player")) return;

        triggered = true;
        StartCoroutine(DescendreDoor());
    }

    private IEnumerator DescendreDoor()
    {
        if (sonDescente != null)
            audioSource.PlayOneShot(sonDescente);

        Vector3 startPos = door.position;
        float elapsed = 0f;

        while (elapsed < descentDuration)
        {
            elapsed += Time.deltaTime;
            door.position = Vector3.Lerp(startPos, initialDoorPosition, elapsed / descentDuration);
            yield return null;
        }

        door.position = initialDoorPosition;
    }
}
