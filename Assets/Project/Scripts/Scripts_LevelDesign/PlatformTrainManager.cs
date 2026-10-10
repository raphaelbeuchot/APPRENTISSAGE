using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

public class PlatformTrainManager : MonoBehaviour
{
    [Header("Spline")]
    [Tooltip("Le premier wagon part du point 0 de la spline, le train avance dans le sens de la spline")]
    [SerializeField] private SplineContainer splineContainer;

    [Header("Settings")]
    [Tooltip("Reglages partages (prefab du wagon, vitesses, sons, materiaux)")]
    [SerializeField] private LavaTrainSettings settings;

    [Header("Platforms")]
    [Tooltip("Nombre de wagons, crees au Start et repartis a intervalles egaux sur la spline")]
    [SerializeField, Min(1)] private int platformCount = 5;

    private List<PlatformTrainCar> platforms = new List<PlatformTrainCar>();

    private bool messageShown = false;
    private bool playerWasOnTrain = false;

    private AudioSource trainAudioSource;
    private bool wasHoldingX = false;
    private float holdXTimer = 0f;
    private bool brakeSoundPlayed = false;

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

        if (settings == null || settings.platformPrefab == null)
        {
            Debug.LogError("[PlatformTrainManager] LavaTrainSettings (ou son prefab de wagon) non assigne !");
            enabled = false;
            return;
        }

        splineLength = splineContainer.Spline.GetLength();

        float spacing = 1f / platformCount;
        for (int i = 0; i < platformCount; i++)
        {
            PlatformTrainCar car = Instantiate(settings.platformPrefab, GetSplinePosition(spacing * i), settings.platformPrefab.transform.rotation, transform);
            car.name = settings.platformPrefab.name + " (" + i + ")";
            car.currentProgress = spacing * i;
            platforms.Add(car);
        }

        lastPositions = new Vector3[platforms.Count];
        currentDiscScales = new float[platforms.Count];

        for (int i = 0; i < platforms.Count; i++)
        {
            lastPositions[i] = platforms[i].transform.position;
            currentDiscScales[i] = settings.discScaleBase;
        }

        currentSpeed = 0f;
        player = FindFirstObjectByType<PlayerPhysicsMovement>();

        trainAudioSource = gameObject.AddComponent<AudioSource>();
        trainAudioSource.spatialBlend = 0f;
        trainAudioSource.playOnAwake = false;
    }

    void FixedUpdate()
    {
        if (!hasStarted && IsPlayerOnTrain())
            StartTrain();

        if (!hasStarted) return;

        bool playerOnTrain = IsPlayerOnTrain();
        bool holdingX = PlayerInputManager.Instance.InteractHeld && playerOnTrain;

        // Message
        bool justBoarded = playerOnTrain && !playerWasOnTrain;

        if (justBoarded)
        {
            if (!settings.showMessageOnce || !messageShown)
            {
                CommentPanel.Show(settings.holdMessage);
                messageShown = true;
            }

            if (settings.startSound != null)
                trainAudioSource.PlayOneShot(settings.startSound);
        }

        playerWasOnTrain = playerOnTrain;
        // Son brake : apres 0.1s de maintien
        if (holdingX)
        {
            holdXTimer += Time.fixedDeltaTime;
            if (!brakeSoundPlayed && holdXTimer >= 0.1f)
            {
                if (settings.brakeSound != null)
                    trainAudioSource.PlayOneShot(settings.brakeSound);
                brakeSoundPlayed = true;
            }
        }
        else
        {
            if (wasHoldingX && playerOnTrain)
            {
                if (settings.releaseSound != null)
                    trainAudioSource.PlayOneShot(settings.releaseSound);
            }
            holdXTimer = 0f;
            brakeSoundPlayed = false;
        }

        wasHoldingX = holdingX;
        float targetSpeed = holdingX ? 0f : settings.trainSpeed;
        float inertia = holdingX ? settings.stopInertia : settings.startInertia;
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
                platforms[i].lavaTrainFloor.sharedMaterial = holdingX ? settings.lavaTrainOff : settings.lavaTrainOn;
            }
            else
            {
                platforms[i].lavaTrainFloor.sharedMaterial = settings.lavaTrainBasic;
            }

            // Scale disque
            float targetScale;
            if (platforms[i] == playerCar)
                targetScale = holdingX ? settings.discScaleHoldingX : settings.discScaleOnPlatform;
            else
                targetScale = settings.discScaleBase;

            currentDiscScales[i] = Mathf.Lerp(currentDiscScales[i], targetScale, settings.discLerpSpeed * Time.fixedDeltaTime);

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

    // Apercu en edition : la ou chaque wagon sera cree au Start (meme calcul que Start)
    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        if (splineContainer == null || platformCount < 1) return;

        // Forme du wagon : les meshes du prefab (plaque + disque), relatifs a sa racine
        PlatformTrainCar prefab = settings != null ? settings.platformPrefab : null;
        MeshFilter[] meshes = prefab != null ? prefab.GetComponentsInChildren<MeshFilter>() : new MeshFilter[0];
        Vector3 rootPosition = prefab != null ? prefab.transform.position : Vector3.zero;

        float spacing = 1f / platformCount;
        for (int i = 0; i < platformCount; i++)
        {
            Vector3 position = GetSplinePosition(spacing * i);
            Gizmos.color = i == 0 ? new Color(1f, 0.9f, 0f, 0.6f) : new Color(1f, 0.5f, 0f, 0.6f);

            if (meshes.Length == 0)
            {
                Gizmos.DrawCube(position, new Vector3(1f, 0.1f, 1f));
                continue;
            }

            foreach (MeshFilter mf in meshes)
            {
                if (mf.sharedMesh == null) continue;
                Vector3 offset = mf.transform.position - rootPosition;
                Gizmos.DrawMesh(mf.sharedMesh, position + offset, mf.transform.rotation, mf.transform.lossyScale);
            }
        }
    }
}
