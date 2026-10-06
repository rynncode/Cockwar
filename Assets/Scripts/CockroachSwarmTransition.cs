using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Cockroaches swarm in, cover the whole screen, then either:
///  - Play(): fire onCovered (swap panels), then swarm out.
///  - LoadSceneWithSwarm("SceneName"): load a scene while covered, then swarm out.
/// </summary>
public class CockroachSwarmTransition : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Full-screen stretched RectTransform under the Canvas. Starts disabled.")]
    public RectTransform swarmRoot;
    [Tooltip("Cockroach sprite. Should face UP in the image.")]
    public Sprite roachSprite;
    [Tooltip("Optional solid Image under SwarmRoot so no gaps show.")]
    public Image backdrop;
    public Color backdropColor = new Color(0.04f, 0.12f, 0.1f, 1f);

    [Header("Food Splat")]
    [Tooltip("Food splat sprite. Leave empty to skip the splat.")]
    public Sprite splatSprite;
    [Tooltip("Splat height as a fraction of screen height.")]
    public float splatScreenFraction = 0.85f;
    public float splatTime = 0.25f;      // how fast it slaps onto the screen
    public float splatHold = 0.45f;      // pause (with a slow drip) before roaches arrive
    public int droplets = 10;            // little flying blobs around the main splat

    [Header("Swarm")]
    public int roachCount = 300;
    public float sizeMultiplier = 2.2f;
    public float travelTime = 0.7f;
    public float staggerTime = 0.6f;
    public float holdTime = 0.25f;
    public float wobbleDegrees = 10f;

    [Header("Loading Screen (optional, used by LoadSceneWithSwarm)")]
    [Tooltip("Your LoadingScreenObject (text, spinner, progress bar). Shown on top of the swarm while the scene loads.")]
    public GameObject loadingScreenObject;
    public Slider progressBar;
    [Tooltip("Optional: rotated while loading.")]
    public RectTransform spinner;
    [Tooltip("Minimum seconds the loading screen stays up, so it never just flashes.")]
    public float minLoadingTime = 1f;
    [Tooltip("How fast the bar fills (units per second). Lower = slower, smoother fill.")]
    public float barFillSpeed = 1.5f;

    [Header("Audio (all optional)")]
    public AudioClip splatSfx;       // plays when the food hits the screen
    public AudioClip swarmInSfx;     // plays when the roaches start rushing in
    public AudioClip swarmOutSfx;    // plays when they run back out
    [Tooltip("Your SFX mixer group, so the volume slider in Settings controls these.")]
    public AudioMixerGroup sfxMixerGroup;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Tooltip("When the swarm sounds are cut off, as a fraction of Travel Time after the last roach starts. Lower = sound ends sooner.")]
    [Range(0.1f, 1f)] public float swarmSfxCutoff = 0.6f;
    [Tooltip("Seconds the swarm sound takes to fade out before the cutoff.")]
    public float sfxTailFade = 0.25f;

    [Header("Events")]
    [Tooltip("Fires while covered (used by Play()). Swap panels here.")]
    public UnityEvent onCovered;
    [Tooltip("Fires while covered for PlaySwarmOnly() (swarm with no splat). Use for Back buttons.")]
    public UnityEvent onCoveredSwarmOnly;
    public UnityEvent onFinished;

    class Roach
    {
        public RectTransform rt;
        public Vector2 offscreen;
        public Vector2 target;
        public float delay;
        public float seed;
    }

    readonly List<Roach> roaches = new List<Roach>();
    readonly List<Image> splatImages = new List<Image>();
    bool running;

    // ---------- Public entry points ----------

    /// <summary>Cover -> onCovered (panel swap) -> uncover.</summary>
    public void Play()
    {
        Debug.Log("[SwarmTransition] Play() called");
        if (running || !Validate()) return;
        StartCoroutine(PanelRoutine(onCovered, true));
    }

    /// <summary>Swarm only, no food splat. Fires onCoveredSwarmOnly. Use for Back buttons.</summary>
    public void PlaySwarmOnly()
    {
        Debug.Log("[SwarmTransition] PlaySwarmOnly() called");
        if (running || !Validate()) return;
        StartCoroutine(PanelRoutine(onCoveredSwarmOnly, false));
    }

    /// <summary>Cover -> load scene -> uncover. Use from a level button's OnClick.</summary>
    public void LoadSceneWithSwarm(string sceneName)
    {
        Debug.Log("[SwarmTransition] LoadSceneWithSwarm(" + sceneName + ")");
        if (running || !Validate()) return;
        running = true;

        // The menu Canvas is destroyed on scene load, so move the swarm to its own
        // persistent canvas and run the coroutine from a persistent object.
        var host = new GameObject("SwarmPersistentCanvas");
        var canvas = host.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        var scaler = host.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        host.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(host);

        swarmRoot.SetParent(host.transform, false);
        host.AddComponent<SwarmRunner>().StartCoroutine(LoadRoutine(sceneName, host));
    }

    // ---------- Routines ----------

    IEnumerator PanelRoutine(UnityEvent covered, bool withSplat)
    {
        running = true;
        yield return Cover(withSplat);
        covered?.Invoke();
        yield return new WaitForSecondsRealtime(holdTime);
        yield return Uncover();
        onFinished?.Invoke();
        running = false;
    }

    IEnumerator LoadRoutine(string sceneName, GameObject host)
    {
        yield return Cover();

        // Show the loading screen on top of the swarm
        if (loadingScreenObject)
        {
            loadingScreenObject.transform.SetParent(swarmRoot, false);
            loadingScreenObject.transform.SetAsLastSibling();
            loadingScreenObject.SetActive(true);
        }
        if (progressBar) progressBar.value = progressBar.minValue;

        // Load in the background, but hold the new scene until the bar is full
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op != null)
        {
            op.allowSceneActivation = false;
            float shown = 0f;
            float start = Time.unscaledTime;
            float minTime = loadingScreenObject ? minLoadingTime : 0f;

            while (true)
            {
                float target = Mathf.Clamp01(op.progress / 0.9f); // Unity reports 0..0.9 until activation
                shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * barFillSpeed);
                if (progressBar)
                    progressBar.value = Mathf.Lerp(progressBar.minValue, progressBar.maxValue, shown);
                if (spinner) spinner.Rotate(0f, 0f, -360f * Time.unscaledDeltaTime);

                if (op.progress >= 0.9f && shown >= 0.999f && Time.unscaledTime - start >= minTime)
                    break;
                yield return null;
            }

            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
        }

        if (loadingScreenObject) loadingScreenObject.SetActive(false);
        yield return null; // let the new scene start up
        yield return new WaitForSecondsRealtime(holdTime);

        yield return Uncover();
        Destroy(host); // also removes SwarmRoot
    }

    bool splatActive;

    IEnumerator Cover(bool withSplat = true)
    {
        splatActive = withSplat && splatSprite;
        swarmRoot.gameObject.SetActive(true);
        Stretch(swarmRoot);
        swarmRoot.SetAsLastSibling();
        if (backdrop)
        {
            Stretch(backdrop.rectTransform);
            backdrop.transform.SetAsFirstSibling();
            backdrop.raycastTarget = true;
            SetBackdrop(0f);
        }
        Canvas.ForceUpdateCanvases();
        ClearAll();

        if (splatActive) yield return SplatIn();   // 1) food splats on screen
        Build();                                   // roaches spawn off-screen, above the splat
        yield return Animate(true);                // 2) roaches swarm onto it
        ClearSplat();
        if (backdrop) SetBackdrop(1f);
    }

    IEnumerator Uncover()
    {
        if (backdrop) SetBackdrop(0f);
        yield return Animate(false);
        ClearAll();
        swarmRoot.gameObject.SetActive(false);
    }

    // ---------- Internals ----------

    bool Validate()
    {
        if (!swarmRoot || !roachSprite)
        {
            Debug.LogError("[SwarmTransition] Assign Swarm Root and Roach Sprite!", this);
            return false;
        }
        return true;
    }

    void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    void Build()
    {
        Rect r = swarmRoot.rect;
        float aspect = r.width / r.height;
        int cols = Mathf.CeilToInt(Mathf.Sqrt(roachCount * aspect));
        int rows = Mathf.CeilToInt((float)roachCount / cols);
        float cellW = r.width / cols;
        float cellH = r.height / rows;
        float size = Mathf.Max(cellW, cellH) * sizeMultiplier;
        float outDist = r.size.magnitude * 0.65f;
        float maxDist = r.size.magnitude * 0.5f;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                var go = new GameObject("Roach", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(swarmRoot, false);

                var img = go.GetComponent<Image>();
                img.sprite = roachSprite;
                img.raycastTarget = false;
                img.preserveAspect = true;

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = Vector2.one * size * Random.Range(0.9f, 1.15f);

                Vector2 cell = new Vector2(
                    r.xMin + (col + 0.5f) * cellW,
                    r.yMin + (row + 0.5f) * cellH);
                Vector2 jitter = new Vector2(Random.Range(-0.3f, 0.3f) * cellW,
                                             Random.Range(-0.3f, 0.3f) * cellH);

                Vector2 dir = (cell.sqrMagnitude < 1f ? Random.insideUnitCircle : cell).normalized;
                dir = (dir + Random.insideUnitCircle * 0.35f).normalized;

                roaches.Add(new Roach
                {
                    rt = rt,
                    target = cell + jitter,
                    offscreen = dir * outDist,
                    // roaches near the splat arrive first, the swarm spreads outward from it
                    delay = splatActive
                        ? (Mathf.Clamp01(cell.magnitude / maxDist) * 0.7f + Random.value * 0.3f) * staggerTime
                        : Random.value * staggerTime,
                    seed = Random.value * 100f
                });
                rt.anchoredPosition = dir * outDist;
            }
        }
    }

    IEnumerator Animate(bool toTarget)
    {
        AudioSource sfx = PlaySfx(toTarget ? swarmInSfx : swarmOutSfx);
        float total = staggerTime + travelTime;
        float t = 0f;
        while (t < total)
        {
            t += Time.unscaledDeltaTime;

            // fade the swarm sound out with the roaches, then cut it
            if (sfx)
            {
                float left = staggerTime + travelTime * swarmSfxCutoff - t;
                if (left <= 0f) { Destroy(sfx.gameObject); sfx = null; }
                else sfx.volume = sfxVolume * Mathf.Clamp01(left / Mathf.Max(0.01f, sfxTailFade));
            }

            if (toTarget && splatImages.Count > 0)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.9f, t / total));
                foreach (var im in splatImages)
                {
                    if (!im) continue;
                    var c = im.color; c.a = 1f - k; im.color = c;
                    im.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.8f, k);
                }
            }

            foreach (var ro in roaches)
            {
                float d = toTarget ? ro.delay : staggerTime - ro.delay;
                float p = Mathf.Clamp01((t - d) / travelTime);
                float e = 1f - Mathf.Pow(1f - p, 3f);

                Vector2 from = toTarget ? ro.offscreen : ro.target;
                Vector2 to = toTarget ? ro.target : ro.offscreen;

                Vector2 moveDir = to - from;
                float angle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg - 90f;
                float wiggle = (p > 0f && p < 1f)
                    ? Mathf.Sin(Time.unscaledTime * 25f + ro.seed) * wobbleDegrees : 0f;

                ro.rt.anchoredPosition = Vector2.Lerp(from, to, e);
                ro.rt.localRotation = Quaternion.Euler(0, 0, angle + wiggle);
            }
            yield return null;
        }

        if (sfx) Destroy(sfx.gameObject);
        foreach (var ro in roaches)
            ro.rt.anchoredPosition = toTarget ? ro.target : ro.offscreen;
    }

    void SetBackdrop(float a)
    {
        var c = backdropColor; c.a = a; backdrop.color = c;
    }

    Image MakeSplat(float size, Vector2 pos)
    {
        var go = new GameObject("Splat", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(swarmRoot, false);
        var img = go.GetComponent<Image>();
        img.sprite = splatSprite;
        img.raycastTarget = false;
        img.preserveAspect = true;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.one * size;
        rt.anchoredPosition = pos;
        rt.localScale = Vector3.zero;
        splatImages.Add(img);
        return img;
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        x = Mathf.Clamp01(x);
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    // Plays on a temporary persistent object, so it keeps playing across scene loads.
    AudioSource PlaySfx(AudioClip clip)
    {
        if (!clip) return null;
        var go = new GameObject("SwarmSFX");
        DontDestroyOnLoad(go);
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = sfxVolume;
        src.spatialBlend = 0f; // 2D sound
        src.outputAudioMixerGroup = sfxMixerGroup;
        src.Play();
        Destroy(go, clip.length + 0.2f);
        return src;
    }

    IEnumerator SplatIn()
    {
        PlaySfx(splatSfx);
        Rect r = swarmRoot.rect;
        float h = r.height * splatScreenFraction;

        var main = MakeSplat(h, Vector2.zero);
        main.rectTransform.localRotation = Quaternion.Euler(0, 0, Random.Range(-12f, 12f));

        // little droplets flying out from the impact
        var dropRts = new List<RectTransform>();
        var dropDirs = new List<Vector2>();
        for (int i = 0; i < droplets; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            var d = MakeSplat(h * Random.Range(0.08f, 0.2f), Vector2.zero);
            d.rectTransform.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            dropRts.Add(d.rectTransform);
            dropDirs.Add(new Vector2(dir.x * r.width * 0.45f, dir.y * r.height * 0.45f)
                         * Random.Range(0.5f, 1f));
        }

        float total = splatTime + splatHold;
        float t = 0f;
        while (t < total)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / splatTime);

            // SLAP: pops in with a squashy overshoot
            float s = EaseOutBack(p);
            main.rectTransform.localScale = new Vector3(s * (1f + (1f - p) * 0.25f), s * (1f - (1f - p) * 0.15f), 1f);

            // slow drip downward while it sits there
            float hold = Mathf.Clamp01((t - splatTime) / Mathf.Max(0.01f, splatHold));
            main.rectTransform.anchoredPosition = new Vector2(0f, -h * 0.05f * hold);

            for (int i = 0; i < dropRts.Count; i++)
            {
                float e = 1f - Mathf.Pow(1f - p, 3f);
                dropRts[i].anchoredPosition = dropDirs[i] * e;
                dropRts[i].localScale = Vector3.one * s;
            }
            yield return null;
        }
    }

    void ClearSplat()
    {
        foreach (var im in splatImages)
            if (im) Destroy(im.gameObject);
        splatImages.Clear();
    }

    void ClearRoaches()
    {
        foreach (var ro in roaches)
            if (ro.rt) Destroy(ro.rt.gameObject);
        roaches.Clear();
    }

    void ClearAll()
    {
        ClearSplat();
        ClearRoaches();
    }
}