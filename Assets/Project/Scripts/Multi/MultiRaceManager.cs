using System;
using UnityEngine;

// Course multi (Versus) : enregistre le premier joueur qui atteint la porte.
// Le verrou "premiere arrivee" est deja dans GoalDoorNew (hasBeenReached), ici on ne fait que retenir le gagnant.
// V1 volontairement minimale : pas d'ecran de fin, le perdant reste jouable.
public class MultiRaceManager : MonoBehaviour
{
    [SerializeField] private GoalDoorNew goalDoor;

    public GameObject Winner { get; private set; }
    public event Action<GameObject> OnRaceWon;

    private void Start()
    {
        if (goalDoor == null)
            goalDoor = FindFirstObjectByType<GoalDoorNew>();

        if (goalDoor == null)
        {
            Debug.LogWarning("[MultiRace] Aucune GoalDoorNew trouvee dans la scene");
            return;
        }

        goalDoor.OnPlayerReached += HandlePlayerReached;
    }

    private void OnDestroy()
    {
        if (goalDoor != null)
            goalDoor.OnPlayerReached -= HandlePlayerReached;
    }

    private void HandlePlayerReached(GameObject player)
    {
        if (Winner != null) return;

        Winner = player;
        Debug.Log($"[MultiRace] {player.name} gagne la course");
        OnRaceWon?.Invoke(player);
    }
}
