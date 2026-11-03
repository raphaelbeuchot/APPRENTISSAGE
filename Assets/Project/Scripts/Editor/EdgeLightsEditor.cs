using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

[CustomEditor(typeof(EdgeLights))]
public class EdgeLightsEditor : Editor
{
    // ---------------------------------------------------------------- Sources du chemin

    // Ce qui porte les lumieres : longueur du chemin (metres) et point de bord en repere local du GO.
    abstract class EdgePath
    {
        public abstract string Label { get; }
        public abstract float Length { get; }
        // s, outward, up en metres. side = -1 gauche, +1 droite. forward en repere local, unitaire.
        public abstract void Point(float s, int side, float outward, float up, out Vector3 localPos, out Vector3 forward);
    }

    class RampPath : EdgePath
    {
        readonly Ramp ramp;
        public RampPath(Ramp ramp) { this.ramp = ramp; }
        public override string Label => "Ramp";
        public override float Length => ramp.WalkLength;
        public override void Point(float s, int side, float outward, float up, out Vector3 localPos, out Vector3 forward)
        {
            ramp.GetEdgePoint(s, side, outward, up, out localPos, out forward);
        }
    }

    // Boite du mesh (repere local) : chemin le long de X ou Z, dessus = face haute. Distances converties en metres
    // reels avec l'echelle du GO.
    class BoxPath : EdgePath
    {
        readonly Bounds b;
        readonly Vector3 scale;
        readonly bool alongX;

        public BoxPath(Bounds bounds, Vector3 lossyScale, EdgeLights.Axis axis)
        {
            b = bounds;
            scale = new Vector3(Mathf.Max(1e-4f, Mathf.Abs(lossyScale.x)), Mathf.Max(1e-4f, Mathf.Abs(lossyScale.y)),
                                Mathf.Max(1e-4f, Mathf.Abs(lossyScale.z)));
            alongX = axis == EdgeLights.Axis.X
                     || (axis == EdgeLights.Axis.Auto && b.size.x * scale.x > b.size.z * scale.z);
        }

        public override string Label => "Boite du mesh, chemin le long de " + (alongX ? "X" : "Z");
        public override float Length => alongX ? b.size.x * scale.x : b.size.z * scale.z;

        public override void Point(float s, int side, float outward, float up, out Vector3 localPos, out Vector3 forward)
        {
            float y = b.max.y + up / scale.y;
            if (alongX)
            {
                // En avancant vers +X, la droite est -Z.
                float x = b.min.x + s / scale.x;
                float z = b.center.z - side * (b.extents.z + outward / scale.z);
                localPos = new Vector3(x, y, z);
                forward = Vector3.right;
            }
            else
            {
                float z = b.min.z + s / scale.z;
                float x = b.center.x + side * (b.extents.x + outward / scale.x);
                localPos = new Vector3(x, y, z);
                forward = Vector3.forward;
            }
        }
    }

    // Chemin le long d'une chaine (EdgeChainPath), avec un repere en chaque point : sens du chemin, exterieur, axe.
    // outward = vers l'exterieur, up = le long de l'axe, along = glissement le long du chemin. side ignore.
    // Partage par la tranche du RoundedBlock et les aretes vives : memes options dans les deux cas.
    abstract class FramedPath : EdgePath
    {
        protected EdgeChainPath path;
        protected Vector3 scale;
        protected float outSign = 1f;
        protected float along;
        protected bool loop;
        public Vector3 axis;          // repere local tourne, en metres (echelle appliquee), unitaire
        public bool hasAxis = true;   // faux : chaine ouverte, axe = haut du monde, orientation a part
        public float[] cornerPositions = new float[0]; // Aux coins : abscisses, glissement non compris

        protected void Init(MeshEdgeChains.Chain chain, Vector3 lossyScale, float arcDegrees, float along)
        {
            scale = new Vector3(Mathf.Max(1e-4f, Mathf.Abs(lossyScale.x)), Mathf.Max(1e-4f, Mathf.Abs(lossyScale.y)),
                                Mathf.Max(1e-4f, Mathf.Abs(lossyScale.z)));
            this.along = along;
            loop = chain.closed;
            path = new EdgeChainPath(chain, scale, arcDegrees);
        }

