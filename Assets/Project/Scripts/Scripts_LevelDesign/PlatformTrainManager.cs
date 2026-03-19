using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

public class PlatformTrainManager : MonoBehaviour
{
    [Header("Spline")]
    [SerializeField] private SplineContainer splineContainer;

    [Header("Platforms")]
    [SerializeField] private List<PlatformTrainCar> platforms = new List<PlatformTrainCar>();

    [Header("Speed")]
    [SerializeField] private float trainSpeed = 2f;
    [SerializeField] private float stopInertia = 3f;
    [SerializeField] private float startInertia = 25f;

    [Header("Materials")]
    [SerializeField] private Material lavaTrainBasic;
    [SerializeField] private Material lavaTrainOn;
    [SerializeField] private Material lavaTrainOff;

    [Header("Disc Scale")]
    [SerializeField] private float discScaleBase = 0.8f;
    [SerializeField] private float discScaleOnPlatform = 0.7f;
    [SerializeField] private float discScaleHoldingX = 0.6f;
    [SerializeField] private float discLerpSpeed = 5f;

    private float currentSpeed = 0f;
    private float splineLength = 0f;
    private Vector3[] lastPositions;
    private float[] currentDiscScales;
    private PlayerPhysicsMovement player;
    private bool hasStarted = false;

    void Start()
    {
        if (splineContainer == null)
        {
            Debug.LogError("[PlatformTrainManager] SplineContainer non assigne !");
            enabled = false;
            return;
        }

        splineLength = splineContainer.Spline.GetLength();

        if (platforms.Count == 0)
        {
            Debug.LogError("[PlatformTrainManager] Aucune plateforme assignee !");
            enabled = false;
            return;
        }

        float spacing = 1f / platforms.Count;
        for (int i = 0; i < platforms.Count; i++)
        {
            platforms[i].currentProgress = spacing * i;
        }

        lastPositions = new Vector3[platforms.Count];
        currentDiscScales = new float[platforms.Count];

        for (int i = 0; i < platforms.Count; i++)
        {
            lastPositions[i] = GetSplinePosition(platforms[i].currentProgress);
            platforms[i].transform.position = lastPositions[i];
            currentDiscScales[i] = discScaleBase;
        }

        currentSpeed = 0f;
        player = FindFirstObjectByType<PlayerPhysicsMovement>();
    }

    void FixedUpdate()
    {
        if (!hasStarted && IsPlayerOnTrain())
            StartTrain();

        if (!hasStarted) return;

        bool playerOnTrain = IsPlayerOnTrain();
        bool holdingX = PlayerInputManager.Instance.InteractHeld && playerOnTrain;

        float targetSpeed = holdingX ? 0f : trainSpeed;
        float inertia = holdingX ? stopInertia : startInertia;
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, inertia * Time.fixedDeltaTime);

        float progressIncrement = (currentSpeed / splineLength) * Time.fixedDeltaTime;

        PlatformTrainCar playerCar = playerOnTrain ? player.GetCurrentPlatform() as PlatformTrainCar : null;

        for (int i = 0; i < platforms.Count; i++)
        {
            platforms[i].currentProgress += progressIncrement;
            if (platforms[i].currentProgress > 1f)
                platforms[i].currentProgress -= 1f;
            else if (platforms[i].currentProgress < 0f)
                platforms[i].currentProgress += 1f;

            Vector3 newPosition = GetSplinePosition(platforms[i].currentProgress);
            platforms[i].transform.position = newPosition;

            Vector3 velocity = (newPosition - lastPositions[i]) / Time.fixedDeltaTime;
            platforms[i].SetVelocity(velocity);
            lastPositions[i] = newPosition;

            if (platforms[i].lavaTrainFloor == null) continue;

            // Material
            if (platforms[i] == playerCar)
            {
                platforms[i].lavaTrainFloor.sharedMaterial = holdingX ? lavaTrainOff : lavaTrainOn;
            }
            else
            {
                platforms[i].lavaTrainFloor.sharedMaterial = lavaTrainBasic;
            }

            // Scale disque
            float targetScale;
            if (platforms[i] == playerCar)
                targetScale = holdingX ? discScaleHoldingX : discScaleOnPlatform;
            else
                targetScale = discScaleBase;

            currentDiscScales[i] = Mathf.Lerp(currentDiscScales[i], targetScale, discLerpSpeed * Time.fixedDeltaTime);

            Transform discTransform = platforms[i].lavaTrainFloor.transform;
            Vector3 s = discTransform.localScale;
            discTransform.localScale = new Vector3(currentDiscScales[i], s.y, currentDiscScales[i]);
        }
    }

    public void StartTrain()
    {
        hasStarted = true;
    }

    private Vector3 GetSplinePosition(float progress)
    {
        return splineContainer.EvaluatePosition(progress);
    }

    private bool IsPlayerOnTrain()
    {
        if (player == null) return false;
        return player.GetCurrentPlatform() is PlatformTrainCar;
    }
}