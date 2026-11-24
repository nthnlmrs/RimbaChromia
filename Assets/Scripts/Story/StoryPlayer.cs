using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Pemutar visual novel (overlay layar penuh, dibangun lewat kode).
///  - Teks muncul huruf per huruf; klik / Spasi / Enter = lanjut (atau langsung tampilkan teks penuh)
///  - Tombol "Lewati" untuk melewati seluruh adegan
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
    private TextMeshProUGUI continueHint;

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

        // Kotak dialog
        Image box = UIKit.Panel("Dialogue Box", shakeRoot, new Color(0.05f, 0.08f, 0.06f, 0.9f), false);
        UIKit.Place(box.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1500f, 250f));

        Image border = UIKit.Panel("Border", box.transform, UIKit.Accent, false);
        UIKit.Place(border.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1500f, 4f));

        Image tag = UIKit.Panel("Name Tag", box.transform, UIKit.ButtonColor, false);
        UIKit.Place(tag.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(40f, 0f), new Vector2(260f, 50f));
        nameTag = tag.gameObject;
        nameText = UIKit.Label("Name", tag.transform, "", 28f, TextAlignmentOptions.Center, UIKit.Accent, true);
        UIKit.Stretch(nameText.rectTransform);

        bodyText = UIKit.Label("Text", box.transform, "", 32f, TextAlignmentOptions.TopLeft, UIKit.TextColor);
        UIKit.Place(bodyText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(1400f, 170f));
        bodyText.textWrappingMode = TextWrappingModes.Normal;

        continueHint = UIKit.Label("Continue", box.transform, "Klik / Spasi >>", 20f, TextAlignmentOptions.BottomRight, UIKit.TextMuted);
        UIKit.Place(continueHint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 14f), new Vector2(300f, 28f));

        Button skip = UIKit.Button("Skip", shakeRoot, "Lewati  >>", 22f, Finish);
        UIKit.Place((RectTransform)skip.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(180f, 54f));
    }

    private void ShowLine(int index)
    {
        lineIndex = index;
        DialogueLine line = dialogue.lines[index];

        background.color = line.backgroundColor;
        cg.sprite = line.cg;
        cg.enabled = line.cg != null;

        bool hasSpeaker = !string.IsNullOrWhiteSpace(line.speaker);
        nameTag.SetActive(hasSpeaker);
        nameText.text = hasSpeaker ? line.speaker : "";

        // Narasi (tanpa pembicara) ditulis miring
        bodyText.fontStyle = hasSpeaker ? FontStyles.Normal : FontStyles.Italic;
        bodyText.text = line.text;
        bodyText.ForceMeshUpdate();
        totalChars = bodyText.textInfo.characterCount;
        bodyText.maxVisibleCharacters = 0;
        typed = 0f;

        if (line.shake) shakeTimer = 0.45f;
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
        continueHint.enabled = bodyText.maxVisibleCharacters >= totalChars && Mathf.Repeat(Time.unscaledTime, 1f) < 0.7f;

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
        Time.timeScale = previousTimeScale;
        Action callback = onFinished;
        onFinished = null;
        Destroy(gameObject);
        callback?.Invoke();
    }

    private void OnDestroy()
    {
        // Jaga-jaga kalau scene berganti saat cerita masih jalan
        if (IsPlaying)
        {
            IsPlaying = false;
            Time.timeScale = previousTimeScale;
        }
    }
}
