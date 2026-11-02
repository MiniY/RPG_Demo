using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Standalone visual acceptance only. No game services, spawning, combat or save access.
public sealed class TinySwordsEnemyPackPreview : MonoBehaviour
{
    [Serializable] public sealed class ClipEntry
    {
        public string name;
        public AnimationClip clip;
        public Sprite[] frames;
        public int[] durationsMs;
        public bool loop;
        public float Duration => durationsMs.Sum(x => x) / 1000f;
    }
    [Serializable] public sealed class Entry
    {
        public string id, category, variant, direction;
        public GameObject prefab;
        public ClipEntry[] states;
    }
    [Serializable] sealed class Check
    {
        public string prefab, state, screenshot, terminalScreenshot;
        public int frames, sampledFrames;
        public float durationSeconds;
        public float clipDurationSeconds;
        public bool pauseResetLoopPassed, animatorReferencesPassed;
    }
    [Serializable] sealed class PlayerEvidence
    {
        public string status, error, unity;
        public string scene = "TinySwordsEnemyPackPreview";
        public string gameplay = "GAMEPLAY_NOT_WIRED";
        public string interaction = "Same selection/play/pause/reset/loop methods exercised in Player; human keyboard/mouse acceptance NOT_RUN";
        public int prefabs, states;
        public bool isolatedScene, saveApisUsed = false;
        public Check[] checks;
    }
    public Entry[] entries;
    GameObject instance;
    Camera visualCamera;
    Animator animator;
    SpriteRenderer spriteRenderer;
    int entryIndex, stateIndex;
    float elapsed, zoom = 1.35f;
    bool paused, loop, mirrored, automated;
    string acceptanceRoot, error;
    readonly List<Check> checks = new List<Check>();
    ClipEntry Current => entries[entryIndex].states[stateIndex];

