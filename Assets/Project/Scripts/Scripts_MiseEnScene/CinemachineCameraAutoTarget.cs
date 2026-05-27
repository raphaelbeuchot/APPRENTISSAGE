using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// A placer sur une CinemachineCamera dans un prefab.
/// Auto-assigne Follow et LookAt sur le joueur au Awake.
/// Utilise : CM_Victory et CM_TransitionRoom dans le prefab TRANSITIONROOM.
/// </summary>
public class CinemachineCameraAutoTarget : MonoBehaviour
{
    private void Awake()
    {
        CinemachineCamera cm = GetComponent<CinemachineCamera>();
        if (cm == null) return;

        PlayerPhysicsMovement player = FindObjectOfType<PlayerPhysicsMovement>();
        if (player == null)
        {
            Debug.LogWarning("[CinemachineCameraAutoTarget] Joueur introuvable — Follow/LookAt non assignes.");
            return;
        }

        cm.Follow = player.transform;
        cm.LookAt = player.transform;
    }
}
