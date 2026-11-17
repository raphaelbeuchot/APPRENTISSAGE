#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

// Mesures de performance : F3 demarre / arrete l'enregistrement ("REC" a l'ecran).
// Deux fois par seconde, une ligne CSV dans persistentDataPath/perf_<date>.csv
// (meme dossier que Player.log) : scene, etat du cycle, FPS, temps par image, CPU, GPU, rendu, memoire.
// Interrupteurs A/B (colonne "off") : F5 ombres, F6 post-process (Volumes), F7 lumieres sauf directionnelles,
// F8 objets reactifs au cycle (ampoules...). Rien n'est enregistre dans les scenes ; tout revient au changement de scene.
// F9 : photo de l'image (snapshot_<date>.txt, meme dossier) : cameras, reglages URP, Volumes, lumieres,
// objets visibles par shader, particules, canvas. En editeur, fermer la Scene view (elle compte comme visible).
// S'installe tout seul au lancement (rien a poser dans les scenes), editeur et builds de dev seulement.
public class PerfRecorder : MonoBehaviour
{
    // Etat d'origine memorise a l'activation de chaque interrupteur, restaure a la desactivation
    private bool shadowsOff, postOff, lightsOff, reactiveOff;
    private readonly Dictionary<Light, LightShadows> savedShadows = new Dictionary<Light, LightShadows>();
    private readonly Dictionary<Volume, bool> savedVolumes = new Dictionary<Volume, bool>();
    private readonly Dictionary<Light, bool> savedLights = new Dictionary<Light, bool>();
    private readonly Dictionary<GameObject, bool> savedReactive = new Dictionary<GameObject, bool>();

    // F10 SSAO, F11 TAA, F12 Render Scale. SSAO et Render Scale modifient l'asset URP :
    // toujours restaures (changement de scene, arret du Play, fermeture). Ne pas faire Ctrl+S pendant le test.
    private bool ssaoOff, taaOff;
    private readonly List<ScriptableRendererFeature> savedSsao = new List<ScriptableRendererFeature>();
    private readonly Dictionary<UniversalAdditionalCameraData, AntialiasingMode> savedAA = new Dictionary<UniversalAdditionalCameraData, AntialiasingMode>();
    private static readonly float[] RENDER_SCALES = { 1f, 0.75f, 0.5f };
    private int renderScaleIndex;
    private float originalRenderScale = -1f;

    private const float SAMPLE_INTERVAL = 0.5f;

    private bool recording;
    private StreamWriter writer;
    private string filePath;
    private string cycleState = "-";

    private ProfilerRecorder mainThread;
    private ProfilerRecorder waitForGpu;
    private ProfilerRecorder batches;
    private ProfilerRecorder setPass;
    private ProfilerRecorder drawCalls;
    private ProfilerRecorder triangles;
    private ProfilerRecorder gcAlloc;

    // Accumulateurs sur l'intervalle
    private int frames;
    private float intervalTime;
    private float frameMsMax;
    private double mainMsSum, waitGpuMsSum, gpuMsSum;
    private int gpuSamples;
    private long batchesSum, setPassSum, drawSum, trisSum, gcSum;
    private long batchesMax, setPassMax;

