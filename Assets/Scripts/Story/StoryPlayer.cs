using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Pemutar visual novel (overlay layar penuh, dibangun lewat kode).
///  - Teks muncul huruf per huruf; klik / Spasi / Enter = lanjut (atau langsung tampilkan teks penuh)
///  - CG per baris (atau warna latar placeholder), efek getar untuk ledakan, fade masuk/keluar
///  - Game di-pause selama cerita (Time.timeScale = 0), dikembalikan setelah selesai
/// Pemakaian: StoryPlayer.Play(dialogue, () => { ...lanjut gameplay... });
/// </summary>
public class StoryPlayer : MonoBehaviour
{
    public static bool IsPlaying { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsOnPlay()
    {
        IsPlaying = false;
    }

    private const float CharsPerSecond = 45f;
    private const float FadeDuration = 0.35f;
    private static readonly Vector2 ArrowRest = new Vector2(-56f, 40f);

    private DialogueData dialogue;
    private Action onFinished;
    private float previousTimeScale;

    private CanvasGroup group;
    private RectTransform shakeRoot;
    private Image background;
    private Image cg;
    private GameObject nameTag;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI bodyText;
    private RectTransform continueArrow;
    private Image plainBar;
    private Image taduhBar;

    // Kotak dialog dari artist: 980x235 px, diperbesar supaya pas layar 1920x1080
    private const float BarScale = 1.33f;
    private static readonly Vector2 BarPixels = new Vector2(980f, 235f);
    // Posisi kotak di dalam gambar "Taduh Dialogue Bar" (1167x506) & posisi teks
    private static readonly Vector2 TaduhBarOffset = new Vector2(187f, 135f);

    private int lineIndex = -1;
    private float typed;
    private int totalChars;
    private float shakeTimer;
    private float fade;
    private bool closing;

    public static void Play(DialogueData data, Action finished)
    {
        if (data == null || data.lines == null || data.lines.Length == 0)
        {
            finished?.Invoke();
            return;
        }

        UIKit.EnsureEventSystem();
        GameObject go = new GameObject("Story Player");
        StoryPlayer player = go.AddComponent<StoryPlayer>();
        player.dialogue = data;
        player.onFinished = finished;
        player.Build();
        player.Begin();
    }

    private void Begin()
    {
        IsPlaying = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        fade = 0f;
        AudioManager.StoryStarted();
        ShowLine(0);
    }

    // ------------------------------------------------------------------
    // UI
    // ------------------------------------------------------------------

    private void Build()
    {
        Canvas canvas = UIKit.CreateCanvas("Story Canvas", 100);
        canvas.transform.SetParent(transform, false);
        RectTransform root = (RectTransform)canvas.transform;
        group = canvas.gameObject.AddComponent<CanvasGroup>();

        shakeRoot = UIKit.Stretch(UIKit.Rect("Shake Root", root));

        background = UIKit.Panel("Background", shakeRoot, Color.black);
        UIKit.Stretch(background.rectTransform, -40f); // sedikit lebih besar supaya tepi tidak terlihat saat bergetar

        cg = UIKit.Panel("CG", shakeRoot, Color.white, false);
        UIKit.Stretch(cg.rectTransform);
        cg.preserveAspect = true;

        // Area klik seluruh layar untuk lanjut
        Button advance = background.gameObject.AddComponent<Button>();
        advance.transition = Selectable.Transition.None;
        advance.onClick.AddListener(Advance);

        // Kotak dialog (art: kotak gelap transparan; khusus Taduh ada potret & namanya)
        UISkin skin = UISkin.Current;
        Vector2 barSize = BarPixels * BarScale;
        RectTransform box = UIKit.Rect("Dialogue Box", shakeRoot);
        UIKit.Place(box, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), barSize);

        plainBar = UIKit.Panel("Bar", box, new Color(0f, 0f, 0f, 0.5f), false);
        if (skin != null && skin.storyDialogueBar != null) { plainBar.sprite = skin.storyDialogueBar; plainBar.color = Color.white; }
        UIKit.Stretch(plainBar.rectTransform);

        if (skin != null && skin.storyTaduhBar != null)
        {
            taduhBar = UIKit.ArtImage("Taduh Bar", box, skin.storyTaduhBar, BarScale);
            RectTransform t = taduhBar.rectTransform;
            t.anchorMin = t.anchorMax = new Vector2(0f, 1f);
            t.pivot = new Vector2(0f, 1f);
            t.anchoredPosition = new Vector2(-TaduhBarOffset.x, TaduhBarOffset.y) * BarScale;
        }

        // Nama pembicara selain Taduh (nama Taduh sudah ada di gambarnya)
        Image tag = UIKit.Panel("Name Tag", box, new Color(0f, 0f, 0f, 0f), false);
        UIKit.Place(tag.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -22f), new Vector2(500f, 56f));
        nameTag = tag.gameObject;
        nameText = UIKit.ArtLabel("Name", tag.transform, "", 44f, TextAlignmentOptions.Left);
        UIKit.Stretch(nameText.rectTransform);

        bodyText = UIKit.Label("Text", box, "", 30f, TextAlignmentOptions.TopLeft, UIKit.TextColor);
        bodyText.textWrappingMode = TextWrappingModes.Normal;

