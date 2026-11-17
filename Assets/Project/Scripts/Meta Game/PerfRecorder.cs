#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Unity.Profiling;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

// Mesures de performance : F3 demarre / arrete l'enregistrement ("REC" a l'ecran).
// Deux fois par seconde, une ligne CSV dans persistentDataPath/perf_<date>.csv
// (meme dossier que Player.log) : scene, etat du cycle, FPS, temps par image, CPU, GPU, rendu, memoire.
// Interrupteurs A/B (colonne "off") : F5 ombres, F6 post-process (Volumes), F7 lumieres sauf directionnelles,
// F8 objets reactifs au cycle (ampoules...). Rien n'est enregistre dans les scenes ; tout revient au changement de scene.
// S'installe tout seul au lancement (rien a poser dans les scenes), editeur et builds de dev seulement.
public class PerfRecorder : MonoBehaviour
{
    // Etat d'origine memorise a l'activation de chaque interrupteur, restaure a la desactivation
    private bool shadowsOff, postOff, lightsOff, reactiveOff;
    private readonly Dictionary<Light, LightShadows> savedShadows = new Dictionary<Light, LightShadows>();
    private readonly Dictionary<Volume, bool> savedVolumes = new Dictionary<Volume, bool>();
    private readonly Dictionary<Light, bool> savedLights = new Dictionary<Light, bool>();
    private readonly Dictionary<GameObject, bool> savedReactive = new Dictionary<GameObject, bool>();

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

    private string OffLabel()
    {
        string s = "";
        if (shadowsOff) s += "ombres+";
        if (postOff) s += "post+";
        if (lightsOff) s += "lumieres+";
        if (reactiveOff) s += "ampoules+";
        return s.Length == 0 ? "-" : s.TrimEnd('+');
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