    static string Arg(string key)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i+1];
        return null;
    }
    void Start()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        visualCamera = FindObjectsOfType<Camera>().SingleOrDefault(c => c.name == "PreviewCamera");
        if (!visualCamera) { Fail("Missing unique PreviewCamera"); return; }
        visualCamera.tag = "MainCamera";
        UpdateCamera();
        // The visual camera only clears its viewport. Clear the remaining GUI area
        // each frame as well so changing labels cannot leave previous pixels behind.
        var background = new GameObject("PreviewBackground").AddComponent<Camera>();
        background.depth = -100;
        background.cullingMask = 0;
        background.clearFlags = CameraClearFlags.SolidColor;
        background.backgroundColor = new Color(.075f, .09f, .12f);
        acceptanceRoot = Arg("--enemy-accept");
        automated = !string.IsNullOrEmpty(acceptanceRoot);
        if (entries == null || entries.Length == 0) { Fail("Empty preview catalog"); return; }
        SelectEntry(0);
        if (automated)
        {
            Directory.CreateDirectory(Path.Combine(acceptanceRoot, "Screenshots"));
            StartCoroutine(Observe(Acceptance()));
        }
    }
    void Update()
    {
        if (!automated) Advance(Time.unscaledDeltaTime);
        UpdateCamera();
    }
    void UpdateCamera()
    {
        if (visualCamera)
        {
            visualCamera.rect = new Rect(.32f, .13f, .67f, .74f);
            visualCamera.orthographicSize = 4 / zoom;
        }
    }
    void SelectEntry(int index)
    {
        entryIndex = (index + entries.Length) % entries.Length;
        stateIndex = 0;
        if (instance) { instance.SetActive(false); Destroy(instance); }
        instance = Instantiate(entries[entryIndex].prefab);
        instance.transform.position = Vector3.zero;
        animator = instance.GetComponentInChildren<Animator>();
        spriteRenderer = instance.GetComponentInChildren<SpriteRenderer>();
        if (!animator || !spriteRenderer || !animator.runtimeAnimatorController) throw new InvalidOperationException("Missing preview visual references");
        animator.speed = 0;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        SelectState(0);
    }
    void SelectState(int index)
    {
        stateIndex = (index + entries[entryIndex].states.Length) % entries[entryIndex].states.Length;
        loop = Current.loop;
        paused = false;
        ResetAction();
    }
    void ResetAction() { elapsed = 0; ApplyTime(); }
    void Advance(float delta)
    {
        if (paused) return;
        elapsed += delta;
        float duration = Current.Duration;
        elapsed = loop ? elapsed % duration : Mathf.Min(elapsed, duration);
        ApplyTime();
    }
    void ApplyTime()
    {
        // Manual time selection uses the real Animator state and imported Sprite curves.
        // Speed=0 avoids a second, frame-dependent clock while paused or being inspected.
        float normalized = Mathf.Min(.999999f, elapsed / Current.Duration);
        animator.Play(Current.name, 0, normalized);
        animator.Update(0);
        spriteRenderer.flipX = mirrored;
    }
    int FrameAt(float seconds)
    {
        float cursor = 0;
        for (int i = 0; i < Current.durationsMs.Length; i++)
        {
            cursor += Current.durationsMs[i] / 1000f;
            if (seconds < cursor) return i;
        }
        return Current.durationsMs.Length - 1;
    }
    void OnGUI()
    {
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
        var title = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold };
        var text = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
        GUI.Label(new Rect(28, 18, 1210, 38), "TINY SWORDS  /  ENEMY PACK", title);
        GUI.Label(new Rect(28, 58, 1210, 28), "Visual-ready / Gameplay not integrated  |  Private licensed artwork", text);
        if (entries == null || entries.Length == 0) return;
        var entry = entries[entryIndex];
        GUI.Box(new Rect(18, 105, 366, 538), "");
        GUI.Label(new Rect(36, 122, 330, 60), (entryIndex+1) + " / " + entries.Length + "   " + entry.id, title);
        GUI.Label(new Rect(36, 188, 330, 115), entry.category + "\nVariant: " + entry.variant + "\n" + entry.direction, text);
        if (GUI.Button(new Rect(36, 310, 152, 38), "< Previous")) SelectEntry(entryIndex-1);
        if (GUI.Button(new Rect(200, 310, 152, 38), "Next >")) SelectEntry(entryIndex+1);
        GUI.Label(new Rect(36, 363, 325, 55), "State: " + Current.name + "\n" + Current.frames.Length + " frames / " + Current.Duration.ToString("0.00") + " s", text);
        if (GUI.Button(new Rect(36, 426, 152, 38), "< State")) SelectState(stateIndex-1);
        if (GUI.Button(new Rect(200, 426, 152, 38), "State >")) SelectState(stateIndex+1);
        if (GUI.Button(new Rect(36, 480, 152, 38), paused ? "Play" : "Pause")) paused = !paused;
        if (GUI.Button(new Rect(200, 480, 152, 38), "Reset action")) ResetAction();
        loop = GUI.Toggle(new Rect(36, 534, 300, 28), loop, " Loop (off = single playback / hold last)");
        bool flip = GUI.Toggle(new Rect(36, 568, 300, 28), mirrored, " Mirrored view (not authored direction)");
        if (flip != mirrored) { mirrored = flip; ApplyTime(); }
        GUI.Label(new Rect(36, 607, 75, 24), "Zoom", text);
        zoom = GUI.HorizontalSlider(new Rect(110, 615, 230, 24), zoom, .5f, 2.5f);
        GUI.Label(new Rect(425, 634, 820, 48), "Frame " + (FrameAt(elapsed)+1) + " / " + Current.frames.Length +
            "   |   " + elapsed.ToString("0.00") + " s   |   Source timing preserved", text);
        GUI.Label(new Rect(28, 679, 1210, 30), "Arrows: prefab/state   Space: play/pause   R: reset   L: loop   Esc: quit", text);
        if (!string.IsNullOrEmpty(error)) GUI.Label(new Rect(425, 110, 800, 85), error, text);
        var e = Event.current;
        if (e.type != EventType.KeyDown || automated) return;
        switch (e.keyCode)
        {
            case KeyCode.LeftArrow: SelectEntry(entryIndex-1); break;
            case KeyCode.RightArrow: SelectEntry(entryIndex+1); break;
            case KeyCode.UpArrow: SelectState(stateIndex-1); break;
            case KeyCode.DownArrow: SelectState(stateIndex+1); break;
            case KeyCode.Space: paused = !paused; break;
            case KeyCode.R: ResetAction(); break;
            case KeyCode.L: loop = !loop; break;
            case KeyCode.Escape: Application.Quit(0); break;
            default: return;
        }
        e.Use();
    }
    static void CheckThat(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    IEnumerator Acceptance()
    {
        CheckThat(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "TinySwordsEnemyPackPreview", "Unexpected scene");
        CheckThat(FindObjectsOfType<MonoBehaviour>().All(m => m is TinySwordsEnemyPackPreview), "Unexpected gameplay script in preview");
        yield return null;
        CheckThat(Camera.main == visualCamera && visualCamera.rect == new Rect(.32f, .13f, .67f, .74f), "Preview camera ownership/viewport differs");
        float originalZoom = zoom;
        zoom = 2; UpdateCamera();
        CheckThat(Mathf.Approximately(visualCamera.orthographicSize, 2), "Zoom did not change the visual camera");
        zoom = originalZoom; UpdateCamera();
        for (int p = 0; p < entries.Length; p++)
        {
            SelectEntry(p);
            yield return null;
            for (int s = 0; s < entries[p].states.Length; s++)
            {
                SelectState(s);
                yield return null;
                CheckThat(Current.frames.Length == Current.durationsMs.Length && Current.clip, "Invalid clip catalog");
                CheckThat(Mathf.Abs(Current.clip.length-Current.Duration) < .001f,
                    "Clip/source duration differs: " + entries[p].id + "/" + Current.name + " source=" + Current.Duration + " clip=" + Current.clip.length);
                CheckThat(spriteRenderer.sprite == Current.frames[0], "Initial Animator frame differs: " + entries[p].id + "/" + Current.name);
                paused = true; float before = elapsed; var beforeSprite = spriteRenderer.sprite; Advance(.3f);
                CheckThat(elapsed == before && spriteRenderer.sprite == beforeSprite, "Pause advanced animation");
                paused = false; loop = true; Advance(Current.Duration * 2 + .01f);
                CheckThat(elapsed < .02f && spriteRenderer.sprite == Current.frames[0], "Loop did not wrap to the first Sprite");
                loop = false; Advance(Current.Duration * 2);
                CheckThat(FrameAt(elapsed) == Current.frames.Length - 1, "Single playback did not hold terminal frame");
                CheckThat(spriteRenderer.sprite == Current.frames.Last(), "Single playback Animator did not hold terminal Sprite");
                ResetAction();
                CheckThat(spriteRenderer.sprite == Current.frames[0], "Reset did not select first frame");
                int samples = 0;
                for (int f = 0; f < Current.frames.Length; f++)
                {
                    elapsed = (Current.durationsMs.Take(f).Sum() + Current.durationsMs[f] * .5f) / 1000f;
                    ApplyTime();
                    CheckThat(spriteRenderer.sprite == Current.frames[f], "Animator frame/order differs: " + entries[p].id + "/" + Current.name + "/" + f +
                        " expected=" + Current.frames[f].name + " actual=" + (spriteRenderer.sprite ? spriteRenderer.sprite.name : "null") +
                        " sourceTime=" + elapsed + " clipLength=" + Current.clip.length + " animatorLength=" + animator.GetCurrentAnimatorStateInfo(0).length);
                    samples++;
                }
                elapsed = Current.durationsMs.Take(Current.frames.Length/2).Sum() / 1000f + .01f;
                ApplyTime();
                yield return new WaitForEndOfFrame();
                string file = entries[p].id + "__" + Current.name + ".png";
                ScreenCapture.CaptureScreenshot(Path.Combine(acceptanceRoot, "Screenshots", file));
                yield return null;
                var check = new Check { prefab = entries[p].id, state = Current.name, frames = Current.frames.Length,
                    durationSeconds = Current.Duration, clipDurationSeconds = Current.clip.length, sampledFrames = samples, screenshot = "Screenshots/" + file,
                    pauseResetLoopPassed = true, animatorReferencesPassed = true };
                if (Current.name == "Dead" || Current.name.StartsWith("Guard_") || Current.name == "Hit")
                {
                    elapsed = Current.Duration; ApplyTime();
                    yield return new WaitForEndOfFrame();
                    string terminal = entries[p].id + "__" + Current.name + "__last.png";
                    ScreenCapture.CaptureScreenshot(Path.Combine(acceptanceRoot, "Screenshots", terminal));
                    check.terminalScreenshot = "Screenshots/" + terminal;
                    yield return null;
                }
                checks.Add(check);
            }
        }
        yield return new WaitForSecondsRealtime(.5f);
        foreach (var check in checks)
            CheckThat(File.Exists(Path.Combine(acceptanceRoot, check.screenshot)), "Screenshot was not written");
        WriteEvidence("PASS", null);
        Application.Quit(0);
    }
    IEnumerator Observe(IEnumerator inner)
    {
        while (true)
        {
            object next = null;
            bool moved = false;
            Exception failure = null;
            try { moved = inner.MoveNext(); if (moved) next = inner.Current; }
            catch (Exception exception) { failure = exception; }
            if (failure != null) { Fail(failure.ToString()); yield break; }
            if (!moved) yield break;
            yield return next;
        }
    }
    void WriteEvidence(string status, string detail)
    {
        if (string.IsNullOrEmpty(acceptanceRoot)) return;
        File.WriteAllText(Path.Combine(acceptanceRoot, "player-evidence.json"), JsonUtility.ToJson(new PlayerEvidence
        {
            status = status, error = detail, unity = Application.unityVersion,
            prefabs = entries.Length, states = checks.Count, isolatedScene = true, checks = checks.ToArray()
        }, true));
    }
    void Fail(string detail)
    {
        error = detail; Debug.LogError(detail);
        WriteEvidence("FAIL", detail);
        if (automated) Application.Quit(2);
    }
}
