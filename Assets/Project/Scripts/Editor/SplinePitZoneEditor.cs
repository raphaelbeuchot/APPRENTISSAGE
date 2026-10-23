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

        GUI.backgroundColor = Color.white;
        GUILayout.Space(5);
        // Pas de bouton : la grille est rescannee au lancement, la fosse se reapplique dans son Start()
        GUILayout.Label("A* : la fosse rend la grille non marchable automatiquement au lancement.", EditorStyles.miniLabel);
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
    // Les reglages sont memorises sur la fosse (champs quickRect* de SplinePitZone).
    const int pointsPerCorner = 8; // points d'echantillonnage vises par quart de tour
    const float maxTotalTilt = 89f; // inclinaison max cumulee des cotes : au-dela, un coin tournerait a l'envers

    void DrawRoundedRectSection(SplinePitZone zone)
    {
        GUILayout.Space(5);
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Forme rapide : rectangle arrondi", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        float rectWidth = Mathf.Max(0.1f, EditorGUILayout.FloatField("Largeur (X)", zone.quickRectWidth));
        float rectLength = Mathf.Max(0.1f, EditorGUILayout.FloatField("Longueur (Z)", zone.quickRectLength));
        float maxRadius = Mathf.Min(rectWidth, rectLength) * 0.49f;
        float cornerRadius = EditorGUILayout.Slider("Rayon des coins", zone.quickRectRadius, 0f, maxRadius);

        float chordX = rectWidth - 2f * cornerRadius;
        float chordZ = rectLength - 2f * cornerRadius;
        // Nommes selon la direction du gonflement : "Bombe X" pousse les cotes gauche/droite vers -X / +X
        float bulgeZ = EditorGUILayout.Slider(new GUIContent("Bombe X", "Gonflement vers -X / +X : milieu des cotes gauche et droite (m)"), zone.quickRectBulgeZ, 0f, chordZ * 0.5f);
        float bulgeX = EditorGUILayout.Slider(new GUIContent("Bombe Z", "Gonflement vers -Z / +Z : milieu des cotes bas et haut (m)"), zone.quickRectBulgeX, 0f, chordX * 0.5f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(zone, "Forme rapide");
            zone.quickRectWidth = rectWidth;
            zone.quickRectLength = rectLength;
            zone.quickRectRadius = cornerRadius;
            zone.quickRectBulgeX = bulgeX;
            zone.quickRectBulgeZ = bulgeZ;
            EditorUtility.SetDirty(zone);
        }

        float tiltX = SideTilt(bulgeX, chordX);
        float tiltZ = SideTilt(bulgeZ, chordZ);
        if (tiltX + tiltZ > maxTotalTilt)
            EditorGUILayout.HelpBox("Bombes trop forts pour ces coins : ils seront reduits a la creation pour garder une forme convexe.", MessageType.Warning);

        UnityEngine.Splines.SplineContainer sc = zone.GetSplineContainer();
        GUI.enabled = sc != null;
        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("Creer (remplace la spline)", GUILayout.Height(25)))
        {
            Undo.RecordObjects(new Object[] { sc, zone }, "Rectangle arrondi");
            BuildRoundedRect(sc.Spline, rectWidth, rectLength, cornerRadius, tiltX, tiltZ);
            zone.splineSampleCount = Mathf.Max(zone.splineSampleCount, SampleCountFor(sc.Spline));
            EditorUtility.SetDirty(sc);
            zone.GenerateMeshes();
            EditorUtility.SetDirty(zone);
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        GUILayout.EndVertical();
    }

    // Inclinaison (degres) de la tangente en bout d'un cote bombe en arc de cercle :
    // fleche s sur une corde L -> demi-angle au centre = 2 * atan(2s / L).
    static float SideTilt(float bulge, float chord)
    {
        if (bulge <= 0.0001f || chord <= 0.0001f) return 0f;
        return 2f * Mathf.Atan(2f * bulge / chord) * Mathf.Rad2Deg;
    }

    // Parcours anti-horaire vu du dessus (bas, droite, haut, gauche). Angles en degres dans le plan XZ
    // (0 = +X, 90 = +Z). Un cote bombe part incline de -tilt vers l'exterieur et revient a +tilt.
    // Rayon > 0 : 8 noeuds (2 par coin), Continuous. Rayon 0 : 4 noeuds aux coins, Broken (angle vif).
    // Longueur des tangentes de chaque segment = approximation Bezier d'un arc de cercle.
    static void BuildRoundedRect(UnityEngine.Splines.Spline spline, float width, float length, float radius,
        float tiltX, float tiltZ)
    {
        float total = tiltX + tiltZ;
        if (total > maxTotalTilt)
        {
            tiltX *= maxTotalTilt / total;
            tiltZ *= maxTotalTilt / total;
        }

        float hx = width * 0.5f;
        float hz = length * 0.5f;
        bool sharp = radius <= 0.001f;

        // Position, direction d'arrivee, direction de depart
        var pos = new System.Collections.Generic.List<Vector3>();
        var dirIn = new System.Collections.Generic.List<float>();
        var dirOut = new System.Collections.Generic.List<float>();
        void Add(Vector3 p, float aIn, float aOut) { pos.Add(p); dirIn.Add(aIn); dirOut.Add(aOut); }

        if (sharp)
        {
            Add(new Vector3(-hx, 0f, -hz), 270f + tiltZ, 0f - tiltX);
            Add(new Vector3(hx, 0f, -hz), 0f + tiltX, 90f - tiltZ);
            Add(new Vector3(hx, 0f, hz), 90f + tiltZ, 180f - tiltX);
            Add(new Vector3(-hx, 0f, hz), 180f + tiltX, 270f - tiltZ);
        }
        else
        {
            Add(new Vector3(-hx + radius, 0f, -hz), 0f - tiltX, 0f - tiltX);
            Add(new Vector3(hx - radius, 0f, -hz), 0f + tiltX, 0f + tiltX);
            Add(new Vector3(hx, 0f, -hz + radius), 90f - tiltZ, 90f - tiltZ);
            Add(new Vector3(hx, 0f, hz - radius), 90f + tiltZ, 90f + tiltZ);
            Add(new Vector3(hx - radius, 0f, hz), 180f - tiltX, 180f - tiltX);
            Add(new Vector3(-hx + radius, 0f, hz), 180f + tiltX, 180f + tiltX);
            Add(new Vector3(-hx, 0f, hz - radius), 270f - tiltZ, 270f - tiltZ);
            Add(new Vector3(-hx, 0f, -hz + radius), 270f + tiltZ, 270f + tiltZ);
        }

        int n = pos.Count;
        var tanIn = new Vector3[n];
        var tanOut = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            float turn = Mathf.DeltaAngle(dirOut[i], dirIn[next]);
            float h = ArcHandleLength(Vector3.Distance(pos[i], pos[next]), turn);
            tanOut[i] = Dir(dirOut[i]) * h;
            tanIn[next] = -Dir(dirIn[next]) * h;
        }

        spline.Clear();
        var mode = sharp ? UnityEngine.Splines.TangentMode.Broken : UnityEngine.Splines.TangentMode.Continuous;
        for (int i = 0; i < n; i++)
            spline.Add(new UnityEngine.Splines.BezierKnot(pos[i], tanIn[i], tanOut[i], Quaternion.identity), mode);
        spline.Closed = true;
    }

    static Vector3 Dir(float angleDeg)
    {
        float a = angleDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
    }

    // Tangente Bezier d'un arc de cercle de corde c qui tourne de turnDeg : (4/3) tan(turn/4) * R,
    // avec R = c / (2 sin(turn/2)). Quart de cercle : 0.5523 * rayon. Segment droit : c / 3.
    static float ArcHandleLength(float chord, float turnDeg)
    {
        float t = Mathf.Abs(turnDeg) * Mathf.Deg2Rad;
        if (t < 0.0001f) return chord / 3f;
        float r = chord / (2f * Mathf.Sin(t * 0.5f));
        return 4f / 3f * Mathf.Tan(t * 0.25f) * r;
    }

    // L'echantillonnage est regulier sur tout le perimetre : il faut assez de points pour que
    // le segment le plus serre (petit coin, cote bombe) en recoive environ pointsPerCorner par quart de tour.
    static int SampleCountFor(UnityEngine.Splines.Spline spline)
    {
        float minSpacing = float.MaxValue;
        for (int i = 0; i < spline.Count; i++)
        {
            int next = (i + 1) % spline.Count;
            Vector3 a = Vector3.Normalize((Vector3)Unity.Mathematics.math.rotate(spline[i].Rotation, spline[i].TangentOut));
            Vector3 b = Vector3.Normalize((Vector3)Unity.Mathematics.math.rotate(spline[next].Rotation, -spline[next].TangentIn));
            float turn = Vector3.Angle(a, b);
            int needed = Mathf.CeilToInt(turn / 90f * pointsPerCorner);
            if (needed > 0)
                minSpacing = Mathf.Min(minSpacing, spline.GetCurveLength(i) / needed);
        }
        if (minSpacing == float.MaxValue) return 32;
        return Mathf.Clamp(Mathf.CeilToInt(spline.GetLength() / minSpacing), 32, 512);
    }
}