        // Segitiga "lanjut" (Next button dari artist, diputar menghadap bawah), memantul saat teks selesai
        Sprite arrowSprite = skin != null ? skin.nextNormal : null;
        if (arrowSprite != null)
        {
            Image arrow = UIKit.ArtImage("Continue", box, arrowSprite, 0.8f);
            continueArrow = arrow.rectTransform;
            continueArrow.localRotation = Quaternion.Euler(0f, 0f, -90f);
        }
        else
        {
            TextMeshProUGUI hint = UIKit.Label("Continue", box, "▼", 30f, TextAlignmentOptions.Center, UIKit.TextColor);
            continueArrow = hint.rectTransform;
            continueArrow.sizeDelta = new Vector2(40f, 40f);
        }
        continueArrow.anchorMin = continueArrow.anchorMax = new Vector2(1f, 0f);
        continueArrow.pivot = new Vector2(0.5f, 0.5f);
        continueArrow.anchoredPosition = ArrowRest;

    }

    private void ShowLine(int index)
    {
        lineIndex = index;
        DialogueLine line = dialogue.lines[index];

        background.color = line.backgroundColor;
        cg.sprite = line.cg;
        cg.enabled = line.cg != null;

        bool hasSpeaker = !string.IsNullOrWhiteSpace(line.speaker);
        bool taduh = hasSpeaker && taduhBar != null && line.speaker.Trim().Equals("Taduh", StringComparison.OrdinalIgnoreCase);
        if (taduhBar != null) taduhBar.gameObject.SetActive(taduh);
        plainBar.enabled = !taduh;
        nameTag.SetActive(hasSpeaker && !taduh);
        nameText.text = hasSpeaker ? line.speaker : "";

        // Teks: di kanan potret Taduh, atau dari kiri kotak; turun sedikit kalau ada nama
        Vector2 barSize = BarPixels * BarScale;
        float left = taduh ? 290f : 60f;
        float top = taduh || hasSpeaker ? 92f : 50f;
        UIKit.Place(bodyText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(left, -top),
            new Vector2(barSize.x - left - 90f, barSize.y - top - 30f));

        // Narasi (tanpa pembicara) ditulis miring
        bodyText.fontStyle = hasSpeaker ? FontStyles.Normal : FontStyles.Italic;
        bodyText.text = line.text;
        bodyText.ForceMeshUpdate();
        totalChars = bodyText.textInfo.characterCount;
        bodyText.maxVisibleCharacters = 0;
        typed = 0f;

        if (line.shake)
        {
            shakeTimer = 0.45f;
            AudioManager.Explosion(); // GDD: "(bunyi ledakan)"
            AudioManager.StoryCue();  // musik gameplay mulai membangun setelah ledakan
        }
    }

    // ------------------------------------------------------------------
    // Alur
    // ------------------------------------------------------------------

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // Fade masuk / keluar
        fade = Mathf.MoveTowards(fade, closing ? 0f : 1f, dt / FadeDuration);
        group.alpha = fade;
        if (closing && fade <= 0f)
        {
            End();
            return;
        }

        // Ketik huruf per huruf
        if (!closing && bodyText.maxVisibleCharacters < totalChars)
        {
            typed += dt * CharsPerSecond;
            bodyText.maxVisibleCharacters = Mathf.Min(totalChars, Mathf.FloorToInt(typed));
        }
        // Segitiga muncul & memantul naik-turun setelah teks selesai diketik
        bool done = bodyText.maxVisibleCharacters >= totalChars;
        AudioManager.SetTyping(!done && !closing);
        continueArrow.gameObject.SetActive(done);
        if (done) continueArrow.anchoredPosition = ArrowRest + new Vector2(0f, Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) * 10f);

        // Getar layar
        if (shakeTimer > 0f)
        {
            shakeTimer -= dt;
            float s = 18f * Mathf.Clamp01(shakeTimer / 0.45f);
            shakeRoot.anchoredPosition = new Vector2(UnityEngine.Random.Range(-s, s), UnityEngine.Random.Range(-s, s));
        }
        else
        {
            shakeRoot.anchoredPosition = Vector2.zero;
        }

        Keyboard kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
        {
            Advance();
        }
    }

    private void Advance()
    {
        if (closing) return;

        // Teks belum selesai diketik -> tampilkan penuh dulu
        if (bodyText.maxVisibleCharacters < totalChars)
        {
            bodyText.maxVisibleCharacters = totalChars;
            typed = totalChars;
            return;
        }

        if (lineIndex + 1 < dialogue.lines.Length) ShowLine(lineIndex + 1);
        else Finish();
    }

    private void Finish()
    {
        closing = true;
    }

    private void End()
    {
        IsPlaying = false;
        AudioManager.SetTyping(false);
        AudioManager.StoryFinished();
        Time.timeScale = previousTimeScale;
        Action callback = onFinished;
        onFinished = null;
        Destroy(gameObject);
        callback?.Invoke();
    }

    private void OnDestroy()
    {
        // Jaga-jaga kalau scene berganti saat cerita masih jalan
        AudioManager.SetTyping(false);
        if (IsPlaying)
        {
            IsPlaying = false;
            Time.timeScale = previousTimeScale;
        }
    }
}
