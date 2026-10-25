#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections;

// Panneau de playtest : F1 ouvre/ferme (jeu en pause pendant l'ouverture).
// S'installe tout seul au lancement (rien a poser dans les scenes), editeur et builds de dev seulement.
// Options retenues d'un Play a l'autre (PlayerPrefs "DEV_...") ; badge DEV a l'ecran tant qu'une option est active.
// Ajouter une option : un champ, sa ligne dans DrawPanel, sa sauvegarde dans LoadPrefs/SavePrefs.
public class DevPanel : MonoBehaviour
{
    private const string PREFS_FORCE_LOADOUT = "DEV_ForceLoadout";
    private const string PREFS_LOADOUT_BROOM = "DEV_LoadoutBroom";
    private const string PREFS_LOADOUT_SPRAY = "DEV_LoadoutSpray";
    private const string PREFS_INVINCIBLE = "DEV_Invincible";
    private const string PREFS_NO_DETECTION = "DEV_NoSentinelDetection";
    private const string PREFS_OPEN_DOOR = "DEV_OpenGoalDoor";

    private bool open;
    private float timeScaleBeforeOpen = 1f;

    // Loadout
    private bool forceLoadout;
    private bool loadoutBroom = true;
    private bool loadoutSpray = true;

    // Joueur / sentinelle (DevFlags, tous les joueurs, solo et multi)
    private bool invincible;
    private bool noDetection;

