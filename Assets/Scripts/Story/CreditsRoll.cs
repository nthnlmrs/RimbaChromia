using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Credit bergulir setelah Ending (GDD Progression: "Good ending of the game and credit").
/// Isi teks diatur di StageData stage terakhir (field Credits). Klik / Space / Esc untuk melewati.
/// Berjalan dengan waktu unscaled karena game dijeda saat Victory.
/// </summary>
public class CreditsRoll : MonoBehaviour
{
    public static bool IsPlaying { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsOnPlay()
    {
        IsPlaying = false;
    }

    private const float ScrollSpeed = 70f;

    private RectTransform content;
    private CanvasGroup group;
    private Action onFinished;
    private float fade;
    private bool closing;
    private float contentHeight;

    public static void Play(string credits, Action finished)
    {
        if (string.IsNullOrWhiteSpace(credits))
        {
            finished?.Invoke();
            return;
        }

        UIKit.EnsureEventSystem();
        GameObject go = new GameObject("Credits Roll");
        CreditsRoll roll = go.AddComponent<CreditsRoll>();
        roll.onFinished = finished;
        roll.Build(credits);
        IsPlaying = true;
    }

    private void Build(string credits)
    {
        Canvas canvas = UIKit.CreateCanvas("Credits Canvas", 110);
        canvas.transform.SetParent(transform, false);
        RectTransform root = (RectTransform)canvas.transform;
        group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        Image background = UIKit.Panel("Background", root, Color.black);
        UIKit.Stretch(background.rectTransform);
        Button skip = background.gameObject.AddComponent<Button>();
        skip.transition = Selectable.Transition.None;
        skip.onClick.AddListener(Close);

        content = UIKit.Rect("Content", root);
        TextMeshProUGUI text = UIKit.Label("Credits", content, credits, 40f, TextAlignmentOptions.Top, UIKit.TextColor);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.richText = true;
        text.raycastTarget = false;
        UIKit.Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1400f, 10f));
        contentHeight = text.GetPreferredValues(credits, 1400f, 0f).y;
        text.rectTransform.sizeDelta = new Vector2(1400f, contentHeight);

        // Mulai dari bawah layar
        UIKit.Place(content, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1400f, contentHeight));

        TextMeshProUGUI hint = UIKit.Label("Hint", root, "Click to skip", 24f, TextAlignmentOptions.BottomRight, UIKit.TextMuted);
        hint.raycastTarget = false;
        UIKit.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 20f), new Vector2(400f, 40f));
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        fade = Mathf.MoveTowards(fade, closing ? 0f : 1f, dt * 1.5f);
        group.alpha = fade;
        if (closing)
        {
            if (fade <= 0f) Finish();
            return;
        }

        content.anchoredPosition += new Vector2(0f, ScrollSpeed * dt);

        RectTransform canvasRect = (RectTransform)group.transform;
        if (content.anchoredPosition.y > canvasRect.rect.height + contentHeight) Close();

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)) Close();
    }

    private void Close()
    {
        closing = true;
    }

    private void Finish()
    {
        IsPlaying = false;
        Action done = onFinished;
        Destroy(gameObject);
        done?.Invoke();
    }
}