        // Exterieur = axe x tangente, au signe pres (depend du sens du chemin) : fixe sur le premier point, du cote
        // oppose a 'center' (repere local).
        protected void FixOutSign(Vector3 center)
        {
            path.Point(0f, out Vector3 p0, out Vector3 f0);
            outSign = Vector3.Dot(Vector3.Cross(axis, f0), Vector3.Scale(p0 - center, scale)) >= 0f ? 1f : -1f;
        }

        public bool closed => path.closed;
        public override float Length => path.Length;

        public override void Point(float s, int side, float outward, float up, out Vector3 localPos, out Vector3 forward)
        {
            float total = path.TotalLength;
            path.Point(loop ? Mathf.Repeat(s + along, total) : Mathf.Clamp(s + along, 0f, total),
                       out Vector3 p, out forward);
            // Pile sur un coin : tangente moyenne des deux cotes, l'exterieur est alors la diagonale
            // (sur un coin vif, la tangente d'un seul segment ne rentrerait l'instance que d'un cote).
            if (along == 0f && System.Array.IndexOf(cornerPositions, s) >= 0)
            {
                const float e = 0.01f;
                path.Point(loop ? Mathf.Repeat(s - e, total) : Mathf.Max(0f, s - e), out _, out Vector3 before);
                path.Point(loop ? Mathf.Repeat(s + e, total) : Mathf.Min(total, s + e), out _, out Vector3 after);
                if ((before + after).sqrMagnitude > 1e-6f) forward = (before + after).normalized;
            }
            Vector3 o = Vector3.Cross(axis, forward);
            o = o.sqrMagnitude > 1e-8f ? o.normalized * outSign : Vector3.zero; // arete parallele a l'axe : pas d'exterieur
            Vector3 offset = o * outward + axis * up; // en metres
            localPos = p + new Vector3(offset.x / scale.x, offset.y / scale.y, offset.z / scale.z);
        }
    }

    // Chaine d'aretes vives (MeshEdgeChains). Boucle : axe = perpendiculaire a son plan, tourne vers l'exterieur du
    // mesh (cercle du haut d'un cylindre : vers le haut) ; exterieur = loin du centre de la boucle ; coins = sommets
    // qui tournent de plus de 45 degres. Chaine ouverte : axe = haut du monde, exterieur = loin du centre du mesh.
    class ChainPath : FramedPath
    {
        const float cornerTurn = 45f;

        public ChainPath(MeshEdgeChains.Chain chain, Transform tr, Vector3 meshCenter, float arcDegrees, float along)
        {
            Init(chain, tr.lossyScale, arcDegrees, along);

            Vector3 center = Vector3.zero;
            foreach (Vector3 p in chain.points) center += p;
            center /= Mathf.Max(1, chain.points.Count);
            Vector3 worldUp = Quaternion.Inverse(tr.rotation) * Vector3.up;

            Vector3 n = Vector3.zero;
            if (loop)
                for (int i = 0; i < chain.points.Count; i++)
                    n += Vector3.Cross(Vector3.Scale(chain.points[i] - center, scale),
                                       Vector3.Scale(chain.points[(i + 1) % chain.points.Count] - center, scale));
            if (loop && n.sqrMagnitude > 1e-10f)
            {
                axis = n.normalized;
                float side = Vector3.Dot(axis, Vector3.Scale(center - meshCenter, scale));
                // Boucle passant par le centre du mesh : on departage avec le haut du monde.
                if (side < 0f || (Mathf.Abs(side) < 1e-4f && Vector3.Dot(axis, worldUp) < 0f)) axis = -axis;
                FixOutSign(center);
            }
            else
            {
                axis = worldUp;
                hasAxis = false;
                FixOutSign(meshCenter);
            }

            cornerPositions = path.Corners(cornerTurn).ToArray();
        }

        public override string Label => "Arete";
    }

    // Tranche d'un RoundedBlock : tour du profil au bord de la face haute (RoundedBlock.GetTopContour). Axe = axe
    // d'extrusion ; up negatif = descend sur la tranche ; coins = milieu des quatre coins du profil.
    class BlockPath : FramedPath
    {
        public BlockPath(RoundedBlock block, float arcDegrees, float along)
        {
            var chain = new MeshEdgeChains.Chain { closed = true };
            block.GetTopContour(chain.points, out Vector3 extrusion);
            Init(chain, block.transform.lossyScale, arcDegrees, along);
            axis = extrusion;

            Vector3 center = Vector3.zero;
            foreach (Vector3 p in chain.points) center += p;
            center /= Mathf.Max(1, chain.points.Count);
            FixOutSign(center);

            var corners = new List<Vector3>();
            block.GetTopCorners(corners);
            cornerPositions = new float[corners.Count];
            for (int i = 0; i < corners.Count; i++) cornerPositions[i] = path.Nearest(corners[i]);
            System.Array.Sort(cornerPositions);
        }

