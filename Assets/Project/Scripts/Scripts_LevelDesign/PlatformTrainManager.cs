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

    private float currentSpeed = 0f;
    private float splineLength = 0f;
    private Vector3[] lastPositions;
    private PlayerPhysicsMovement player;

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
        for (int i = 0; i < platforms.Count; i++)
        {
            lastPositions[i] = GetSplinePosition(platforms[i].currentProgress);
            platforms[i].transform.position = lastPositions[i];
        }

        currentSpeed = trainSpeed;
        player = FindFirstObjectByType<PlayerPhysicsMovement>();
    }

    void Update()
    {
        bool holdingX = PlayerInputManager.Instance.InteractHeld && IsPlayerOnTrain();

        float targetSpeed = holdingX ? 0f : trainSpeed;
        float inertia = holdingX ? stopInertia : startInertia;
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, inertia * Time.deltaTime);

        float progressIncrement = (currentSpeed / splineLength) * Time.deltaTime;

        for (int i = 0; i < platforms.Count; i++)
        {
            platforms[i].currentProgress += progressIncrement;

            if (platforms[i].currentProgress > 1f)
                platforms[i].currentProgress -= 1f;
            else if (platforms[i].currentProgress < 0f)
                platforms[i].currentProgress += 1f;

            Vector3 newPosition = GetSplinePosition(platforms[i].currentProgress);
            platforms[i].transform.position = newPosition;

            Vector3 velocity = (newPosition - lastPositions[i]) / Time.deltaTime;
            platforms[i].SetVelocity(velocity);
            lastPositions[i] = newPosition;
        }
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