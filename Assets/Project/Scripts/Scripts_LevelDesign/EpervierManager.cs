using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EpervierManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SentinelCycleManager sentinelCycleManager;

    [Header("Terrain")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform arrivalPoint;
    [SerializeField] private float groundY = 0f;

    [Header("Line Config")]
    [SerializeField] private int numberOfSlots = 6;
    [SerializeField] private int numberOfGaps = 2;
    [SerializeField] private float slotWidth = 2f;
    [SerializeField] private float obstacleHeight = 2f;
    [SerializeField] private float obstacleDepth = 0.5f;

    [Header("Speeds")]
    [SerializeField] private float dropSpeed = 5f;
    [SerializeField] private float traverseSpeed = 8f;
    [SerializeField] private float returnSpeed = 10f;
    [SerializeField] private float rearrangeSpeed = 4f;
    [SerializeField] private float greenLightDelay = 1f;
    [SerializeField] private float maxTraverseDelay = 0.3f;

    [Header("Player Escape")]
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private float escapeRaycastHeight = 2f;
    [SerializeField] private float escapeForceDuration = 0.5f;
    [SerializeField] private float escapeForce = 10f;
    [SerializeField] private PlayerPhysicsMovement playerMovement;

    [SerializeField] private float sweepRaycastDistance = 1.5f;
    private bool isSweeping = false;

    private bool isEscaping = false;
    private bool isFirstDrop = true;

    [SerializeField] private Material[] obstacleMaterials;

    private enum LineState { Idle, Rearranging, Dropping, Ready, Traversing, Returning }

    private class EpervierLine
    {
        public GameObject[] obstacles;
        public bool[] isGap;
        public LineState state = LineState.Idle;
        public float currentY;
        public float[] currentZ;
        public float[] currentX;
        public Coroutine activeCoroutine;
    }

    private const int POOL_SIZE = 1;
    private EpervierLine[] lines;
    private float[] slotXPositions;
    private int readyLineIndex = -1;

    // -------------------------------------------------------
    // INIT
    // -------------------------------------------------------

    void Start()
    {
        BuildSlotPositions();
        CreateLines();
    }

    void Update()
    {
        if (isEscaping || playerMovement == null) return;

        Vector3 rayOrigin = playerRigidbody.position + Vector3.up * 1.4f;
        RaycastHit hit;
        int obstacleLayer = LayerMask.GetMask("Obstacle");

        if (Physics.Raycast(rayOrigin, Vector3.up, out hit, escapeRaycastHeight, obstacleLayer))
        {
            Debug.Log("[Epervier] Raycast touche : " + hit.collider.gameObject.name + " layer : " + hit.collider.gameObject.layer);

            for (int i = 0; i < POOL_SIZE; i++)
            {
                if (lines[i].state != LineState.Dropping && lines[i].state != LineState.Rearranging) continue;
                foreach (GameObject obs in lines[i].obstacles)
                {
                    if (obs == hit.collider.gameObject)
                    {
                        Debug.Log("[Epervier] Obstacle reconnu, lancement EscapeCoroutine");
                        StartCoroutine(EscapeCoroutine());
                        return;
                    }
                }
            }
        }
        else
        {
            Debug.Log("[Epervier] Raycast ne touche rien");
        }

        if (!isSweeping)
        {
            Vector3 sweepRayOrigin = playerRigidbody.position + Vector3.up * 0.6f;
            RaycastHit sweepHit;

            if (Physics.Raycast(sweepRayOrigin, Vector3.forward, out sweepHit, sweepRaycastDistance, obstacleLayer))
            {
                for (int i = 0; i < POOL_SIZE; i++)
                {
                    if (lines[i].state != LineState.Traversing) continue;
                    foreach (GameObject obs in lines[i].obstacles)
                    {
                        if (obs == sweepHit.collider.gameObject)
                        {
                            isSweeping = true;
                            StartCoroutine(SweepResetCoroutine());
                            playerMovement.TriggerSweep();
                            return;
                        }
                    }
                }
            }
        }

        EnemyAI_AStar[] enemies = FindObjectsOfType<EnemyAI_AStar>();
        foreach (EnemyAI_AStar enemy in enemies)
        {
            if (enemy.isDead || enemy.isKnockedDownByEpervier) continue;

            Rigidbody enemyRb = enemy.GetComponent<Rigidbody>();
            if (enemyRb == null) continue;

            Vector3 enemyRayOrigin = enemyRb.position + Vector3.up * 1.4f;
            RaycastHit enemyHit;

            if (Physics.Raycast(enemyRayOrigin, Vector3.up, out enemyHit, escapeRaycastHeight, obstacleLayer))
            {
                for (int i = 0; i < POOL_SIZE; i++)
                {
                    if (lines[i].state != LineState.Dropping && lines[i].state != LineState.Rearranging) continue;
                    foreach (GameObject obs in lines[i].obstacles)
                    {
                        if (obs == enemyHit.collider.gameObject)
                        {
                            enemyRb.AddForce(enemy.transform.forward * escapeForce, ForceMode.VelocityChange);
                            break;
                        }
                    }
                }
            }
        }
    }

    IEnumerator SweepResetCoroutine()
    {
        yield return new WaitForSeconds(0.5f);
        isSweeping = false;
    }

    IEnumerator EscapeCoroutine()
    {
        isEscaping = true;

        if (playerMovement != null)
            playerMovement.ApplyProgressivePush(Vector3.forward, escapeForce, escapeForceDuration);

        yield return new WaitForSeconds(escapeForceDuration);

        isEscaping = false;
    }

    // -------------------------------------------------------
    // SETUP
    // -------------------------------------------------------

    void BuildSlotPositions()
    {
        slotXPositions = new float[numberOfSlots];
        float totalWidth = numberOfSlots * slotWidth;
        float startX = spawnPoint.position.x - totalWidth / 2f + slotWidth / 2f;

        for (int i = 0; i < numberOfSlots; i++)
            slotXPositions[i] = startX + i * slotWidth;
    }

    void CreateLines()
    {
        lines = new EpervierLine[POOL_SIZE];
        int obstacleCount = numberOfSlots - numberOfGaps;

        for (int i = 0; i < POOL_SIZE; i++)
        {
            EpervierLine line = new EpervierLine();
            line.obstacles = new GameObject[obstacleCount];
            line.isGap = new bool[numberOfSlots];
            line.currentX = new float[obstacleCount];
            line.currentZ = new float[obstacleCount];
            line.currentY = spawnPoint.position.y;
            line.state = LineState.Idle;

            GameObject root = new GameObject("EpervierLine_" + i);
            root.transform.parent = transform;

            for (int j = 0; j < obstacleCount; j++)
            {
                GameObject obs = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                obs.name = "Obs_" + i + "_" + j;
                obs.transform.parent = root.transform;
                obs.transform.localScale = new Vector3(slotWidth, slotWidth, slotWidth);

                Rigidbody rb = obs.AddComponent<Rigidbody>();
                rb.isKinematic = true;

                line.currentX[j] = slotXPositions[j];
                line.currentZ[j] = spawnPoint.position.z;
                obs.transform.position = new Vector3(slotXPositions[j], spawnPoint.position.y, spawnPoint.position.z);
                line.obstacles[j] = obs;
                obs.layer = LayerMask.NameToLayer("Obstacle");
                obs.tag = "EpervierObstacle";

                if (obstacleMaterials != null && j < obstacleMaterials.Length && obstacleMaterials[j] != null)
                {
                    Renderer rend = obs.GetComponent<Renderer>();
                    if (rend != null)
                        rend.material = obstacleMaterials[j];
                }
            }

            lines[i] = line;
        }
    }

    // -------------------------------------------------------
    // PHASE CALLBACKS
    // -------------------------------------------------------

    public void OnGreenLight()
    {
        if (readyLineIndex >= 0)
            StartCoroutine(DelayedGreenLightTraverse());
        else
            ActivateNextLine();
    }

    IEnumerator DelayedGreenLightTraverse()
    {
        yield return new WaitForSeconds(greenLightDelay);
        if (readyLineIndex >= 0)
            StartTraverse(readyLineIndex);
    }

    public void OnRedLight()
    {
        if (readyLineIndex >= 0)
            StartTraverse(readyLineIndex);
    }

    // -------------------------------------------------------
    // LOGIQUE LIGNES
    // -------------------------------------------------------

    void ActivateNextLine()
    {
        int idx = GetIdleLineIndex();
        if (idx < 0)
        {
            Debug.LogWarning("[Epervier] Aucune ligne disponible dans le pool.");
            return;
        }

        EpervierLine line = lines[idx];
        AssignGaps(line);

        line.currentY = spawnPoint.position.y;
        for (int i = 0; i < line.currentZ.Length; i++)
            line.currentZ[i] = spawnPoint.position.z;

        line.state = LineState.Rearranging;
        if (line.activeCoroutine != null) StopCoroutine(line.activeCoroutine);
        line.activeCoroutine = StartCoroutine(RearrangeAndDropCoroutine(idx));
    }

    void StartTraverse(int idx)
    {
        EpervierLine line = lines[idx];
        if (line.state != LineState.Ready) return;

        readyLineIndex = -1;
        line.state = LineState.Traversing;
        if (line.activeCoroutine != null) StopCoroutine(line.activeCoroutine);
        line.activeCoroutine = StartCoroutine(TraverseCoroutine(idx));
    }

    // -------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------

    float[] GetFilledPositions(EpervierLine line)
    {
        int obstacleCount = numberOfSlots - numberOfGaps;
        float[] result = new float[obstacleCount];
        int idx = 0;
        for (int i = 0; i < numberOfSlots; i++)
        {
            if (!line.isGap[i])
                result[idx++] = slotXPositions[i];
        }
        return result;
    }

    void ApplyLineTransform(int idx)
    {
        EpervierLine line = lines[idx];
        for (int i = 0; i < line.obstacles.Length; i++)
        {
            line.obstacles[i].transform.position = new Vector3(
                line.currentX[i],
                line.currentY,
                line.currentZ[i]
            );
        }
    }

    void AssignGaps(EpervierLine line)
    {
        for (int i = 0; i < numberOfSlots; i++)
            line.isGap[i] = false;

        List<int> available = new List<int>();
        for (int i = 0; i < numberOfSlots; i++)
            available.Add(i);

        int count = Mathf.Min(numberOfGaps, numberOfSlots);
        for (int i = 0; i < count; i++)
        {
            int pick = Random.Range(0, available.Count);
            line.isGap[available[pick]] = true;
            available.RemoveAt(pick);
        }
    }

    int GetIdleLineIndex()
    {
        for (int i = 0; i < POOL_SIZE; i++)
            if (lines[i].state == LineState.Idle) return i;
        return -1;
    }

    // -------------------------------------------------------
    // COROUTINES
    // -------------------------------------------------------

    IEnumerator RearrangeAndDropCoroutine(int idx)
    {
        EpervierLine line = lines[idx];
        float[] targetX = GetFilledPositions(line);

        bool allDone = false;
        while (!allDone)
        {
            allDone = true;
            for (int i = 0; i < line.currentX.Length; i++)
            {
                line.currentX[i] = Mathf.MoveTowards(line.currentX[i], targetX[i], rearrangeSpeed * Time.deltaTime);
                if (Mathf.Abs(line.currentX[i] - targetX[i]) > 0.02f)
                    allDone = false;
            }
            ApplyLineTransform(idx);
            yield return null;
        }

        for (int i = 0; i < line.currentX.Length; i++)
            line.currentX[i] = targetX[i];

        line.activeCoroutine = StartCoroutine(DropCoroutine(idx));
    }

    IEnumerator DropCoroutine(int idx)
    {
        EpervierLine line = lines[idx];
        line.state = LineState.Dropping;
        float targetY = groundY + slotWidth / 2f;

        while (Mathf.Abs(line.currentY - targetY) > 0.02f)
        {
            line.currentY = Mathf.MoveTowards(line.currentY, targetY, dropSpeed * Time.deltaTime);
            ApplyLineTransform(idx);
            yield return null;
        }

        line.currentY = targetY;
        ApplyLineTransform(idx);
        line.state = LineState.Ready;
        readyLineIndex = idx;

        Debug.Log("[Epervier] Ligne " + idx + " prete.");

        if (isFirstDrop)
        {
            isFirstDrop = false;
            StartTraverse(idx);
        }
    }

    IEnumerator TraverseCoroutine(int idx)
    {
        EpervierLine line = lines[idx];
        float targetZ = arrivalPoint.position.z;
        int doneCount = 0;

        for (int i = 0; i < line.obstacles.Length; i++)
        {
            float delay = Random.Range(0f, maxTraverseDelay);
            StartCoroutine(TraverseSingleObstacle(line, i, targetZ, delay, () => doneCount++));
        }

        yield return new WaitUntil(() => doneCount >= line.obstacles.Length);

        line.state = LineState.Returning;
        line.activeCoroutine = StartCoroutine(ReturnCoroutine(idx));
    }

    IEnumerator TraverseSingleObstacle(EpervierLine line, int i, float targetZ, float delay, System.Action onDone)
    {
        yield return new WaitForSeconds(delay);

        while (Mathf.Abs(line.currentZ[i] - targetZ) > 0.02f)
        {
            line.currentZ[i] = Mathf.MoveTowards(line.currentZ[i], targetZ, traverseSpeed * Time.deltaTime);
            line.obstacles[i].transform.position = new Vector3(line.currentX[i], line.currentY, line.currentZ[i]);
            yield return null;
        }

        line.currentZ[i] = targetZ;
        line.obstacles[i].transform.position = new Vector3(line.currentX[i], line.currentY, targetZ);
        onDone();
    }

    IEnumerator ReturnCoroutine(int idx)
    {
        EpervierLine line = lines[idx];

        float targetY = spawnPoint.position.y;
        while (Mathf.Abs(line.currentY - targetY) > 0.02f)
        {
            line.currentY = Mathf.MoveTowards(line.currentY, targetY, returnSpeed * Time.deltaTime);
            ApplyLineTransform(idx);
            yield return null;
        }
        line.currentY = targetY;

        float targetZ = spawnPoint.position.z;
        bool allDone = false;
        while (!allDone)
        {
            allDone = true;
            for (int i = 0; i < line.obstacles.Length; i++)
            {
                line.currentZ[i] = Mathf.MoveTowards(line.currentZ[i], targetZ, returnSpeed * Time.deltaTime);
                if (Mathf.Abs(line.currentZ[i] - targetZ) > 0.02f)
                    allDone = false;
            }
            ApplyLineTransform(idx);
            yield return null;
        }

        for (int i = 0; i < line.obstacles.Length; i++)
            line.currentZ[i] = targetZ;

        ApplyLineTransform(idx);

        AssignGaps(line);
        line.state = LineState.Idle;
        line.activeCoroutine = StartCoroutine(RearrangeAndDropCoroutine(idx));
    }
}