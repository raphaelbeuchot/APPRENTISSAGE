using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SplinePitZone))]
public class SplinePitZoneEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SplinePitZone zone = (SplinePitZone)target;

        GUILayout.Space(10);
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Mesh Generation", EditorStyles.boldLabel);

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Generate Meshes", GUILayout.Height(35)))
        {
            zone.GenerateMeshes();
            EditorUtility.SetDirty(zone);
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Clear Meshes", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog(
                "Clear Meshes",
                "Supprimer les meshes generes ?",
                "Oui",
                "Annuler"))
            {
                zone.ClearMeshes();
                EditorUtility.SetDirty(zone);
                SceneView.RepaintAll();
            }
        }

        GUILayout.Space(5);
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("A* Pathfinding", EditorStyles.boldLabel);
        GUI.backgroundColor = Color.yellow;
        if (GUILayout.Button("Apply A* Walkability", GUILayout.Height(30)))
        {
            zone.ApplyAStarWalkability();
            EditorUtility.SetDirty(zone);
        }
        GUI.backgroundColor = Color.white;
        GUILayout.EndVertical();

        GUI.backgroundColor = Color.white;
        GUILayout.EndVertical();

        DrawRoundedRectSection(zone);

        GUILayout.Space(5);
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Info", EditorStyles.boldLabel);

        UnityEngine.Splines.SplineContainer sc = zone.GetSplineContainer();
        if (sc == null)
        {
            EditorGUILayout.HelpBox("Aucun SplineContainer trouve. Ajoute un SplineContainer sur ce GO puis dessine une spline fermee.", MessageType.Warning);
        }
        else
        {
            int knots = sc.Spline.Count;
            EditorGUILayout.HelpBox("SplineContainer OK - " + knots + " knots", MessageType.Info);
        }

        GUILayout.EndVertical();
    }

    // Forme rapide : reecrit la spline en rectangle aux coins arrondis (quarts de cercle).
    // La spline reste editable a la main ensuite ; on peut toujours dessiner d'autres formes.
    static float rectWidth = 6f;   // axe X local de la spline
    static float rectLength = 4f;  // axe Z local de la spline
    static float cornerRadius = 1f;
    const int pointsPerCorner = 8; // points d'echantillonnage vises par coin arrondi

    void DrawRoundedRectSection(SplinePitZone zone)
    {
        GUILayout.Space(5);
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Forme rapide : rectangle arrondi", EditorStyles.boldLabel);

        rectWidth = Mathf.Max(0.1f, EditorGUILayout.FloatField("Largeur (X)", rectWidth));
        rectLength = Mathf.Max(0.1f, EditorGUILayout.FloatField("Longueur (Z)", rectLength));
        float maxRadius = Mathf.Min(rectWidth, rectLength) * 0.49f;
        cornerRadius = EditorGUILayout.Slider("Rayon des coins", cornerRadius, 0f, maxRadius);

        UnityEngine.Splines.SplineContainer sc = zone.GetSplineContainer();
        GUI.enabled = sc != null;
        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("Creer (remplace la spline)", GUILayout.Height(25)))
        {
            Undo.RecordObjects(new Object[] { sc, zone }, "Rectangle arrondi");
            BuildRoundedRect(sc.Spline, rectWidth, rectLength, cornerRadius);
            zone.splineSampleCount = Mathf.Max(zone.splineSampleCount, SampleCountFor(rectWidth, rectLength, cornerRadius));
            EditorUtility.SetDirty(sc);
            zone.GenerateMeshes();
            EditorUtility.SetDirty(zone);
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        GUILayout.EndVertical();
    }

    // 8 noeuds (2 par coin) en mode Mirrored, tangentes alignees sur les cotes :
    // les cotes restent droits, chaque coin est un quart de cercle (tangente = 0.5523 * rayon).
    // Rayon 0 : 4 noeuds lineaires (rectangle aux angles vifs).
    static void BuildRoundedRect(UnityEngine.Splines.Spline spline, float width, float length, float radius)
    {
        float hx = width * 0.5f;
        float hz = length * 0.5f;
        spline.Clear();

        if (radius <= 0.001f)
        {
            spline.Add(new Unity.Mathematics.float3(-hx, 0f, -hz), UnityEngine.Splines.TangentMode.Linear);
            spline.Add(new Unity.Mathematics.float3(hx, 0f, -hz), UnityEngine.Splines.TangentMode.Linear);
            spline.Add(new Unity.Mathematics.float3(hx, 0f, hz), UnityEngine.Splines.TangentMode.Linear);
            spline.Add(new Unity.Mathematics.float3(-hx, 0f, hz), UnityEngine.Splines.TangentMode.Linear);
            spline.Closed = true;
            return;
        }

        float k = 0.5523f * radius;
        // Position du noeud, direction de parcours
        Vector3[,] knots =
        {
            { new Vector3(-hx + radius, 0f, -hz), Vector3.right },
            { new Vector3(hx - radius, 0f, -hz), Vector3.right },
            { new Vector3(hx, 0f, -hz + radius), Vector3.forward },
            { new Vector3(hx, 0f, hz - radius), Vector3.forward },
            { new Vector3(hx - radius, 0f, hz), Vector3.left },
            { new Vector3(-hx + radius, 0f, hz), Vector3.left },
            { new Vector3(-hx, 0f, hz - radius), Vector3.back },
            { new Vector3(-hx, 0f, -hz + radius), Vector3.back },
        };

        for (int i = 0; i < 8; i++)
        {
            Vector3 dir = knots[i, 1] * k;
            var knot = new UnityEngine.Splines.BezierKnot(knots[i, 0], -dir, dir, Quaternion.identity);
            spline.Add(knot, UnityEngine.Splines.TangentMode.Mirrored);
        }
        spline.Closed = true;
    }

    // L'echantillonnage est regulier sur tout le perimetre : il faut assez de points
    // pour que chaque petit arrondi en recoive environ pointsPerCorner.
    static int SampleCountFor(float width, float length, float radius)
    {
        if (radius <= 0.001f) return 32;
        float perimeter = 2f * (width + length) - 8f * radius + 2f * Mathf.PI * radius;
        float spacing = (Mathf.PI * radius * 0.5f) / pointsPerCorner;
        return Mathf.Clamp(Mathf.CeilToInt(perimeter / spacing), 32, 512);
    }
}