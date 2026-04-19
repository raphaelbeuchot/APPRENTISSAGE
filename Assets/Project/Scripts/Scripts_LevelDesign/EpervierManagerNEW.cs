using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EpervierManagerNEW : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform arrivalPoint;

    [Header("Line Config")]
    [SerializeField] private int numberOfBalls = 6;
    [SerializeField] private int numberOfGaps = 2;
    [SerializeField] private float slotWidth = 2f;
    [SerializeField] private float ballRadius = 0.5f;

    [Header("Speeds")]
    [SerializeField] private float rearrangeSpeed = 6f;
    [SerializeField] private float launchSpeed = 10f;

    [Header("Physics")]
    [SerializeField] private float ballMass = 5f;
    [SerializeField] private float ballDrag = 0f;
    [SerializeField] private float ballAngularDrag = 0.05f;
    [SerializeField] private PhysicsMaterial ballPhysicsMaterial;

    [Header("Cycle")]
    [SerializeField] private float delayBetweenCycles = 1.5f;

    [Header("Audio")]
    [SerializeField] private AudioClip soundRearrange;
    [SerializeField] private AudioClip soundLaunch;

    private AudioSource audioSource;
    private List<GameObject> activeBalls = new List<GameObject>();
    private float[] slotXPositions;
    private bool isCycleRunning = false;

    void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        BuildSlotPositions();
        // Plus de CycleCoroutine auto
    }

    public void OnGreenLight()
    {
        StopAllCoroutines();
        foreach (GameObject ball in activeBalls)
            if (ball != null) Destroy(ball);
        activeBalls.Clear();

        StartCoroutine(CycleCoroutine());
    }

    public void OnRedLight()
    {
        // Les boules continuent leur traversee pendant le RedLight
        // On ne fait rien ici, ou on accelere si tu veux
    }

    void Update()
    {
        for (int i = activeBalls.Count - 1; i >= 0; i--)
        {
            if (activeBalls[i] == null)
            {
                activeBalls.RemoveAt(i);
                continue;
            }

            if (activeBalls[i].transform.position.z >= arrivalPoint.position.z)
            {
                Destroy(activeBalls[i]);
                activeBalls.RemoveAt(i);
            }
        }
    }

    // -------------------------------------------------------
    // SLOT POSITIONS
    // -------------------------------------------------------

    void BuildSlotPositions()
    {
        int obstacleCount = numberOfBalls - numberOfGaps;
        slotXPositions = new float[obstacleCount];

        float totalWidth = obstacleCount * slotWidth;
        float startX = spawnPoint.position.x - totalWidth / 2f + slotWidth / 2f;

        for (int i = 0; i < obstacleCount; i++)
            slotXPositions[i] = startX + i * slotWidth;
    }

    // -------------------------------------------------------
    // CYCLE
    // -------------------------------------------------------

    IEnumerator CycleCoroutine()
    {
        while (true)
        {
            yield return StartCoroutine(SpawnAndRearrangeCoroutine());
            yield return StartCoroutine(LaunchCoroutine());
            yield return new WaitForSeconds(delayBetweenCycles);
        }
    }

    IEnumerator SpawnAndRearrangeCoroutine()
    {
        int obstacleCount = numberOfBalls - numberOfGaps;
        float[] targetX = GetShuffledSlotPositions();

        // Spawn toutes les boules a la position de spawn, kinematic
        for (int i = 0; i < obstacleCount; i++)
        {
            GameObject ball = CreateBall(i);
            ball.transform.position = new Vector3(spawnPoint.position.x, spawnPoint.position.y, spawnPoint.position.z);
            activeBalls.Add(ball);
        }

        if (soundRearrange != null)
            audioSource.PlayOneShot(soundRearrange);

        // Rearrange horizontal vers les slots cibles
        bool allDone = false;
        while (!allDone)
        {
            allDone = true;
            for (int i = 0; i < activeBalls.Count; i++)
            {
                if (activeBalls[i] == null) continue;
                Vector3 pos = activeBalls[i].transform.position;
                float newX = Mathf.MoveTowards(pos.x, targetX[i], rearrangeSpeed * Time.deltaTime);
                activeBalls[i].transform.position = new Vector3(newX, pos.y, pos.z);

                if (Mathf.Abs(newX - targetX[i]) > 0.02f)
                    allDone = false;
            }
            yield return null;
        }

        // Snap final
        for (int i = 0; i < activeBalls.Count; i++)
        {
            if (activeBalls[i] == null) continue;
            Vector3 pos = activeBalls[i].transform.position;
            activeBalls[i].transform.position = new Vector3(targetX[i], pos.y, pos.z);
        }
    }

    IEnumerator LaunchCoroutine()
    {
        if (soundLaunch != null)
            audioSource.PlayOneShot(soundLaunch);

        // Switch dynamic + velocity
        for (int i = 0; i < activeBalls.Count; i++)
        {
            if (activeBalls[i] == null) continue;

            Rigidbody rb = activeBalls[i].GetComponent<Rigidbody>();
            if (rb == null) continue;

            rb.isKinematic = false;
            rb.linearVelocity = Vector3.forward * launchSpeed;
        }

        // Attendre que toutes les boules soient detruites ou hors limites
        yield return new WaitUntil(() => activeBalls.Count == 0);
    }

    // -------------------------------------------------------
    // FACTORY
    // -------------------------------------------------------

    GameObject CreateBall(int index)
    {
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "EpervierBall_" + index;
        ball.transform.parent = transform;
        ball.transform.localScale = Vector3.one * (ballRadius * 2f);
        ball.layer = LayerMask.NameToLayer("Default");

        SphereCollider col = ball.GetComponent<SphereCollider>();
        if (ballPhysicsMaterial != null)
            col.material = ballPhysicsMaterial;

        Rigidbody rb = ball.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.mass = ballMass;
        rb.linearDamping = ballDrag;
        rb.angularDamping = ballAngularDrag;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        return ball;
    }

    // -------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------

    float[] GetShuffledSlotPositions()
    {
        List<float> slots = new List<float>(slotXPositions);
        for (int i = slots.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            float tmp = slots[i];
            slots[i] = slots[j];
            slots[j] = tmp;
        }
        return slots.ToArray();
    }
}