        public override string Label => "Tranche du RoundedBlock";
    }

    static EdgePath GetPath(EdgeLights el)
    {
        Ramp ramp = el.GetComponent<Ramp>();
        if (ramp != null) return new RampPath(ramp);
        RoundedBlock block = el.GetComponent<RoundedBlock>();
        if (block != null) return new BlockPath(block, el.arc, el.alongOffset);
        MeshFilter mf = el.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) return new BoxPath(mf.sharedMesh.bounds, el.transform.lossyScale, el.axis);
        return null;
    }

    // Un passage de generation : un bord (gauche / droite) d'une Ramp ou d'une boite, ou une chaine cochee.
    class Run
    {
        public EdgePath path;
        public int side;
        public string name;
        public bool closed;
    }

    static List<Run> GetRuns(EdgeLights el, List<MeshEdgeChains.Chain> chains)
    {
        var runs = new List<Run>();
        if (el.source == EdgeLights.Source.Aretes)
        {
            MeshFilter mf = el.GetComponent<MeshFilter>();
            Vector3 meshCenter = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.center : Vector3.zero;
            foreach (int i in el.selectedChains)
            {
                if (i < 0 || i >= chains.Count || chains[i].points.Count < 2) continue;
                var cp = new ChainPath(chains[i], el.transform, meshCenter, el.arc, el.alongOffset);
                runs.Add(new Run { path = cp, side = 0, name = "C" + i, closed = cp.closed });
            }
            return runs;
        }

        EdgePath path = GetPath(el);
        if (path == null) return runs;
        if (path is BlockPath bp)
        {
            runs.Add(new Run { path = bp, side = 0, name = "T", closed = bp.closed });
            return runs;
        }
        if (el.sides != EdgeLights.Sides.Droite) runs.Add(new Run { path = path, side = -1, name = "L" });
        if (el.sides != EdgeLights.Sides.Gauche) runs.Add(new Run { path = path, side = 1, name = "R" });
        return runs;
    }

    static List<MeshEdgeChains.Chain> FindChains(EdgeLights el)
    {
        MeshFilter mf = el.GetComponent<MeshFilter>();
        return MeshEdgeChains.Find(mf != null ? mf.sharedMesh : null, el.transform.lossyScale, el.edgeAngle);
    }

    // ---------------------------------------------------------------- Inspector

    // Champs sans objet selon la source : masques.
    static readonly string[] hiddenAuto = { "edgeAngle", "arc", "alongOffset" };
    static readonly string[] hiddenAretes = { "sides", "axis" };
    static readonly string[] hiddenTranche = { "edgeAngle", "sides", "axis" };

    // Source Auto sur un RoundedBlock (sans Ramp) : sa tranche.
    static bool IsTranche(EdgeLights el) =>
        el.source == EdgeLights.Source.Auto && el.GetComponent<Ramp>() == null && el.GetComponent<RoundedBlock>() != null;

    // Aux coins possible : tranche du RoundedBlock ou aretes vives (Ramp et boite : non).
    static bool HasCorners(EdgeLights el) => el.source == EdgeLights.Source.Aretes || IsTranche(el);

    static bool UsesSpacing(EdgeLights el) =>
        el.distribution == EdgeLights.Distribution.Espacement
        || (el.distribution == EdgeLights.Distribution.AuxCoins && !HasCorners(el));

    public override void OnInspectorGUI()
    {
        EdgeLights el = (EdgeLights)target;
        bool aretes = el.source == EdgeLights.Source.Aretes;
        string[] hidden = aretes ? hiddenAretes : IsTranche(el) ? hiddenTranche : hiddenAuto;

        serializedObject.Update();
        SerializedProperty it = serializedObject.GetIterator();
        for (bool enter = true; it.NextVisible(enter); enter = false)
        {
            if (it.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(it);
                continue;
            }
            if (System.Array.IndexOf(hidden, it.propertyPath) >= 0) continue;
            if (it.propertyPath == "count" && el.distribution != EdgeLights.Distribution.Nombre) continue;
            if (it.propertyPath == "spacing" && !UsesSpacing(el)) continue;
            EditorGUILayout.PropertyField(it, true);
        }
        if (serializedObject.ApplyModifiedProperties()) SceneView.RepaintAll(); // fantomes a jour

        List<MeshEdgeChains.Chain> chains = aretes ? GetChains(el) : null;
        List<Run> runs = GetRuns(el, chains);

        GUILayout.Space(10);
        GUILayout.BeginVertical(EditorStyles.helpBox);

        if (aretes) DrawChainList(el, chains);

        if (!aretes && runs.Count == 0)
            EditorGUILayout.HelpBox("Pas de mesh sur ce GO.", MessageType.Warning);
        else
        {
            if (!aretes) GUILayout.Label("Source : " + runs[0].path.Label, EditorStyles.miniLabel);
            if (el.prefab == null)
                EditorGUILayout.HelpBox("Choisir un prefab ou un GO.", MessageType.Warning);
            else if (IsInsideGenerated(el))
                EditorGUILayout.HelpBox("Le modele est dans " + EdgeLights.ContainerName + " : il serait efface a la " +
                                        "generation. Le sortir de ce dossier.", MessageType.Error);
            else if (aretes && runs.Count == 0)
                EditorGUILayout.HelpBox("Cocher au moins une chaine.", MessageType.Warning);
            else
            {
                int total = 0;
                foreach (Run r in runs) total += PositionsFor(el, r).Length;
                GUILayout.Label(total + " instances", EditorStyles.miniLabel);
                if (el.distribution == EdgeLights.Distribution.AuxCoins && !HasCorners(el))
                    EditorGUILayout.HelpBox("Aux coins : RoundedBlock ou Aretes seulement, reparti ici a l'espacement.",
                                            MessageType.Info);
            }
        }

        GUI.enabled = el.prefab != null && runs.Count > 0 && !IsInsideGenerated(el);
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Generer (remplace les existantes)", GUILayout.Height(30)))
            Generate(el);
        GUI.enabled = true;

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Effacer", GUILayout.Height(22)))
            Clear(el);
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
    }

    // ---------------------------------------------------------------- Aretes vives : affichage

    readonly EdgeChainsGUI.Cache chainCache = new EdgeChainsGUI.Cache();

    List<MeshEdgeChains.Chain> GetChains(EdgeLights el) => chainCache.Get(el.transform, el.edgeAngle);

    void DrawChainList(EdgeLights el, List<MeshEdgeChains.Chain> chains)
    {
        EdgeChainsGUI.DrawChainList(el, el.selectedChains, chains, el.transform.lossyScale);
    }

    void OnSceneGUI()
    {
        EdgeLights el = (EdgeLights)target;
        SyncHiddenContainer(el);
        if (el.source != EdgeLights.Source.Aretes) return;
        EdgeChainsGUI.DrawSceneChains(el.transform, GetChains(el), el.selectedChains, el.arc);
    }

    // ---------------------------------------------------------------- Generation

    static float[] PositionsFor(EdgeLights el, Run run)
    {
        if (el.distribution == EdgeLights.Distribution.AuxCoins && run.path is FramedPath fp)
            return fp.cornerPositions;
        return Positions(el, run.path.Length, run.closed);
    }

    // Abscisses le long du chemin. Ouvert : de la marge de debut a la marge de fin, les deux bouts compris.
    // Boucle : sans marges, n intervalles egaux, le dernier point retombe sur le premier donc n'est pas repete.
    // Nombre : n impose ; sinon deduit de l'espacement (Aux coins hors RoundedBlock : comme Espacement).
    static float[] Positions(EdgeLights el, float length, bool closed)
    {
        float spacing = Mathf.Max(0.05f, el.spacing);
        bool byCount = el.distribution == EdgeLights.Distribution.Nombre;
        int wanted = Mathf.Max(1, el.count);
        if (closed)
        {
            if (length < 1e-4f) return new float[0];
            int count = byCount ? wanted : Mathf.Max(1, Mathf.RoundToInt(length / spacing));
            float loopStep = length / count;
            float[] l = new float[count];
            for (int i = 0; i < count; i++) l[i] = i * loopStep;
            return l;
        }

        float start = el.startMargin;
        float usable = length - el.startMargin - el.endMargin;
        if (usable < 0f) return new float[0];
        if (usable < 1e-4f) return new[] { start };
        if (byCount && wanted == 1) return new[] { start + usable * 0.5f }; // seule : au milieu

        int intervals = byCount ? wanted - 1 : Mathf.Max(1, Mathf.RoundToInt(usable / spacing));
        float step = usable / intervals;
        float[] s = new float[intervals + 1];
        for (int i = 0; i <= intervals; i++) s[i] = start + i * step;
        return s;
    }

    static void Generate(EdgeLights el)
    {
        Undo.SetCurrentGroupName("Generer EdgeLights");
        int group = Undo.GetCurrentGroup();

        Clear(el);
        Ramp ramp = el.GetComponent<Ramp>();
        if (ramp != null) ramp.Rebuild(); // pivot et dimensions a jour
        bool aretes = el.source == EdgeLights.Source.Aretes;
        List<Run> runs = GetRuns(el, aretes ? FindChains(el) : null);
        if (runs.Count == 0) return;

        GameObject container = new GameObject(EdgeLights.ContainerName);
        Undo.RegisterCreatedObjectUndo(container, "Generer EdgeLights");
        container.transform.SetParent(el.transform, false);
        // Echelle inverse du GO : les instances sont sous un parent sans echelle et gardent la forme du prefab, meme
        // tournees sur un GO etire (compenser l'echelle sur chaque instance deformerait les instances tournees).
        Vector3 parentScale = ParentScale(el);
        container.transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);

        bool isAsset = PrefabUtility.IsPartOfPrefabAsset(el.prefab);
        Vector3 scale = el.prefab.transform.localScale;

        foreach (Placement p in Placements(el, runs))
        {
            GameObject inst = isAsset
                ? (GameObject)PrefabUtility.InstantiatePrefab(el.prefab, container.transform)
                : Instantiate(el.prefab, container.transform);
            inst.name = p.name;
            if (!isAsset) inst.SetActive(true); // modele de la scene eventuellement desactive
            inst.transform.localPosition = Vector3.Scale(p.localPos, parentScale);
            inst.transform.localRotation = p.localRot;
            inst.transform.localScale = scale;
            if (el.noColliders)
                foreach (Collider c in inst.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            Undo.RegisterCreatedObjectUndo(inst, "Generer EdgeLights");
        }

        Undo.CollapseUndoOperations(group);
    }

    // Une instance a venir, en repere du GO (le dossier genere est a l'identite sous lui).
    struct Placement
    {
        public string name;
        public Vector3 localPos;
        public Quaternion localRot;
    }

    // Positions et rotations des instances : partage par Generer et la previsualisation, qui montre donc exactement
    // ce qui sera genere.
    static List<Placement> Placements(EdgeLights el, List<Run> runs)
    {
        var list = new List<Placement>();
        if (el.prefab == null) return list;
        bool aretes = el.source == EdgeLights.Source.Aretes;
        Quaternion toLocal = Quaternion.Inverse(el.transform.rotation);
        Quaternion extra = Quaternion.Euler(el.rotationOffset);

        foreach (Run run in runs)
        {
            float[] positions = PositionsFor(el, run);
            for (int i = 0; i < positions.Length; i++)
            {
                run.path.Point(positions[i], run.side, el.outward, el.height, out Vector3 pos, out Vector3 forward);

                Quaternion rot; // monde
                if (!el.alignToPath)
                    rot = el.prefab.transform.rotation;
                else if (run.path is FramedPath fp && fp.hasAxis)
                    rot = el.transform.rotation * Quaternion.LookRotation(forward, fp.axis);
                else if (aretes)
                {
                    // Chaine ouverte : haut du monde ; arete verticale (coin de cube) : +Z monde pour ne pas partir en vrille.
                    Vector3 worldForward = el.transform.rotation * forward;
                    Vector3 up = Mathf.Abs(Vector3.Dot(worldForward, Vector3.up)) > 0.999f ? Vector3.forward : Vector3.up;
                    rot = Quaternion.LookRotation(worldForward, up);
                }
                else
                    rot = el.transform.rotation * Quaternion.LookRotation(forward, Vector3.up);

                list.Add(new Placement
                {
                    name = el.prefab.name + "_" + run.name + (aretes ? "_" : "") + i.ToString("D2"),
                    localPos = pos,
                    localRot = toLocal * rot * extra
                });
            }
        }
        return list;
    }

    // Echelle du GO, annulee par le dossier genere.
    static Vector3 ParentScale(EdgeLights el)
    {
        Vector3 s = el.transform.lossyScale;
        return new Vector3(Mathf.Max(1e-4f, Mathf.Abs(s.x)), Mathf.Max(1e-4f, Mathf.Abs(s.y)),
                           Mathf.Max(1e-4f, Mathf.Abs(s.z)));
    }

    // ---------------------------------------------------------------- Previsualisation

    // Instances fantomes : les meshes du prefab dessines a chaque rendu de la Scene view (Graphics.DrawMesh), avec
    // leurs materiaux, sans rien creer. URP : beginCameraRendering ; pipeline integre : Camera.onPreCull.
    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        Camera.onPreCull += DrawPreview;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        Camera.onPreCull -= DrawPreview;
        ShowHiddenContainer();
    }

    // Instances deja generees masquees dans la Scene view pendant la previsualisation (visibilite editeur, comme
    // l'oeil de la Hierarchy : rien n'est desactive ni sauvegarde). Seul ce qu'on a masque nous-memes est reaffiche.
    GameObject hiddenContainer;

    void SyncHiddenContainer(EdgeLights el)
    {
        Transform t = el.transform.Find(EdgeLights.ContainerName);
        GameObject container = t != null ? t.gameObject : null;
        bool hide = el.preview && el.prefab != null && container != null;

        if (hiddenContainer != null && (!hide || hiddenContainer != container)) ShowHiddenContainer();
        if (hide && hiddenContainer == null && !SceneVisibilityManager.instance.IsHidden(container, true))
        {
            SceneVisibilityManager.instance.Hide(container, true);
            hiddenContainer = container;
        }
    }

    void ShowHiddenContainer()
    {
        if (hiddenContainer != null) SceneVisibilityManager.instance.Show(hiddenContainer, true);
        hiddenContainer = null;
    }

    void OnBeginCamera(ScriptableRenderContext context, Camera cam) => DrawPreview(cam);

    void DrawPreview(Camera cam)
    {
        if (cam == null || cam.cameraType != CameraType.SceneView) return;
        EdgeLights el = target as EdgeLights;
        if (el == null || !el.preview || el.prefab == null || IsInsideGenerated(el)) return;

        List<Run> runs = GetRuns(el, el.source == EdgeLights.Source.Aretes ? GetChains(el) : null);
        List<Placement> placements = Placements(el, runs);
        if (placements.Count == 0) return;

        // Morceaux du prefab, en repere de sa racine
        Transform root = el.prefab.transform;
        Matrix4x4 toRoot = root.worldToLocalMatrix;
        var renderers = new List<MeshRenderer>();
        foreach (MeshRenderer mr in el.prefab.GetComponentsInChildren<MeshRenderer>(true))
            if (mr.enabled && ActiveBelowRoot(mr.transform, root)) renderers.Add(mr);
        if (renderers.Count == 0) return;

        // Meme hierarchie que Generer : GO, dossier a echelle inverse, instance.
        Vector3 parentScale = ParentScale(el);
        Matrix4x4 parent = el.transform.localToWorldMatrix
                           * Matrix4x4.Scale(new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z));
        Vector3 scale = el.prefab.transform.localScale;
        foreach (Placement p in placements)
        {
            Matrix4x4 inst = parent * Matrix4x4.TRS(Vector3.Scale(p.localPos, parentScale), p.localRot, scale);
            foreach (MeshRenderer mr in renderers)
            {
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Mesh mesh = mf.sharedMesh;
                Matrix4x4 m = inst * toRoot * mr.transform.localToWorldMatrix;
                Material[] mats = mr.sharedMaterials;
                for (int sub = 0; sub < mesh.subMeshCount && sub < mats.Length; sub++)
                    if (mats[sub] != null) Graphics.DrawMesh(mesh, m, mats[sub], mr.gameObject.layer, cam, sub);
            }
        }
    }

    // La racine peut etre desactivee (modele cache dans la scene) : les copies sont reactivees, pas leurs enfants.
    static bool ActiveBelowRoot(Transform t, Transform root)
    {
        for (; t != null && t != root; t = t.parent)
            if (!t.gameObject.activeSelf) return false;
        return true;
    }

    // Un modele range dans le dossier genere disparaitrait au premier Generer / Effacer.
    static bool IsInsideGenerated(EdgeLights el)
    {
        if (el.prefab == null || PrefabUtility.IsPartOfPrefabAsset(el.prefab)) return false;
        Transform container = el.transform.Find(EdgeLights.ContainerName);
        return container != null && el.prefab.transform.IsChildOf(container);
    }

    static void Clear(EdgeLights el)
    {
        Transform existing = el.transform.Find(EdgeLights.ContainerName);
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
    }
}
