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
    private bool isStopped = false;
    private float splineLength = 0f;

    // Pour calculer la velocite de chaque plateforme
    private Vector3[] lastPositions;

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

        // Repartir les plateformes equitablement sur le spline
        float spacing = 1f / platforms.Count;
        for (int i = 0; i < platforms.Count; i++)
        {
            platforms[i].currentProgress = spacing * i;
        }

        // Initialiser les positions de reference pour le calcul de velocite
        lastPositions = new Vector3[platforms.Count];
        for (int i = 0; i < platforms.Count; i++)
        {
            lastPositions[i] = GetSplinePosition(platforms[i].currentProgress);
            platforms[i].transform.position = lastPositions[i];
        }

        currentSpeed = trainSpeed;
    }

    void Update()
    {
        // Lecture input X
        bool holdingX = PlayerInputManager.Instance.InteractHeld;

        // Gestion inertie
        float targetSpeed = holdingX ? 0f : trainSpeed;
        float inertia = holdingX ? stopInertia : startInertia;
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, inertia * Time.deltaTime);

        // Avancer toutes les plateformes
        float progressIncrement = (currentSpeed / splineLength) * Time.deltaTime;

        for (int i = 0; i < platforms.Count; i++)
        {
            platforms[i].currentProgress += progressIncrement;

            // Boucle
            if (platforms[i].currentProgress > 1f)
                platforms[i].currentProgress -= 1f;
            else if (platforms[i].currentProgress < 0f)
                platforms[i].currentProgress += 1f;

            // Mettre a jour position
            Vector3 newPosition = GetSplinePosition(platforms[i].currentProgress);
            platforms[i].transform.position = newPosition;

            // Calculer et envoyer la velocite
            Vector3 velocity = (newPosition - lastPositions[i]) / Time.deltaTime;
            platforms[i].SetVelocity(velocity);
            lastPositions[i] = newPosition;
        }
    }

    private Vector3 GetSplinePosition(float progress)
    {
        Vector3 position = splineContainer.EvaluatePosition(progress);

        // Offset Y selon collider comme dans RoamingObstacle
        // A ajuster selon la taille de tes plateformes dans l'Inspector
        return position;
    }

    // Petit helper car InteractPressed se reset chaque frame dans LateUpdate
    // On a besoin de detecter le maintien, pas juste le tap
    private bool IsInteractHeld()
    {
        return UnityEngine.InputSystem.Keyboard.current != null &&
               UnityEngine.InputSystem.Keyboard.current.eKey.isPressed ||
               UnityEngine.InputSystem.Gamepad.current != null &&
               UnityEngine.InputSystem.Gamepad.current.buttonSouth.isPressed;
    }
}