    private readonly FrameTiming[] frameTimings = new FrameTiming[1];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject go = new GameObject("[PerfRecorder]");
        DontDestroyOnLoad(go);
        go.AddComponent<PerfRecorder>();
    }

    void OnEnable()
    {
        SentinelCycleManager.OnCycleChanged += OnCycleChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SentinelCycleManager.OnCycleChanged -= OnCycleChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StopRecording();
        RestoreUrpAsset();
    }

    void OnApplicationQuit()
    {
        RestoreUrpAsset();
    }

    // Remet l'asset URP comme a l'origine (SSAO, Render Scale)
    private void RestoreUrpAsset()
    {
        if (ssaoOff)
        {
            foreach (ScriptableRendererFeature f in savedSsao)
                if (f != null) f.SetActive(true);
            savedSsao.Clear();
            ssaoOff = false;
        }
        if (originalRenderScale > 0f)
        {
            UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null) urp.renderScale = originalRenderScale;
            originalRenderScale = -1f;
        }
        renderScaleIndex = 0;
    }

    private void OnCycleChanged(SentinelCycleManager.GameState state)
    {
        cycleState = state.ToString();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        cycleState = "-";
        // Nouvelle scene : les objets memorises n'existent plus, interrupteurs remis a zero
        shadowsOff = postOff = lightsOff = reactiveOff = false;
        savedShadows.Clear();
        savedVolumes.Clear();
        savedLights.Clear();
        savedReactive.Clear();
        RestoreUrpAsset();
        taaOff = false;
        savedAA.Clear();
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.f3Key.wasPressedThisFrame)
            {
                if (recording) StopRecording();
                else StartRecording();
            }
            if (kb.f5Key.wasPressedThisFrame) ToggleShadows();
            if (kb.f6Key.wasPressedThisFrame) TogglePost();
            if (kb.f7Key.wasPressedThisFrame) ToggleLights();
            if (kb.f8Key.wasPressedThisFrame) ToggleReactive();
            if (kb.f9Key.wasPressedThisFrame) WriteSnapshot();
            if (kb.f4Key.wasPressedThisFrame && !gpuCaptureRunning) StartCoroutine(GpuCapture());
            if (kb.f10Key.wasPressedThisFrame) ToggleSsao();
            if (kb.f11Key.wasPressedThisFrame) ToggleTaa();
            if (kb.f12Key.wasPressedThisFrame) CycleRenderScale();
        }

        if (!recording) return;

        float dtMs = Time.unscaledDeltaTime * 1000f;
        frames++;
        intervalTime += Time.unscaledDeltaTime;
        if (dtMs > frameMsMax) frameMsMax = dtMs;

        mainMsSum += mainThread.LastValue * 1e-6;
        waitGpuMsSum += waitForGpu.LastValue * 1e-6;

        long b = batches.LastValue, s = setPass.LastValue;
        batchesSum += b; setPassSum += s;
        if (b > batchesMax) batchesMax = b;
        if (s > setPassMax) setPassMax = s;
        drawSum += drawCalls.LastValue;
        trisSum += triangles.LastValue;
        gcSum += gcAlloc.LastValue;

        FrameTimingManager.CaptureFrameTimings();
        if (FrameTimingManager.GetLatestTimings(1, frameTimings) > 0 && frameTimings[0].gpuFrameTime > 0)
        {
            gpuMsSum += frameTimings[0].gpuFrameTime;
            gpuSamples++;
        }

        if (intervalTime >= SAMPLE_INTERVAL)
            WriteSample();
    }

    // Le jeu peut rallumer une lumiere ou un Volume (cycle, boss) : on le recoupe apres son Update
    void LateUpdate()
    {
        if (postOff)
            foreach (Volume v in savedVolumes.Keys)
                if (v != null && v.enabled) v.enabled = false;

        if (lightsOff)
            foreach (Light l in savedLights.Keys)
                if (l != null && l.enabled) l.enabled = false;
    }

    // ============================================
    // INTERRUPTEURS A/B
    // ============================================

    private void ToggleShadows()
    {
        shadowsOff = !shadowsOff;
        if (shadowsOff)
        {
            savedShadows.Clear();
            foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                savedShadows[l] = l.shadows;
                l.shadows = LightShadows.None;
            }
        }
        else
        {
            foreach (KeyValuePair<Light, LightShadows> kv in savedShadows)
                if (kv.Key != null) kv.Key.shadows = kv.Value;
            savedShadows.Clear();
        }
        Debug.Log("[PerfRecorder] Ombres " + (shadowsOff ? "coupees" : "retablies"));
    }

    private void TogglePost()
    {
        postOff = !postOff;
        if (postOff)
        {
            savedVolumes.Clear();
            foreach (Volume v in FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                savedVolumes[v] = v.enabled;
                v.enabled = false;
            }
        }
        else
        {
            foreach (KeyValuePair<Volume, bool> kv in savedVolumes)
                if (kv.Key != null) kv.Key.enabled = kv.Value;
            savedVolumes.Clear();
        }
        Debug.Log("[PerfRecorder] Post-process " + (postOff ? "coupe" : "retabli"));
    }

    private void ToggleLights()
    {
        lightsOff = !lightsOff;
        if (lightsOff)
        {
            savedLights.Clear();
            foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) continue;
                savedLights[l] = l.enabled;
                l.enabled = false;
            }
        }
        else
        {
            // Etat d'avant F7 : le cycle remet ses propres lumieres a jour au changement d'etat suivant
            foreach (KeyValuePair<Light, bool> kv in savedLights)
                if (kv.Key != null) kv.Key.enabled = kv.Value;
            savedLights.Clear();
        }
        Debug.Log("[PerfRecorder] Lumieres non directionnelles " + (lightsOff ? "coupees" : "retablies"));
    }

    private void ToggleReactive()
    {
        reactiveOff = !reactiveOff;
        if (reactiveOff)
        {
            savedReactive.Clear();
            foreach (CycleReactiveRenderer r in FindObjectsByType<CycleReactiveRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                savedReactive[r.gameObject] = true;
                r.gameObject.SetActive(false);
            }
        }
        else
        {
            foreach (KeyValuePair<GameObject, bool> kv in savedReactive)
                if (kv.Key != null) kv.Key.SetActive(kv.Value);
            savedReactive.Clear();
        }
        Debug.Log("[PerfRecorder] Objets reactifs au cycle " + (reactiveOff ? "coupes (" + savedReactive.Count + ")" : "retablis"));
    }

    private void ToggleSsao()
    {
        if (ssaoOff)
        {
            foreach (ScriptableRendererFeature f in savedSsao)
                if (f != null) f.SetActive(true);
            savedSsao.Clear();
            ssaoOff = false;
        }
        else
        {
            savedSsao.Clear();
            foreach (ScriptableRendererData data in GetRendererDataList())
            {
                if (data == null) continue;
                foreach (ScriptableRendererFeature f in data.rendererFeatures)
                {
                    if (f == null || !f.isActive) continue;
                    if (f.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                    f.SetActive(false);
                    savedSsao.Add(f);
                }
            }
            ssaoOff = savedSsao.Count > 0;
        }
        Debug.Log("[PerfRecorder] SSAO " + (ssaoOff ? "coupee" : "retablie (ou introuvable)"));
    }

    // La liste des renderers de l'asset URP n'est pas publique : lecture du champ serialise
    private static ScriptableRendererData[] GetRendererDataList()
    {
        UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return new ScriptableRendererData[0];
        System.Reflection.FieldInfo field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        ScriptableRendererData[] list = field != null ? field.GetValue(urp) as ScriptableRendererData[] : null;
        return list ?? new ScriptableRendererData[0];
    }

    private void ToggleTaa()
    {
        taaOff = !taaOff;
        if (taaOff)
        {
            savedAA.Clear();
            foreach (Camera c in Camera.allCameras)
            {
                UniversalAdditionalCameraData data = c.GetComponent<UniversalAdditionalCameraData>();
                if (data == null) continue;
                savedAA[data] = data.antialiasing;
                data.antialiasing = AntialiasingMode.None;
            }
        }
        else
        {
            foreach (KeyValuePair<UniversalAdditionalCameraData, AntialiasingMode> kv in savedAA)
                if (kv.Key != null) kv.Key.antialiasing = kv.Value;
            savedAA.Clear();
        }
        Debug.Log("[PerfRecorder] Anticrenelage camera " + (taaOff ? "coupe" : "retabli"));
    }

    private void CycleRenderScale()
    {
        UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return;
        if (originalRenderScale < 0f) originalRenderScale = urp.renderScale;

        renderScaleIndex = (renderScaleIndex + 1) % RENDER_SCALES.Length;
        urp.renderScale = renderScaleIndex == 0 ? originalRenderScale : RENDER_SCALES[renderScaleIndex];
        if (renderScaleIndex == 0) originalRenderScale = -1f;
        Debug.Log("[PerfRecorder] Render Scale " + urp.renderScale.ToString("F2", CultureInfo.InvariantCulture));
    }

    private string OffLabel()
    {
        string s = "";
        if (shadowsOff) s += "ombres+";
        if (postOff) s += "post+";
        if (lightsOff) s += "lumieres+";
        if (reactiveOff) s += "ampoules+";
        if (ssaoOff) s += "ssao+";
        if (taaOff) s += "taa+";
        if (renderScaleIndex != 0) s += "scale" + RENDER_SCALES[renderScaleIndex].ToString("F2", CultureInfo.InvariantCulture) + "+";
        return s.Length == 0 ? "-" : s.TrimEnd('+');
    }

    // ============================================
    // TEMPS GPU PAR ETAPE DU RENDU (F4)
    // ============================================

    private const int GPU_CAPTURE_FRAMES = 120;
    private bool gpuCaptureRunning;

    private System.Collections.IEnumerator GpuCapture()
    {
        gpuCaptureRunning = true;
        CultureInfo inv = CultureInfo.InvariantCulture;
        string date = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("# Temps GPU par etape " + date);
        sb.AppendLine("scene: " + SceneManager.GetActiveScene().name + " | etat cycle: " + cycleState + " | off: " + OffLabel());
        sb.AppendLine("ecran: " + Screen.width + "x" + Screen.height + " | GPU: " + SystemInfo.graphicsDeviceName + " | API: " + SystemInfo.graphicsDeviceType);
        sb.AppendLine("supportsGpuRecorder: " + SystemInfo.supportsGpuRecorder);

        // Marqueurs disponibles : ceux qui mesurent le GPU, sinon ceux de la categorie Render
        List<ProfilerRecorderHandle> handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        List<ProfilerRecorderHandle> gpuHandles = new List<ProfilerRecorderHandle>();
        foreach (ProfilerRecorderHandle h in handles)
        {
            ProfilerRecorderDescription d = ProfilerRecorderHandle.GetDescription(h);
            if ((d.Flags & MarkerFlags.SampleGPU) != 0) gpuHandles.Add(h);
        }
        bool flagged = gpuHandles.Count > 0;
        if (!flagged)
            foreach (ProfilerRecorderHandle h in handles)
                if (ProfilerRecorderHandle.GetDescription(h).Category == ProfilerCategory.Render) gpuHandles.Add(h);
        sb.AppendLine("marqueurs: " + handles.Count + " disponibles, " + gpuHandles.Count + " suivis (" + (flagged ? "marques GPU" : "categorie Render, pas de marque GPU") + ")");

        List<ProfilerRecorder> recorders = new List<ProfilerRecorder>();
        List<string> names = new List<string>();
        foreach (ProfilerRecorderHandle h in gpuHandles)
        {
            ProfilerRecorder r = new ProfilerRecorder(h, 1, ProfilerRecorderOptions.GpuRecorder | ProfilerRecorderOptions.SumAllSamplesInFrame);
            r.Start();
            recorders.Add(r);
            names.Add(ProfilerRecorderHandle.GetDescription(h).Name);
        }

        double[] sums = new double[recorders.Count];
        double gpuTotal = 0;
        int gpuTotalSamples = 0;
        Debug.Log("[PerfRecorder] Capture GPU par etape (" + GPU_CAPTURE_FRAMES + " images)...");

        for (int frame = 0; frame < GPU_CAPTURE_FRAMES; frame++)
        {
            yield return null;
            for (int i = 0; i < recorders.Count; i++)
                sums[i] += recorders[i].LastValue * 1e-6;

            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, frameTimings) > 0 && frameTimings[0].gpuFrameTime > 0)
            {
                gpuTotal += frameTimings[0].gpuFrameTime;
                gpuTotalSamples++;
            }
        }

        foreach (ProfilerRecorder r in recorders) r.Dispose();

        sb.AppendLine("temps GPU total par image (FrameTiming): " + (gpuTotalSamples > 0 ? (gpuTotal / gpuTotalSamples).ToString("F2", inv) + " ms" : "indisponible"));
        sb.AppendLine("Attention : les etapes s'emboitent (une etape parente contient ses enfants), ne pas additionner.");
        sb.AppendLine();
        sb.AppendLine("## Etapes, ms GPU par image (moyenne sur " + GPU_CAPTURE_FRAMES + " images)");

        List<KeyValuePair<string, double>> rows = new List<KeyValuePair<string, double>>();
        for (int i = 0; i < sums.Length; i++)
        {
            double avg = sums[i] / GPU_CAPTURE_FRAMES;
            if (avg >= 0.005) rows.Add(new KeyValuePair<string, double>(names[i], avg));
        }
        foreach (KeyValuePair<string, double> kv in rows.OrderByDescending(k => k.Value))
            sb.AppendLine("- " + kv.Value.ToString("F3", inv) + " | " + kv.Key);
        if (rows.Count == 0) sb.AppendLine("(aucune mesure : GPU recorder non pris en charge ici)");

        string path = Path.Combine(Application.persistentDataPath, "gpu_" + date + ".txt");
        File.WriteAllText(path, sb.ToString());
        Debug.Log("[PerfRecorder] Temps GPU par etape : " + path);
        gpuCaptureRunning = false;
    }

    // ============================================
    // PHOTO DE L'IMAGE (F9)
    // ============================================

    private class ShaderGroup
    {
        public int count, transparent;
        public long tris;
    }

    private void WriteSnapshot()
    {
        CultureInfo inv = CultureInfo.InvariantCulture;
        StringBuilder sb = new StringBuilder();
        string date = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        sb.AppendLine("# Snapshot " + date);
        sb.AppendLine("scene: " + SceneManager.GetActiveScene().name + " | etat cycle: " + cycleState + " | off: " + OffLabel());
        sb.AppendLine("ecran: " + Screen.width + "x" + Screen.height + " | GPU: " + SystemInfo.graphicsDeviceName);

        // --- URP ---
        UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
            sb.AppendLine("URP: " + urp.name + " | renderScale " + urp.renderScale.ToString("F2", inv) + " | MSAA " + urp.msaaSampleCount
                + " | HDR " + urp.supportsHDR + " | shadowDistance " + urp.shadowDistance.ToString("F0", inv));

        // --- Cameras ---
        sb.AppendLine();
        sb.AppendLine("## Cameras actives");
        Camera mainCam = Camera.main;
        foreach (Camera c in Camera.allCameras)
        {
            UniversalAdditionalCameraData data = c.GetComponent<UniversalAdditionalCameraData>();
            string extra = data != null
                ? " | type " + data.renderType + " | postFX " + data.renderPostProcessing + " | AA " + data.antialiasing
                  + " | ombres " + data.renderShadows + " | stack " + (data.cameraStack != null ? data.cameraStack.Count : 0)
                : "";
            sb.AppendLine("- " + PathOf(c.transform) + (c == mainCam ? " (MAIN)" : "") + " | depth " + c.depth
                + " | far " + c.farClipPlane.ToString("F0", inv) + " | rect " + c.rect + " | target " + (c.targetTexture != null ? c.targetTexture.name : "ecran") + extra);
        }

        // --- Volumes ---
        sb.AppendLine();
        sb.AppendLine("## Volumes actifs (* = agit sur la camera main)");
        Vector3 camPos = mainCam != null ? mainCam.transform.position : Vector3.zero;
        foreach (Volume v in FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!v.enabled) continue;
            bool acts = v.isGlobal || VolumeContains(v, camPos);
            VolumeProfile p = v.HasInstantiatedProfile() ? v.profile : v.sharedProfile;
            string comps = "";
            if (p != null)
                foreach (VolumeComponent vc in p.components)
                    if (vc.active) comps += vc.GetType().Name + " ";
            sb.AppendLine((acts ? "* " : "- ") + PathOf(v.transform) + " | " + (v.isGlobal ? "global" : "local") + " | prio " + v.priority.ToString("F0", inv)
                + " | poids " + v.weight.ToString("F2", inv) + " | profil " + (p != null ? p.name : "aucun") + " | " + comps.Trim());
        }

        // --- Lumieres ---
        sb.AppendLine();
        sb.AppendLine("## Lumieres allumees");
        foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!l.enabled) continue;
            sb.AppendLine("- " + PathOf(l.transform) + " | " + l.type + " | intensite " + l.intensity.ToString("F2", inv)
                + " | portee " + l.range.ToString("F1", inv) + " | ombres " + l.shadows + " | mode " + l.renderMode);
        }

        // --- Objets visibles par shader ---
        Dictionary<string, ShaderGroup> byShader = new Dictionary<string, ShaderGroup>();
        List<KeyValuePair<string, long>> heavy = new List<KeyValuePair<string, long>>();
        Dictionary<string, int> byRoot = new Dictionary<string, int>();
        int visible = 0;
        long totalTris = 0;
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!r.enabled || !r.isVisible) continue;
            visible++;
            long tris = TriangleCount(r);
            totalTris += tris;

            Material m = r.sharedMaterial;
            string shader = m != null && m.shader != null ? m.shader.name : "(aucun materiau)";
            bool transp = m != null && m.renderQueue >= 3000;
            if (!byShader.TryGetValue(shader, out ShaderGroup g)) { g = new ShaderGroup(); byShader[shader] = g; }
            g.count++;
            g.tris += tris;
            if (transp) g.transparent++;

            heavy.Add(new KeyValuePair<string, long>(PathOf(r.transform) + " [" + r.GetType().Name + "]", tris));
            string root = r.transform.root.name;
            byRoot[root] = byRoot.TryGetValue(root, out int n) ? n + 1 : 1;
        }

        sb.AppendLine();
        sb.AppendLine("## Objets visibles : " + visible + " renderers, " + totalTris + " triangles (meshes seulement)");
        sb.AppendLine("### Par shader (nombre | transparents | triangles)");
        foreach (KeyValuePair<string, ShaderGroup> kv in byShader.OrderByDescending(k => k.Value.count))
            sb.AppendLine("- " + kv.Key + " | " + kv.Value.count + " | " + kv.Value.transparent + " | " + kv.Value.tris);

        sb.AppendLine("### Par objet racine (top 20, nombre de renderers)");
        foreach (KeyValuePair<string, int> kv in byRoot.OrderByDescending(k => k.Value).Take(20))
            sb.AppendLine("- " + kv.Key + " | " + kv.Value);

        sb.AppendLine("### Plus gros en triangles (top 15)");
        foreach (KeyValuePair<string, long> kv in heavy.OrderByDescending(k => k.Value).Take(15))
            sb.AppendLine("- " + kv.Key + " | " + kv.Value);

        // --- Particules ---
        sb.AppendLine();
        int playing = 0, particles = 0;
        List<KeyValuePair<string, int>> ps = new List<KeyValuePair<string, int>>();
        foreach (ParticleSystem p in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!p.isPlaying) continue;
            playing++;
            particles += p.particleCount;
            ps.Add(new KeyValuePair<string, int>(PathOf(p.transform), p.particleCount));
        }
        sb.AppendLine("## Particules : " + playing + " systemes en cours, " + particles + " particules");
        foreach (KeyValuePair<string, int> kv in ps.OrderByDescending(k => k.Value).Take(10))
            sb.AppendLine("- " + kv.Key + " | " + kv.Value);

        // --- Canvas ---
        sb.AppendLine();
        sb.AppendLine("## Canvas actifs (racines)");
        foreach (Canvas cv in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!cv.enabled || !cv.isRootCanvas) continue;
            int graphics = cv.GetComponentsInChildren<UnityEngine.UI.Graphic>(false).Length;
            sb.AppendLine("- " + PathOf(cv.transform) + " | " + cv.renderMode + " | " + graphics + " elements graphiques");
        }

        string path = Path.Combine(Application.persistentDataPath, "snapshot_" + date + ".txt");
        File.WriteAllText(path, sb.ToString());
        Debug.Log("[PerfRecorder] Snapshot : " + path);
    }

    private static bool VolumeContains(Volume v, Vector3 pos)
    {
        foreach (Collider col in v.GetComponents<Collider>())
        {
            if (!col.enabled) continue;
            if (Vector3.Distance(col.ClosestPoint(pos), pos) <= v.blendDistance + 0.001f) return true;
        }
        return false;
    }

    private static long TriangleCount(Renderer r)
    {
        Mesh mesh = null;
        if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
        else
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf != null) mesh = mf.sharedMesh;
        }
        if (mesh == null) return 0;
        long t = 0;
        for (int i = 0; i < mesh.subMeshCount; i++) t += (long)mesh.GetIndexCount(i) / 3;
        return t;
    }

    private static string PathOf(Transform t)
    {
        string p = t.name;
        int depth = 0;
        while (t.parent != null && depth < 3) { t = t.parent; p = t.name + "/" + p; depth++; }
        return p;
    }

    // ============================================
    // ENREGISTREMENT
    // ============================================

    private void StartRecording()
    {
        string dir = Application.persistentDataPath;
        filePath = Path.Combine(dir, "perf_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".csv");
        writer = new StreamWriter(filePath, false);
        writer.WriteLine("time,scene,state,off,fps,frame_ms_avg,frame_ms_max,main_thread_ms,wait_gpu_ms,gpu_ms,batches_avg,batches_max,setpass_avg,setpass_max,drawcalls_avg,triangles_avg,gc_alloc_kb_frame,resolution,gpu_name");
        writer.Flush();

        mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
        waitForGpu = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Gfx.WaitForPresentOnGfxThread");
        batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
        setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

        ResetInterval();
        recording = true;
        Debug.Log("[PerfRecorder] Enregistrement : " + filePath);
    }

    private void StopRecording()
    {
        if (!recording) return;
        recording = false;

        mainThread.Dispose();
        waitForGpu.Dispose();
        batches.Dispose();
        setPass.Dispose();
        drawCalls.Dispose();
        triangles.Dispose();
        gcAlloc.Dispose();

        if (writer != null)
        {
            writer.Flush();
            writer.Dispose();
            writer = null;
        }
        Debug.Log("[PerfRecorder] Arret : " + filePath);
    }

    private void WriteSample()
    {
        CultureInfo inv = CultureInfo.InvariantCulture;
        float n = Mathf.Max(1, frames);

        string line = string.Join(",",
            Time.realtimeSinceStartup.ToString("F1", inv),
            Quote(SceneManager.GetActiveScene().name),
            cycleState,
            OffLabel(),
            (frames / intervalTime).ToString("F1", inv),
            (intervalTime * 1000f / n).ToString("F2", inv),
            frameMsMax.ToString("F2", inv),
            (mainMsSum / n).ToString("F2", inv),
            (waitGpuMsSum / n).ToString("F2", inv),
            gpuSamples > 0 ? (gpuMsSum / gpuSamples).ToString("F2", inv) : "-1",
            (batchesSum / n).ToString("F0", inv),
            batchesMax.ToString(inv),
            (setPassSum / n).ToString("F0", inv),
            setPassMax.ToString(inv),
            (drawSum / n).ToString("F0", inv),
            (trisSum / n).ToString("F0", inv),
            (gcSum / n / 1024f).ToString("F1", inv),
            Screen.width + "x" + Screen.height,
            Quote(SystemInfo.graphicsDeviceName));

        writer.WriteLine(line);
        writer.Flush();
        ResetInterval();
    }

    private void ResetInterval()
    {
        frames = 0;
        intervalTime = 0f;
        frameMsMax = 0f;
        mainMsSum = waitGpuMsSum = gpuMsSum = 0;
        gpuSamples = 0;
        batchesSum = setPassSum = drawSum = trisSum = gcSum = 0;
        batchesMax = setPassMax = 0;
    }

    private static string Quote(string s)
    {
        return "\"" + (s ?? "").Replace("\"", "'") + "\"";
    }

    void OnGUI()
    {
        string off = OffLabel();
        if (!recording && off == "-") return;
        GUI.color = Color.red;
        if (recording) GUI.Label(new Rect(Screen.width - 70, 10, 60, 25), "REC");
        if (off != "-") GUI.Label(new Rect(Screen.width - 260, 30, 250, 25), "OFF : " + off);
    }
}
#endif
