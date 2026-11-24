using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Kamera "spectator" untuk gameplay:
///  - WASD        : geser kamera di atas map (tanpa rotasi, sudut 2.5D tetap)
///  - Scroll mouse: zoom in / out (maju-mundur searah pandangan kamera)
///  - Shift       : geser lebih cepat
///  - R           : kembali ke posisi awal (seluruh map terlihat)
/// Posisi & sudut awal diambil dari Main Camera di scene. Gerakan dibatasi supaya tidak keluar map.
/// Otomatis ditambahkan ke Main Camera oleh GridManager.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("Geser (WASD)")]
    [SerializeField] private float panSpeed = 9f;
    [SerializeField] private float fastMultiplier = 2f;

    [Header("Zoom (scroll)")]
    [Tooltip("Seberapa jauh kamera boleh maju dari posisi awal (unit dunia)")]
    [SerializeField] private float maxZoomIn = 9f;
    [Tooltip("Seberapa jauh kamera boleh mundur dari posisi awal (unit dunia)")]
    [SerializeField] private float maxZoomOut = 2f;
    [SerializeField] private float zoomStep = 1.5f;

    [Header("Kehalusan gerak")]
    [SerializeField] private float smoothTime = 0.12f;

    [Header("Kontrol")]
    [SerializeField] private Key resetKey = Key.R;

    private Vector3 homePosition;
    private Vector3 forward;

    private Vector2 targetPan;   // geser di bidang X/Z relatif posisi awal
    private float targetZoom;    // + = maju (zoom in)
    private Vector2 pan;
    private float zoom;
    private Vector2 panVelocity;
    private float zoomVelocity;

    private void Start()
    {
        homePosition = transform.position;
        forward = transform.forward;
    }

    private void Update()
    {
        // Jangan bergerak saat pause / menang / kalah
        if (Time.timeScale <= 0f)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;
        ReadInput(dt);

        // Batas geser: setengah ukuran map ke tiap arah, ditambah sedikit saat zoom in
        Vector2 limit = GetPanLimit();
        targetPan.x = Mathf.Clamp(targetPan.x, -limit.x, limit.x);
        targetPan.y = Mathf.Clamp(targetPan.y, -limit.y, limit.y);
        targetZoom = Mathf.Clamp(targetZoom, -maxZoomOut, maxZoomIn);

        pan = Vector2.SmoothDamp(pan, targetPan, ref panVelocity, smoothTime, Mathf.Infinity, dt);
        zoom = Mathf.SmoothDamp(zoom, targetZoom, ref zoomVelocity, smoothTime, Mathf.Infinity, dt);

        transform.position = homePosition + new Vector3(pan.x, 0f, pan.y) + forward * zoom;
    }

    private void ReadInput(float dt)
    {
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            Vector2 move = Vector2.zero;
            if (kb.wKey.isPressed) move.y += 1f;
            if (kb.sKey.isPressed) move.y -= 1f;
            if (kb.dKey.isPressed) move.x += 1f;
            if (kb.aKey.isPressed) move.x -= 1f;

            if (move != Vector2.zero)
            {
                float speed = panSpeed * (kb.shiftKey.isPressed ? fastMultiplier : 1f);
                // Makin zoom in makin pelan, supaya mudah melihat detail
                float zoomFactor = Mathf.Lerp(1f, 0.55f, Mathf.InverseLerp(0f, maxZoomIn, targetZoom));
                targetPan += move.normalized * speed * zoomFactor * dt;
            }

            if (kb[resetKey].wasPressedThisFrame)
            {
                targetPan = Vector2.zero;
                targetZoom = 0f;
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            // Scroll di atas UI (kartu tower, panel) tidak menggerakkan kamera
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            float scroll = mouse.scroll.ReadValue().y;
            if (!overUI && Mathf.Abs(scroll) > 0.01f)
            {
                targetZoom += Mathf.Sign(scroll) * zoomStep;
            }
        }
    }

    private Vector2 GetPanLimit()
    {
        GridManager grid = GridManager.Instance;
        float halfWidth = grid != null ? grid.Columns * grid.CellSize * 0.5f : 10f;
        float halfDepth = grid != null ? grid.Rows * grid.CellSize * 0.5f : 8f;

        // Di posisi awal seluruh map sudah terlihat, jadi geser hanya berguna saat zoom in
        float zoomed = Mathf.InverseLerp(-maxZoomOut, maxZoomIn, targetZoom);
        float t = Mathf.Lerp(0.35f, 1f, zoomed);
        return new Vector2(halfWidth * t, halfDepth * t);
    }

    /// <summary>Kembalikan kamera ke tampilan awal (dipakai tombol UI kalau perlu).</summary>
    public void ResetView()
    {
        targetPan = Vector2.zero;
        targetZoom = 0f;
    }
}