    // Niveau
    private bool openDoor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject go = new GameObject("[DevPanel]");
        DontDestroyOnLoad(go);
        go.AddComponent<DevPanel>();
    }

    void Awake()
    {
        LoadPrefs();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        StartCoroutine(ApplyNextFrame());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(ApplyNextFrame());
    }

    // Une frame plus tard : apres le Start de PlayerLoadout, qui applique le loadout du niveau
    private IEnumerator ApplyNextFrame()
    {
        yield return null;
        if (forceLoadout) ApplyLoadout();
        if (openDoor) OpenGoalDoors();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            SetOpen(!open);
    }

    private void SetOpen(bool value)
    {
        open = value;
        if (open)
        {
            timeScaleBeforeOpen = Time.timeScale;
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = timeScaleBeforeOpen;
        }
    }

    private bool AnyOptionActive()
    {
        return forceLoadout || invincible || noDetection || openDoor;
    }

    void OnGUI()
    {
        float scale = Mathf.Max(1f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        if (!open)
        {
            if (AnyOptionActive())
                GUI.Box(new Rect(10, 10, 210, 24), "DEV : options actives (F1)");
            return;
        }

        GUILayout.BeginArea(new Rect(10, 10, 280, 400), GUI.skin.box);
        GUILayout.Label("DEV PANEL  —  F1 pour fermer (jeu en pause)");
        DrawPanel();
        GUILayout.EndArea();
    }

    private void DrawPanel()
    {
        GUILayout.Space(6);
        GUILayout.Label("— Loadout —");

        bool newForce = GUILayout.Toggle(forceLoadout, " Forcer le loadout");
        GUI.enabled = newForce;
        bool newBroom = GUILayout.Toggle(loadoutBroom, "   Balai");
        bool newSpray = GUILayout.Toggle(loadoutSpray, "   Spray");
        GUI.enabled = true;

        if (newForce != forceLoadout || newBroom != loadoutBroom || newSpray != loadoutSpray)
        {
            bool forceChanged = newForce != forceLoadout;
            forceLoadout = newForce;
            loadoutBroom = newBroom;
            loadoutSpray = newSpray;
            SavePrefs();

            if (forceLoadout) ApplyLoadout();
            else if (forceChanged) RestoreLoadout();
        }

        GUILayout.Space(6);
        GUILayout.Label("— Joueur —");
        bool newInvincible = GUILayout.Toggle(invincible, " Invincible");
        bool newNoDetection = GUILayout.Toggle(noDetection, " Zero detection sentinelle");

        GUILayout.Space(6);
        GUILayout.Label("— Niveau —");
        bool newOpenDoor = GUILayout.Toggle(openDoor, " Porte ouverte (sans cle)");

        // Action ponctuelle, pas une option retenue
        LevelStatsTracker stats = LevelStatsTracker.Instance;
        GUI.enabled = stats != null;
        string attemptsLabel = stats != null ? " (actuel : " + stats.AttemptCount + ")" : "";
        if (GUILayout.Button("Essais remis a 1" + attemptsLabel))
            stats.SetCurrentRunAsFirstAttempt();
        GUI.enabled = true;

        if (newInvincible != invincible || newNoDetection != noDetection || newOpenDoor != openDoor)
        {
            bool doorJustOpened = newOpenDoor && !openDoor;
            invincible = newInvincible;
            noDetection = newNoDetection;
            openDoor = newOpenDoor;
            ApplyFlags();
            SavePrefs();
            if (doorJustOpened) OpenGoalDoors();
        }
    }

    private void ApplyFlags()
    {
        DevFlags.Invincible = invincible;
        DevFlags.SentinelIgnoresPlayer = noDetection;
    }

    // Decocher en cours de niveau ne referme pas la porte (retour a la normale au prochain lancement)
    private void OpenGoalDoors()
    {
        GoalDoorNew[] doors = FindObjectsByType<GoalDoorNew>(FindObjectsSortMode.None);
        foreach (GoalDoorNew door in doors)
            door.SetActive(true);
        if (doors.Length > 0)
            Debug.Log("[DevPanel] Porte de fin ouverte.");
    }

    // Solo uniquement : avec plusieurs joueurs (multi), on ne touche a rien
    private PlayerLoadout FindSoloLoadout()
    {
        PlayerLoadout[] loadouts = FindObjectsByType<PlayerLoadout>(FindObjectsSortMode.None);
        return loadouts.Length == 1 ? loadouts[0] : null;
    }

    private void ApplyLoadout()
    {
        PlayerLoadout loadout = FindSoloLoadout();
        if (loadout == null) return;
        loadout.ApplyLoadout(loadoutBroom, loadoutSpray);
        Debug.Log("[DevPanel] Loadout force : balai " + (loadoutBroom ? "ON" : "OFF") + ", spray " + (loadoutSpray ? "ON" : "OFF") + ".");
    }

    private void RestoreLoadout()
    {
        PlayerLoadout loadout = FindSoloLoadout();
        if (loadout == null) return;
        loadout.RestoreLevelLoadout();
        Debug.Log("[DevPanel] Loadout du niveau retabli.");
    }

    private void LoadPrefs()
    {
        forceLoadout = PlayerPrefs.GetInt(PREFS_FORCE_LOADOUT, 0) == 1;
        loadoutBroom = PlayerPrefs.GetInt(PREFS_LOADOUT_BROOM, 1) == 1;
        loadoutSpray = PlayerPrefs.GetInt(PREFS_LOADOUT_SPRAY, 1) == 1;
        invincible = PlayerPrefs.GetInt(PREFS_INVINCIBLE, 0) == 1;
        noDetection = PlayerPrefs.GetInt(PREFS_NO_DETECTION, 0) == 1;
        openDoor = PlayerPrefs.GetInt(PREFS_OPEN_DOOR, 0) == 1;
        ApplyFlags();
    }

    private void SavePrefs()
    {
        PlayerPrefs.SetInt(PREFS_FORCE_LOADOUT, forceLoadout ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_LOADOUT_BROOM, loadoutBroom ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_LOADOUT_SPRAY, loadoutSpray ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_INVINCIBLE, invincible ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_NO_DETECTION, noDetection ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_OPEN_DOOR, openDoor ? 1 : 0);
        PlayerPrefs.Save();
    }
}
#endif
