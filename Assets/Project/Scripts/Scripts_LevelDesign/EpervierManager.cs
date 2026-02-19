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

    [Header("Player Escape")]
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private float escapeRaycastHeight = 2f;
    [SerializeField] private float escapeForceDuration = 0.5f;
    [SerializeField] private float escapeForce = 10f;
    [SerializeField] private PlayerPhysicsMovement playerMovement;

    private bool isEscaping = false;

    [SerializeField] private Material[] obstacleMaterials;
    private enum LineState { Idle, Rearranging, Dropping, Ready, Traversing, Returning }

    private class EpervierLine
    {
        public GameObject[] obstacles;
        public bool[] isGap;
        public LineState state = LineState.Idle;
        public float currentY;
        public float currentZ;
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
        if (isEscaping || playerRigidbody == null) return;

        Vector3 rayOrigin = playerRigidbody.position;
        RaycastHit hit;

        if (Physics.Raycast(rayOrigin, Vector3.up, out hit, escapeRaycastHeight))
        {
            // Verifier que l'objet touche est un obstacle en phase Dropping
            for (int i = 0; i < POOL_SIZE; i++)
            {
                if (lines[i].state != LineState.Dropping) continue;

                foreach (GameObject obs in lines[i].obstacles)
                {
                    if (obs == hit.collider.gameObject)
                    {
                        StartCoroutine(EscapeCoroutine()); return;
                    }
                }
            }
        }
    }

    IEnumerator EscapeCoroutine()
    {
        isEscaping = true;

        if (playerMovement != null)
            playerMovement.ApplyProgressivePush(Vector3.forward, escapeForce, escapeForceDuration);

        yield return new WaitForSeconds(escapeForceDuration);

        isEscaping = false;
    }
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
            line.currentY = spawnPoint.position.y;
            line.currentZ = spawnPoint.position.z;
            line.state = LineState.Idle;

            GameObject root = new GameObject("EpervierLine_" + i);
            root.transform.parent = transform;

            for (int j = 0; j < obstacleCount; j++)
            {
                GameObject obs = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obs.name = "Obs_" + i + "_" + j;
                obs.transform.parent = root.transform;
                obs.transform.localScale = new Vector3(slotWidth, obstacleHeight, obstacleDepth);

                Rigidbody rb = obs.AddComponent<Rigidbody>();
                rb.isKinematic = true;

                line.currentX[j] = slotXPositions[j];
                obs.transform.position = new Vector3(slotXPositions[j], spawnPoint.position.y, spawnPoint.position.z);
                line.obstacles[j] = obs;
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
        {
            // Une ligne est prete : on la traverse apres le delai
            StartCoroutine(DelayedGreenLightTraverse());
        }
        else
        {
            // Premiere fois ou ligne pas encore prete : on active
            ActivateNextLine();
        }
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
        line.currentZ = spawnPoint.position.z;

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
                line.currentZ
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
        float targetY = groundY + obstacleHeight / 2f;

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
    }

    IEnumerator TraverseCoroutine(int idx)
    {
        EpervierLine line = lines[idx];
        float targetZ = arrivalPoint.position.z;

        while (Mathf.Abs(line.currentZ - targetZ) > 0.02f)
        {
            line.currentZ = Mathf.MoveTowards(line.currentZ, targetZ, traverseSpeed * Time.deltaTime);
            ApplyLineTransform(idx);
            yield return null;
        }

        line.currentZ = targetZ;
        ApplyLineTransform(idx);
        line.state = LineState.Returning;
        line.activeCoroutine = StartCoroutine(ReturnCoroutine(idx));
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
        while (Mathf.Abs(line.currentZ - targetZ) > 0.02f)
        {
            line.currentZ = Mathf.MoveTowards(line.currentZ, targetZ, returnSpeed * Time.deltaTime);
            ApplyLineTransform(idx);
            yield return null;
        }
        line.currentZ = targetZ;
        ApplyLineTransform(idx);

        // Des que revenus en haut au fond : nouveaux gaps et reaarrangement immediat
        AssignGaps(line);
        line.state = LineState.Idle;
        line.activeCoroutine = StartCoroutine(RearrangeAndDropCoroutine(idx));
    }
}