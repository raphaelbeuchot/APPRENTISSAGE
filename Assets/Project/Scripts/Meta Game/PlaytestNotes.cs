#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System;
using System.IO;
using System.Text;

// Notes de playtest : F2 met le jeu en pause et ouvre un champ de texte ; Entree enregistre, Echap annule.
// Chaque note est ajoutee a NOTES_PLAYTEST.md (racine du repo en editeur, persistentDataPath en build de dev)
// avec la scene jouee, la date et la position du joueur au moment de l'appui sur F2.
// S'installe tout seul au lancement (rien a poser dans les scenes), editeur et builds de dev seulement.
public class PlaytestNotes : MonoBehaviour
{
    private const string FILE_NAME = "NOTES_PLAYTEST.md";
    private const string FIELD_NAME = "PlaytestNoteField";
    private const float CONFIRM_DURATION = 1.5f;

    private bool open;
    private bool focusPending;
    private string text = "";
    private float timeScaleBeforeOpen = 1f;

    // Contexte capture a l'ouverture (pas a la validation)
    private string noteScene;
    private string noteDate;
    private string notePosition;

    private string confirmMessage;
    private float confirmUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject go = new GameObject("[PlaytestNotes]");
        DontDestroyOnLoad(go);
        go.AddComponent<PlaytestNotes>();
    }

    void Update()
    {
        if (!open && Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            Open();
    }

    void OnDestroy()
    {
        // Ne jamais laisser le clavier coupe (arret du Play pendant la saisie)
        if (open) SetGameKeyboard(true);
    }

    private void Open()
    {
        noteScene = SceneManager.GetActiveScene().name;
        noteDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        notePosition = player != null ? FormatPosition(player.transform.position) : "joueur introuvable";

        open = true;
        focusPending = true;
        text = "";
        timeScaleBeforeOpen = Time.timeScale;
        Time.timeScale = 0f;
        // Le clavier est coupe pour le jeu pendant la saisie (sinon les lettres declenchent les actions, Echap la pause).
        // Le champ de texte (IMGUI) recoit toujours les touches : il ne passe pas par l'Input System.
        SetGameKeyboard(false);
    }

    private void Close()
    {
        open = false;
        Time.timeScale = timeScaleBeforeOpen;
        SetGameKeyboard(true);
    }

    private void SetGameKeyboard(bool enabled)
    {
        if (Keyboard.current == null) return;
        if (enabled) InputSystem.EnableDevice(Keyboard.current);
        else InputSystem.DisableDevice(Keyboard.current);
    }

    private static string FormatPosition(Vector3 p)
    {
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, "pos ({0:F1}, {1:F1}, {2:F1})", p.x, p.y, p.z);
    }

    private void Save()
    {
        string note = text.Trim();
        if (note.Length == 0) return;

        string path = GetFilePath();
        StringBuilder sb = new StringBuilder();
        if (!File.Exists(path))
            sb.Append("# Notes de playtest\n\n> Prises en jeu avec F2 (PlaytestNotes). Boite de reception : a trier dans RAPPORT_PLAYTEST.md.\n\n");
        sb.Append("- [ ] **").Append(noteScene).Append("** — ").Append(noteDate).Append(" — ").Append(notePosition).Append('\n');
        sb.Append("  ").Append(note).Append('\n');

        try
        {
            File.AppendAllText(path, sb.ToString(), new UTF8Encoding(false));
            confirmMessage = "Note enregistree";
            Debug.Log("[PlaytestNotes] Note enregistree dans " + path);
        }
        catch (Exception e)
        {
            confirmMessage = "ERREUR : note non enregistree (voir console)";
            Debug.LogError("[PlaytestNotes] Ecriture impossible dans " + path + " : " + e.Message);
        }
        confirmUntil = Time.realtimeSinceStartup + CONFIRM_DURATION;
    }

    // Editeur : racine du repo (parent du dossier Assets). Build de dev : dossier de sauvegarde du jeu.
    private static string GetFilePath()
    {
#if UNITY_EDITOR
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, FILE_NAME);
#else
        return Path.Combine(Application.persistentDataPath, FILE_NAME);
#endif
    }

    void OnGUI()
    {
        float scale = Mathf.Max(1f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;

        if (!open)
        {
            if (Time.realtimeSinceStartup < confirmUntil)
                GUI.Box(new Rect(width * 0.5f - 130, 40, 260, 26), confirmMessage);
            return;
        }

        // Entree / Echap lus avant le champ (sinon le champ les consomme)
        Event e = Event.current;
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                e.Use();
                Save();
                Close();
                return;
            }
            if (e.keyCode == KeyCode.Escape)
            {
                e.Use();
                Close();
                return;
            }
        }

        GUILayout.BeginArea(new Rect(width * 0.5f - 300, 40, 600, 90), GUI.skin.box);
        GUILayout.Label("NOTE  —  " + noteScene + "  —  " + notePosition + "    (Entree : enregistrer, Echap : annuler)");
        GUI.SetNextControlName(FIELD_NAME);
        text = GUILayout.TextField(text, GUILayout.Height(28));
        GUILayout.EndArea();

        if (focusPending)
        {
            GUI.FocusControl(FIELD_NAME);
            focusPending = false;
        }
    }
}
#endif
