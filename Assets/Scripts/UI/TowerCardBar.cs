using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Deretan kartu tower di bawah layar (isi = Tower Hotbar di GridManager), memakai art kartu dari artist.
/// - Klik kartu = pilih tower lalu klik tile. Drag kartu ke map = langsung pasang (GDD: drag tower).
/// - Kartu yang di-hover / dipilih naik ke atas, dan panel deskripsi tower muncul di kiri
///   (mockup: Art/UI/_Reference/Selected undeployed tower preview).
/// - Insight tidak cukup = kartu digelapkan.
/// </summary>
public class TowerCardBar : MonoBehaviour
{
    private const float Spacing = 14f;

    private readonly List<TowerCard> cards = new List<TowerCard>();
    private Image descriptionPanel;
    private GridManager grid;
    private float cardHeight;

    public void Build(GridManager gridManager, RectTransform hudRoot)
    {
        grid = gridManager;
        UISkin skin = UISkin.Current;
        RectTransform root = (RectTransform)transform;

        List<TowerData> hotbar = new List<TowerData>();
        foreach (TowerData data in grid.Hotbar)
        {
            if (data != null) hotbar.Add(data);
        }

        Vector2 cardSize = new Vector2(200f, 250f) * UIKit.ArtScale;
        cardHeight = cardSize.y;
        float totalWidth = hotbar.Count * cardSize.x + (hotbar.Count - 1) * Spacing;
        UIKit.Place(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(totalWidth, cardSize.y));

        for (int i = 0; i < hotbar.Count; i++)
        {
            RectTransform cardRect = UIKit.Rect($"Card_{i + 1}_{hotbar[i].towerName}", root);
            float x = -totalWidth / 2f + cardSize.x / 2f + i * (cardSize.x + Spacing);
            UIKit.Place(cardRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x, 0f), cardSize);

            TowerCard card = cardRect.gameObject.AddComponent<TowerCard>();
            card.Build(grid, hotbar[i], i + 1, skin);
            cards.Add(card);
        }

        // Panel deskripsi tower (belum dipasang) di kiri, di atas deretan kartu
        descriptionPanel = UIKit.ArtImage("Tower Description", hudRoot, null);
        UIKit.Place(descriptionPanel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(36f, 16f + cardSize.y + 60f), Vector2.zero);
        descriptionPanel.gameObject.SetActive(false);
    }

    private void Update()
    {
        // Kartu yang sedang di-hover diutamakan, kalau tidak ada pakai kartu yang sedang dipilih
        TowerCard focus = null;
        foreach (TowerCard card in cards)
        {
            if (card.IsHovered) { focus = card; break; }
        }
        if (focus == null)
        {
            foreach (TowerCard card in cards)
            {
                if (card.IsSelected) { focus = card; break; }
            }
        }

        Sprite description = focus != null && UISkin.Current != null ? UISkin.Current.Description(focus.Data.color) : null;
        bool show = description != null && Time.timeScale > 0f;
        if (descriptionPanel.gameObject.activeSelf != show) descriptionPanel.gameObject.SetActive(show);
        if (show && descriptionPanel.sprite != description)
        {
            descriptionPanel.sprite = description;
            descriptionPanel.rectTransform.sizeDelta = description.rect.size * UIKit.InfoScale;
        }
    }
}

public class TowerCard : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    private const float RaiseHeight = 50f;

    private GridManager grid;
    private TowerData data;
    private Image art;
    private RectTransform visual;
    private bool dragging;
    private float raise;

    public TowerData Data => data;
    public bool IsHovered { get; private set; }
    public bool IsSelected => grid != null && grid.IsPlacing && grid.SelectedTowerData == data;

    public void Build(GridManager gridManager, TowerData towerData, int hotkey, UISkin skin)
    {
        grid = gridManager;
        data = towerData;

        // Root transparan sebagai penerima klik/drag; gambar kartu di anak supaya bisa "naik" tanpa menggeser area klik
        Image hit = gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);

        Sprite cardSprite = skin != null ? skin.Card(data.color) : null;
        if (cardSprite != null)
        {
            art = UIKit.ArtImage("Card Art", transform, cardSprite);
        }
        else
        {
            // Placeholder kalau art kartu belum ada
            art = UIKit.Panel("Card Art", transform, UIKit.ColorOfTower(data.color), false);
            TextMeshProUGUI label = UIKit.ArtLabel("Name", art.transform, $"{data.towerName}\n{data.cost}", 24f);
            UIKit.Stretch(label.rectTransform);
        }
        visual = art.rectTransform;
        UIKit.Stretch(visual);

        // Angka hotkey kecil di pojok (kontrol keyboard 1-6)
        TextMeshProUGUI key = UIKit.ArtLabel("Hotkey", visual, hotkey.ToString(), 18f, TextAlignmentOptions.TopLeft);
        UIKit.Place(key.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(9f, -4f), new Vector2(32f, 24f));
    }

    private void Update()
    {
        if (grid == null || data == null) return;

        // Naik saat di-hover / dipilih (animasi halus, tetap jalan walau game dijeda)
        float target = (IsHovered || IsSelected) && Time.timeScale > 0f ? RaiseHeight : 0f;
        raise = Mathf.MoveTowards(raise, target, Time.unscaledDeltaTime * 600f);
        visual.offsetMin = new Vector2(0f, raise);
        visual.offsetMax = new Vector2(0f, raise);

        // Insight kurang -> gelap
        art.color = grid.CanAfford(data) ? Color.white : new Color(0.45f, 0.42f, 0.42f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        IsHovered = true;
        AudioManager.CardSelect();
    }
    public void OnPointerExit(PointerEventData eventData) => IsHovered = false;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (dragging || Time.timeScale <= 0f || eventData.button != PointerEventData.InputButton.Left) return;

        // Klik kartu yang sedang dipilih = batal
        if (IsSelected) grid.CancelPlacement();
        else grid.SetSelectedTowerData(data);
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
