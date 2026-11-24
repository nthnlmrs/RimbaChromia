using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Health bar yang mengikuti objek di dunia (digambar di canvas overlay):
/// - Tower: bar DI BAWAH tower, selalu tampil (GDD: "put the health bar below the tower").
///   Hijau -> merah sesuai sisa HP, emas saat kebal (Red setelah merge).
/// - Musuh: bar di atas kepala, hanya tampil setelah terkena damage.
/// </summary>
public class HealthBarLayer : MonoBehaviour
{
    private class Bar
    {
        public RectTransform root;
        public Image fill;
        public int seenFrame;
    }

    private static readonly Vector2 TowerBarSize = new Vector2(64f, 10f);
    private static readonly Vector2 EnemyBarSize = new Vector2(48f, 7f);
    private static readonly Color EnemyColor = new Color(0.9f, 0.25f, 0.25f);

    private readonly Dictionary<Component, Bar> bars = new Dictionary<Component, Bar>();
    private readonly List<Component> removeBuffer = new List<Component>();
    private RectTransform layer;
    private Camera cam;

    private void Awake()
    {
        layer = (RectTransform)transform;
    }

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        int frame = Time.frameCount;

        foreach (Tower tower in Tower.ActiveTowers)
        {
            if (tower == null || tower.OccupiedCell == null || tower.MaxHP <= 0f) continue;

            // Titik sedikit ke arah kamera dari tile = tampil di bawah tower pada layar
            // Sprite tower ditanam sedikit ke depan tile (SpriteBillboard.TowerGroundOffset),
            // jadi bar diletakkan lebih ke depan lagi supaya tepat di bawah kaki, tidak menimpa sprite
            Vector3 cellPos = tower.OccupiedCell.transform.position;
            Vector3 world = new Vector3(cellPos.x, tower.OccupiedCell.SurfaceY, cellPos.z - 0.68f);
            float ratio = tower.CurrentHP / tower.MaxHP;
            Color color = tower.IsImmune ? UIKit.Accent : Color.Lerp(UIKit.Danger, UIKit.Good, ratio);
            UpdateBar(tower, world, ratio, color, TowerBarSize, true, frame);
        }

        foreach (Enemy enemy in Enemy.ActiveEnemies)
        {
            if (enemy == null || enemy.MaxHP <= 0f) continue;

            Vector3 world = enemy.transform.position + Vector3.up * (enemy.VisualHeight + 0.15f);
            float ratio = enemy.CurrentHP / enemy.MaxHP;
            UpdateBar(enemy, world, ratio, EnemyColor, EnemyBarSize, ratio < 0.999f, frame);
        }

        // Hapus bar yang objeknya sudah hilang
        removeBuffer.Clear();
        foreach (KeyValuePair<Component, Bar> pair in bars)
        {
            if (pair.Key == null || pair.Value.seenFrame != frame) removeBuffer.Add(pair.Key);
        }
        foreach (Component key in removeBuffer)
        {
            if (bars[key].root != null) Destroy(bars[key].root.gameObject);
            bars.Remove(key);
        }
    }

    private void UpdateBar(Component target, Vector3 world, float ratio, Color color, Vector2 size, bool visible, int frame)
    {
        if (!bars.TryGetValue(target, out Bar bar))
        {
            bar = CreateBar(size);
            bars[target] = bar;
        }
        bar.seenFrame = frame;

        Vector3 screen = cam.WorldToScreenPoint(world);
        bool onScreen = screen.z > 0f;
        bar.root.gameObject.SetActive(visible && onScreen);
        if (!visible || !onScreen) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, null, out Vector2 local);
        bar.root.anchoredPosition = local;
        bar.fill.fillAmount = Mathf.Clamp01(ratio);
        bar.fill.color = color;
    }

    private Bar CreateBar(Vector2 size)
    {
        Image fill = UIKit.Bar("HealthBar", layer, new Color(0f, 0f, 0f, 0.7f), Color.white, out RectTransform root);
        UIKit.Place(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        fill.rectTransform.offsetMin = new Vector2(1.5f, 1.5f);
        fill.rectTransform.offsetMax = new Vector2(-1.5f, -1.5f);
        return new Bar { root = root, fill = fill };
    }
}
