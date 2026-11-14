using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum Language { EN, FR }

// Textes du jeu en plusieurs langues.
// Source : Google Sheet "inventaire_VF_demo", exportee en CSV dans Resources/Localization/Loc.csv
// (ne pas retoucher le CSV a la main : modifier la Sheet puis reexporter).
//
//   Loc.Get("menu.newgame")            -> texte dans la langue courante
//   Loc.Get("options.timer", "On")     -> remplit {0}, {1}...
//   Loc.SetLanguage(Language.FR)       -> sauvegarde + previent les LocalizedText
//
// Texte FR vide -> texte EN. Cle inconnue -> la cle elle-meme s'affiche (oubli visible a l'ecran).
public static class Loc
{
    private const string ResourcePath = "Localization/Loc";
    private const string PrefKey = "Language";

    public static event Action LanguageChanged;

    private static Dictionary<string, string[]> table;
    private static Language current;
    private static bool languageReady;
    private static readonly HashSet<string> warnedKeys = new HashSet<string>();

    // Remise a zero au lancement du Play (utile si le Domain Reload est desactive)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        table = null;
        languageReady = false;
        LanguageChanged = null;
        warnedKeys.Clear();
    }

    public static Language Current
    {
        get { EnsureLanguage(); return current; }
    }

    public static void SetLanguage(Language language)
    {
        EnsureLanguage();
        if (language == current) return;
        current = language;
        PlayerPrefs.SetInt(PrefKey, (int)language);
        PlayerPrefs.Save();
        LanguageChanged?.Invoke();
    }

    public static void NextLanguage()
    {
        int count = Enum.GetValues(typeof(Language)).Length;
        SetLanguage((Language)(((int)Current + 1) % count));
    }

    public static bool Has(string key)
    {
        EnsureTable();
        return !string.IsNullOrEmpty(key) && table.ContainsKey(key);
    }

    public static string Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        EnsureTable();

        if (!table.TryGetValue(key, out string[] texts))
        {
            if (warnedKeys.Add(key))
                Debug.LogWarning("[Loc] Cle inconnue : '" + key + "'");
            return key;
        }

        string text = texts[(int)Current];
        if (string.IsNullOrEmpty(text)) text = texts[(int)Language.EN];
        return text ?? "";
    }

    public static string Get(string key, params object[] args)
    {
        string text = Get(key);
        if (args == null || args.Length == 0) return text;
        try { return string.Format(text, args); }
        catch (FormatException)
        {
            Debug.LogWarning("[Loc] Mauvais {0} dans le texte de '" + key + "' : " + text);
            return text;
        }
    }

    // Relit le CSV (apres un nouvel export, sans relancer le Play)
    public static void Reload()
    {
        table = null;
        warnedKeys.Clear();
        EnsureTable();
        LanguageChanged?.Invoke();
    }

    // ============================================
    // LANGUE
    // ============================================

    private static void EnsureLanguage()
    {
        if (languageReady) return;
        languageReady = true;

        if (PlayerPrefs.HasKey(PrefKey))
        {
            int saved = PlayerPrefs.GetInt(PrefKey);
            current = Enum.IsDefined(typeof(Language), saved) ? (Language)saved : Language.EN;
        }
        else
        {
            // Premier lancement : langue du systeme
            current = Application.systemLanguage == SystemLanguage.French ? Language.FR : Language.EN;
        }
    }

    // ============================================
    // CHARGEMENT DU CSV
    // ============================================

    private static void EnsureTable()
    {
        if (table != null) return;
        table = new Dictionary<string, string[]>();

        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
        {
            Debug.LogError("[Loc] Fichier Resources/" + ResourcePath + ".csv introuvable");
            return;
        }

        List<List<string>> rows = ParseCsv(asset.text);
        if (rows.Count == 0) return;

        // Colonnes reperees par leur titre : "cle" (avec ou sans accent), puis une colonne par langue
        List<string> header = rows[0];
        int keyCol = -1;
        string[] langNames = Enum.GetNames(typeof(Language));
        int[] langCols = new int[langNames.Length];
        for (int l = 0; l < langCols.Length; l++) langCols[l] = -1;

        for (int c = 0; c < header.Count; c++)
        {
            string h = header[c].Trim();
            if (keyCol < 0 && h.StartsWith("cl", StringComparison.OrdinalIgnoreCase)) keyCol = c;
            for (int l = 0; l < langNames.Length; l++)
                if (string.Equals(h, langNames[l], StringComparison.OrdinalIgnoreCase)) langCols[l] = c;
        }

        if (keyCol < 0 || langCols[(int)Language.EN] < 0)
        {
            Debug.LogError("[Loc] Colonnes 'cle' et 'EN' introuvables dans la premiere ligne du CSV");
            return;
        }

        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            string key = Cell(row, keyCol).Trim();
            if (key.Length == 0) continue;

            string[] texts = new string[langNames.Length];
            for (int l = 0; l < langNames.Length; l++)
                texts[l] = Unescape(Cell(row, langCols[l]));

            if (table.ContainsKey(key))
                Debug.LogWarning("[Loc] Cle en double dans le CSV : '" + key + "' (ligne " + (r + 1) + "), la derniere gagne");
            table[key] = texts;
        }
    }

    private static string Cell(List<string> row, int col)
    {
        return col >= 0 && col < row.Count ? row[col] : "";
    }

    // "\n" ecrit dans la Sheet -> vrai retour a la ligne
    private static string Unescape(string s)
    {
        return s.Replace("\\n", "\n");
    }

    // CSV standard : virgules, champs entre guillemets, "" = guillemet, retours a la ligne dans les guillemets
    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;
        int i = 0;
        if (text.Length > 0 && text[0] == '﻿') i = 1;

        for (; i < text.Length; i++)
        {
            char ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(ch);
            }
            else if (ch == '"') inQuotes = true;
            else if (ch == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (ch == '\r') { }
            else if (ch == '\n')
            {
                row.Add(field.ToString()); field.Clear();
                rows.Add(row); row = new List<string>();
            }
            else field.Append(ch);
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }
        return rows;
    }
}
