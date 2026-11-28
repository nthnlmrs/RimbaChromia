using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Main menu (GDD Asset List: Homescreen background, Play, Exit, Title; Setting: volume slider).
/// - Latar: hutan 3D dari model background (sama dengan di stage) dengan sinar matahari ke tengah kebun,
///   kabut tipis & kamera bergerak pelan.
/// - UI: papan judul, tombol kayu PLAY / SETTINGS / QUIT, panel settings SOUND & MUSIC.
/// Semua dibuat lewat kode; sprite diambil dari Resources/UISkin (bagian Main menu).
/// Kalau art homescreen final dari artist sudah ada, cukup ganti sprite di UISkin.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Tooltip("Aset Data/ArenaArt: model hutan & pengaturan sinar matahari untuk latar menu")]
    [SerializeField] private ArenaArt environment;
    [SerializeField] private string gameScene = "Game";
    [SerializeField] private string title = "RIMBA CHROMIA";
    [SerializeField] private string subtitle = "Protect the forest from the fire of greed";

    [Header("Kamera")]
    [SerializeField] private Vector3 cameraPosition = new Vector3(0f, 2.8f, -7.5f);
    [SerializeField] private Vector3 cameraLookAt = new Vector3(0f, 2.3f, 5f);
    [SerializeField] private float cameraSway = 0.35f;

    private static readonly Color TitleGreen = new Color(0.33f, 0.43f, 0.27f);
    private static readonly Vector2 ButtonSize = new Vector2(450f, 136f);
    private const float ButtonSpacing = 162f;
    private const float MenuLeft = 110f;

    private Camera cam;
    private GameObject settingsOverlay;
    private CanvasGroup menuGroup;
    private GameHUD.VolumeRow soundRow;
    private GameHUD.VolumeRow musicRow;
    private float introFade;
    private bool leaving;

    private void Start()
    {
        Time.timeScale = 1f;
        SetupLightAndCamera();
        // Ada gambar latar dari artist -> pakai itu; kalau belum, latar hutan 3D
        UISkin skin = UISkin.Current;
        if (skin == null || skin.menuBackground == null) BuildEnvironment();
        BuildUI();
    }

    // ------------------------------------------------------------------
    // Latar 3D
    // ------------------------------------------------------------------

    private void SetupLightAndCamera()
    {
        Light sun = RenderSettings.sun;
        if (sun == null)
        {
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) { sun = l; break; }
            }
        }
        if (sun == null)
        {
            // Matahari sore hangat dari kanan belakang, supaya sinar terlihat miring ke arah kamera
            GameObject go = new GameObject("Menu Sun");
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.92f, 0.78f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
        }
        RenderSettings.sun = sun;

        // Kabut hijau gelap: hutan di kejauhan memudar, terasa dalam
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.07f, 0.11f, 0.08f);
        RenderSettings.fogDensity = 0.035f;

        cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.fieldOfView = 50f;
            cam.transform.position = cameraPosition;
            cam.transform.LookAt(cameraLookAt);
        }
    }

    private void BuildEnvironment()
    {
        if (environment == null || environment.backgroundEnvironment == null) return;

        Transform root = new GameObject("Menu Forest").transform;
        Vector2 clearing = new Vector2(21f, 17f);

        // Lantai hutan di bawah semuanya (lubang tengah model background juga tertutup)
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Forest Floor";
        Destroy(ground.GetComponent<Collider>());
        ground.transform.SetParent(root, false);
        ground.transform.position = new Vector3(0f, -0.02f, 0f);
        ground.transform.localScale = new Vector3(14f, 1f, 14f);
        Renderer groundRenderer = ground.GetComponent<Renderer>();
        groundRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Pohon di dalam "lubang" model: rapat di kiri, kanan & belakang, kebun terbuka di tengah untuk sinar
        List<Vector3> trees = new List<Vector3>();
        List<Vector3> bushes = new List<Vector3>();
        for (float x = -clearing.x * 0.5f; x <= clearing.x * 0.5f; x += 1.3f)
        {
            for (float z = -clearing.y * 0.5f; z <= clearing.y * 0.5f; z += 1.3f)
            {
                Vector3 p = new Vector3(x + Random.Range(-0.4f, 0.4f), 0f, z + Random.Range(-0.4f, 0.4f));
                float open = new Vector2(p.x / 6.5f, (p.z - 1f) / 4.5f).magnitude; // elips kebun terbuka
                if (open < 1f) continue;
                if (p.z < -3f) continue; // depan: tempat kamera berdiri, jangan ada yang menghalangi
                if (p.z < 0f) bushes.Add(p); // dekat kamera: semak pendek
                else trees.Add(p);
            }
        }

        GameObject forest = environment.SpawnBackground(Vector3.zero, clearing, root, trees, bushes);
        ApplyFloorMaterial(groundRenderer, forest);
        SunRays.Create(environment, new Rect(-clearing.x * 0.5f, -clearing.y * 0.5f + 1f, clearing.x, clearing.y), 0f, root);
    }

    /// <summary>
    /// Lantai memakai salinan material tanah hutan dari model background (tekstur rumput diulang),
    /// bukan material baru buatan kode (material Lit kosong bisa tampil terang / memantulkan langit).
    /// </summary>
    private void ApplyFloorMaterial(Renderer floor, GameObject forest)
    {
        Material source = null;
        if (forest != null)
        {
            foreach (Renderer r in forest.GetComponentsInChildren<Renderer>())
            {
                if (r.sharedMaterial != null && r.sharedMaterial.name.Contains("Ground")) { source = r.sharedMaterial; break; }
            }
        }
        if (source == null) return;

        Material m = new Material(source) { name = "Menu Forest Floor" };
        m.mainTextureScale = new Vector2(30f, 30f);
        if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", new Vector2(30f, 30f));
        if (m.HasProperty("_EmissionMap")) m.SetTextureScale("_EmissionMap", new Vector2(30f, 30f));
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
        Color c = environment.outerGroundColor * environment.outsideBrightness * 2f;
        c.a = 1f;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
        floor.sharedMaterial = m;
    }

    private void LateUpdate()
    {
        if (cam != null && cameraSway > 0f)
        {
            // Kamera "bernapas" pelan supaya pohon & sinar terasa hidup
            float t = Time.unscaledTime;
            Vector3 offset = new Vector3(Mathf.Sin(t * 0.13f) * 1.2f, Mathf.Sin(t * 0.21f) * 0.25f, Mathf.Sin(t * 0.09f) * 0.6f) * cameraSway;
            cam.transform.position = cameraPosition + offset;
            cam.transform.LookAt(cameraLookAt);
        }

        if (menuGroup != null)
        {
            introFade = Mathf.MoveTowards(introFade, leaving ? 0f : 1f, Time.unscaledDeltaTime * 1.5f);
            menuGroup.alpha = introFade;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && settingsOverlay != null && settingsOverlay.activeSelf)
        {
            settingsOverlay.SetActive(false);
        }
    }

    // ------------------------------------------------------------------
    // UI
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        UIKit.EnsureEventSystem();
        UISkin skin = UISkin.Current;

        Canvas canvas = UIKit.CreateCanvas("Main Menu Canvas", 10);
        RectTransform root = (RectTransform)canvas.transform;
        menuGroup = canvas.gameObject.AddComponent<CanvasGroup>();
        menuGroup.alpha = 0f;

        if (skin != null && skin.menuBackground != null)
        {
            // Latar lukisan layar penuh (dipotong menyesuaikan rasio layar, tidak gepeng)
            Image bg = UIKit.Panel("Background", root, Color.white, false);
            bg.sprite = skin.menuBackground;
            UIKit.Stretch(bg.rectTransform);
            bg.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            bg.GetComponent<AspectRatioFitter>().aspectRatio = skin.menuBackground.rect.width / skin.menuBackground.rect.height;
            bg.transform.SetAsFirstSibling();
        }

        // Gelap tipis di kiri supaya logo & tombol terbaca
        Image shade = UIKit.Panel("Shade", root, new Color(0f, 0f, 0f, 0.18f), false);
        UIKit.Stretch(shade.rectTransform);

        // Taduh di kanan bawah, bernapas pelan
        if (skin != null && skin.menuCharacter != null)
        {
            Image taduh = UIKit.ArtImage("Taduh", root, skin.menuCharacter, 1f);
            float h = 520f;
            Vector2 size = new Vector2(h * skin.menuCharacter.rect.width / skin.menuCharacter.rect.height, h);
            UIKit.Place(taduh.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-330f, 30f), size);
            taduh.gameObject.AddComponent<MenuIdleBob>();
        }

        // Logo kiri atas (sesuai mockup Home UI)
        if (skin != null && skin.menuLogo != null)
        {
            Image logo = UIKit.ArtImage("Logo", root, skin.menuLogo, 1f);
            float w = 860f;
            UIKit.Place(logo.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(MenuLeft, -120f),
                new Vector2(w, w * skin.menuLogo.rect.height / skin.menuLogo.rect.width));
        }
        else
        {
            TextMeshProUGUI titleText = UIKit.ArtLabel("Title Text", root, title, 104f, TextAlignmentOptions.Left, TitleGreen);
            UIKit.Place(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(MenuLeft, -120f), new Vector2(900f, 150f));
        }

        // Tombol kolom kiri: START, SETTINGS, QUIT
        float y = -330f;
        MenuButton(root, skin?.menuStartButton, "START", new Vector2(MenuLeft, y), Play);
        MenuButton(root, skin?.menuSettingsButton, "SETTINGS", new Vector2(MenuLeft, y - ButtonSpacing), OpenSettings);
        MenuButton(root, skin?.menuQuitButton, "QUIT", new Vector2(MenuLeft, y - ButtonSpacing * 2f), Quit);


        BuildSettings(root, skin);
    }

    /// <summary>
    /// Tombol kayu dari artist (teks sudah ada di gambar); posisi = pojok kiri-atas tombol dari pojok kiri-atas layar.
    /// Kalau sprite kosong, dibuat tombol placeholder bertulisan label.
    /// </summary>
    private Button MenuButton(RectTransform parent, Sprite sprite, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        Button button = sprite != null
            ? UIKit.ArtButton(label, parent, sprite, null, 1f, onClick)
            : UIKit.Button(label, parent, label, 40f, onClick);
        RectTransform rt = (RectTransform)button.transform;
        UIKit.Place(rt, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), position + new Vector2(ButtonSize.x * 0.5f, -ButtonSize.y * 0.5f), ButtonSize);
        if (sprite != null) button.GetComponent<Image>().preserveAspect = true;

        button.gameObject.AddComponent<MenuButtonHover>();
        return button;
    }

    private void BuildSettings(RectTransform root, UISkin skin)
    {
        Image dim = UIKit.Panel("Settings", root, new Color(0f, 0f, 0f, 0.55f));
        UIKit.Stretch(dim.rectTransform);
        settingsOverlay = dim.gameObject;

        if (skin == null || skin.menuSettingsPanel == null || skin.volumeLeafEmpty == null)
        {
            Button back = UIKit.Button("Back", dim.transform, "BACK", 32f, CloseSettings);
            UIKit.Place((RectTransform)back.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 70f));
            settingsOverlay.SetActive(false);
            return;
        }

        // Koordinat pixel panel 795x629 (sama dengan panel pause di game)
        Image panel = UIKit.ArtImage("Panel", dim.transform, skin.menuSettingsPanel);
        UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), panel.rectTransform.sizeDelta);
        RectTransform p = panel.rectTransform;
        panel.raycastTarget = true;

        TextMeshProUGUI heading = UIKit.ArtLabel("Heading", p, "SETTINGS", 92f, TextAlignmentOptions.Center, TitleGreen);
        UIKit.PlaceOnArt(heading.rectTransform, new Vector2(380f, 78f), new Vector2(520f, 110f));

        Button close = UIKit.ArtButton("Close", p, skin.exitNormal, skin.exitPressed, UIKit.ArtScale, CloseSettings);
        UIKit.PlaceOnArt((RectTransform)close.transform, new Vector2(740f, 74f), new Vector2(102f, 102f));

        soundRow = new GameHUD.VolumeRow(p, skin, 275f, () => GameSettings.SoundLevel, v => GameSettings.SoundLevel = v);
        musicRow = new GameHUD.VolumeRow(p, skin, 374f, () => GameSettings.MusicLevel, v => GameSettings.MusicLevel = v);
        GameSettings.OnChanged += RefreshVolume;

        PanelButton(p, "BACK", new Vector2(196.5f, 553.5f), CloseSettings);
        PanelButton(p, "DEFAULT", new Vector2(565.5f, 553.5f), () =>
        {
            GameSettings.SoundLevel = GameSettings.MaxLevel;
            GameSettings.MusicLevel = GameSettings.MaxLevel;
        });

        settingsOverlay.SetActive(false);
    }

    /// <summary>Tombol transparan di atas gambar tombol pada panel + teksnya.</summary>
    private static void PanelButton(RectTransform panel, string label, Vector2 pixelCenter, UnityEngine.Events.UnityAction onClick)
    {
        Image hit = UIKit.Panel(label, panel, new Color(1f, 1f, 1f, 0f));
        UIKit.PlaceOnArt(hit.rectTransform, pixelCenter, new Vector2(303f, 129f));
        Button button = hit.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.onClick.AddListener(AudioManager.Click);
        button.onClick.AddListener(onClick);

        TextMeshProUGUI text = UIKit.ArtLabel("Label", hit.rectTransform, label, 50f);
        UIKit.Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(0f, 10f);
        text.raycastTarget = false;
        hit.gameObject.AddComponent<MenuButtonHover>();
    }

    private void OnDestroy()
    {
        GameSettings.OnChanged -= RefreshVolume;
    }

    private void RefreshVolume()
    {
        soundRow?.Refresh();
        musicRow?.Refresh();
    }

    // ------------------------------------------------------------------
    // Aksi
    // ------------------------------------------------------------------

    private void Play()
    {
        if (leaving) return;
        leaving = true;
        WaveManager.SelectedStageIndex = -1; // mulai dari awal (Opening & Tutorial)
        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadScene(gameScene);
        else SceneManager.LoadScene(gameScene);
    }

    private void OpenSettings()
    {
        RefreshVolume();
        settingsOverlay.SetActive(true);
    }

    private void CloseSettings()
    {
        settingsOverlay.SetActive(false);
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

/// <summary>Gerak naik-turun pelan (karakter "bernapas").</summary>
public class MenuIdleBob : MonoBehaviour
{
    private RectTransform rt;
    private Vector2 basePosition;

    private void Start()
    {
        rt = (RectTransform)transform;
        basePosition = rt.anchoredPosition;
    }

    private void Update()
    {
        float t = Time.unscaledTime;
        rt.anchoredPosition = basePosition + new Vector2(0f, Mathf.Sin(t * 1.6f) * 6f);
        rt.localScale = new Vector3(1f, 1f + Mathf.Sin(t * 1.6f + 0.6f) * 0.012f, 1f);
    }
}

/// <summary>Tombol membesar sedikit saat disorot mouse, mengecil saat ditekan.</summary>
public class MenuButtonHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler,
    UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler
{
    private float target = 1f;
    private float current = 1f;
    private bool hovered;

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { hovered = true; target = 1.06f; }
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { hovered = false; target = 1f; }
    public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) { target = 0.95f; }
    public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) { target = hovered ? 1.06f : 1f; }

    private void Update()
    {
        current = Mathf.MoveTowards(current, target, Time.unscaledDeltaTime * 1.2f);
        transform.localScale = Vector3.one * current;
    }
}
