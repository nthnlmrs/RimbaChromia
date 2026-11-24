using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Deretan kartu tower di bawah layar (isi = Tower Hotbar di GridManager).
/// Klik kartu = pilih tower lalu klik tile. Drag kartu ke map = langsung pasang (GDD: drag tower).
/// </summary>
public class TowerCardBar : MonoBehaviour
{
    private const float CardWidth = 150f;
    private const float CardHeight = 196f;
    private const float Spacing = 14f;

    private readonly List<TowerCard> cards = new List<TowerCard>();

    public void Build(GridManager grid)
    {
        RectTransform root = (RectTransform)transform;

        List<TowerData> hotbar = new List<TowerData>();
        foreach (TowerData data in grid.Hotbar)
        {
            if (data != null) hotbar.Add(data);
        }

        float totalWidth = hotbar.Count * CardWidth + (hotbar.Count - 1) * Spacing;
        UIKit.Place(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f),
            new Vector2(totalWidth + 28f, CardHeight + 28f));

        Image backdrop = root.gameObject.AddComponent<Image>();
        backdrop.color = new Color(UIKit.PanelColor.r, UIKit.PanelColor.g, UIKit.PanelColor.b, 0.6f);

        for (int i = 0; i < hotbar.Count; i++)
        {
            RectTransform cardRect = UIKit.Rect($"Card_{i + 1}_{hotbar[i].towerName}", root);
            float x = -totalWidth / 2f + CardWidth / 2f + i * (CardWidth + Spacing);
            UIKit.Place(cardRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f),
                new Vector2(CardWidth, CardHeight));

            TowerCard card = cardRect.gameObject.AddComponent<TowerCard>();
            card.Build(grid, hotbar[i], i + 1);
            cards.Add(card);
        }
    }
}

public class TowerCard : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private GridManager grid;
    private TowerData data;
    private CanvasGroup group;
    private Image border;
    private TextMeshProUGUI costText;
    private bool dragging;

    private static readonly string[] RoleNames = { "Penghalang", "Perangkap", "Insight", "Pemadam", "Penyerang" };

    public void Build(GridManager gridManager, TowerData towerData, int hotkey)
    {
        grid = gridManager;
        data = towerData;
        Color towerColor = UIKit.ColorOfTower(data.color);

        // Root transparan sebagai penerima klik/drag
        Image hit = gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        group = gameObject.AddComponent<CanvasGroup>();

        border = UIKit.Panel("Selected Border", transform, UIKit.Accent, false);
        UIKit.Stretch(border.rectTransform, -5f);
        border.enabled = false;

        Image bg = UIKit.Panel("Background", transform, UIKit.PanelLight, false);
        UIKit.Stretch(bg.rectTransform);

        Image strip = UIKit.Panel("Color Strip", transform, towerColor, false);
        UIKit.Place(strip.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(150f, 8f));

        // Ikon: sprite dari artist kalau ada, kalau belum pakai kotak warna tower
        Image icon = UIKit.Panel("Icon", transform, Color.white, false);
        UIKit.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(72f, 108f));
        if (data.towerSprite != null)
        {
            icon.sprite = data.towerSprite;
            icon.preserveAspect = true;
        }
        else
        {
            icon.color = towerColor;
            icon.rectTransform.sizeDelta = new Vector2(56f, 80f);
        }

        // Badge hotkey (angka keyboard)
        Image badge = UIKit.Panel("Hotkey", transform, new Color(0f, 0f, 0f, 0.55f), false);
        UIKit.Place(badge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, -14f), new Vector2(28f, 28f));
        TextMeshProUGUI key = UIKit.Label("Key", badge.transform, hotkey.ToString(), 18f, TextAlignmentOptions.Center, UIKit.TextColor, true);
        UIKit.Stretch(key.rectTransform);

        TextMeshProUGUI nameText = UIKit.Label("Name", transform, data.towerName, 17f, TextAlignmentOptions.Center, UIKit.TextColor, true);
        UIKit.Place(nameText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(144f, 22f));
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 12f;
        nameText.fontSizeMax = 17f;

        string role = (int)data.role < RoleNames.Length ? RoleNames[(int)data.role] : data.role.ToString();
        TextMeshProUGUI roleText = UIKit.Label("Role", transform, role, 14f, TextAlignmentOptions.Center, UIKit.TextMuted);
        UIKit.Place(roleText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(144f, 18f));

        // Cost: ikon belah ketupat insight + angka
        Image gem = UIKit.Panel("Insight Icon", transform, UIKit.Accent, false);
        UIKit.Place(gem.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-18f, 16f), new Vector2(13f, 13f));
        gem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        costText = UIKit.Label("Cost", transform, data.cost.ToString(), 20f, TextAlignmentOptions.Left, UIKit.TextColor, true);
        UIKit.Place(costText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0.5f), new Vector2(-6f, 16f), new Vector2(60f, 24f));
    }

    private void Update()
    {
        if (grid == null || data == null) return;

        bool affordable = grid.CanAfford(data);
        group.alpha = affordable ? 1f : 0.5f;
        costText.color = affordable ? UIKit.TextColor : UIKit.Danger;
        border.enabled = grid.IsPlacing && grid.SelectedTowerData == data;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (dragging || Time.timeScale <= 0f || eventData.button != PointerEventData.InputButton.Left) return;

        // Klik kartu yang sedang dipilih = batal
        if (grid.IsPlacing && grid.SelectedTowerData == data)
        {
            grid.CancelPlacement();
        }
        else
        {
            grid.SetSelectedTowerData(data);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (Time.timeScale <= 0f || eventData.button != PointerEventData.InputButton.Left) return;

        dragging = true;
        grid.SetSelectedTowerData(data); // preview bayangan + area langsung muncul saat kartu diseret
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Wajib ada supaya OnBeginDrag / OnEndDrag terpanggil. Preview di map diurus PlacementPreview.
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging) return;
        dragging = false;

        // Dilepas di atas UI = batal. Dilepas di atas map = coba pasang / merge.
        bool overUI = eventData.pointerCurrentRaycast.gameObject != null;
        if (!overUI)
        {
            grid.TryPlaceSelectedAt(grid.GetCellUnderCursor());
        }

        grid.CancelPlacement();
    }
}
