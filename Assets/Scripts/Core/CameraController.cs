using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Kamera "spectator" untuk gameplay (sudut 2.5D tetap, tidak bisa diputar):
///  - WASD        : geser kamera
///  - Scroll mouse: zoom in / out (maju-mundur searah pandangan)
///  - Shift       : geser lebih cepat
///  - R           : kembali ke tampilan awal
///
/// Aturan supaya rapi:
///  - Zoom out paling jauh = tampilan awal dari scene (seluruh arena), jadi area kosong tidak bertambah.
///  - Zoom in dibatasi supaya pixel art 64 PPU paling besar ~2.5x (tidak pecah / terlalu kotak-kotak).
///  - Geser dibatasi berdasarkan area tanah yang BENAR-BENAR terlihat kamera (bukan angka kasar),
///    jadi tepi bawah & samping layar tidak bisa keluar dari arena.
/// Otomatis ditambahkan ke Main Camera oleh GridManager.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("Geser (WASD)")]
    [SerializeField] private float panSpeed = 9f;
    [SerializeField] private float fastMultiplier = 2f;

    [Header("Zoom (scroll)")]
    [Tooltip("Pembesaran maksimal pixel art (pixel layar per pixel sprite) di tengah layar")]
    [SerializeField] private float maxPixelZoom = 2.5f;
    [SerializeField] private float zoomStep = 1.5f;

    [Header("Batas arena")]
    [Tooltip("Seberapa jauh tepi area pandang boleh melewati pinggir arena (unit)")]
    [SerializeField] private float sideMargin = 0.5f;
    [SerializeField] private float bottomMargin = 0.4f;
    [SerializeField] private float topMargin = 0.4f;

    [Header("Area layar yang tidak tertutup HUD (0 = bawah, 1 = atas)")]
    [Tooltip("Batas atas kartu tower di bawah layar")]
    [SerializeField, Range(0f, 0.5f)] private float usableBottom = 0.215f;
    [Tooltip("Batas bawah top bar (stage, wall, insight)")]
    [SerializeField, Range(0.5f, 1f)] private float usableTop = 0.875f;

    [Header("Kehalusan gerak")]
    [SerializeField] private float smoothTime = 0.12f;

    [Header("Kontrol")]
    [SerializeField] private Key resetKey = Key.R;

    [Header("Latar")]
    [Tooltip("Warna latar kamera (menyatu dengan hutan di luar arena)")]
    [SerializeField] private Color backgroundColor = new Color(0.10f, 0.17f, 0.11f);

    private const float SpritePixelsPerUnit = 64f;

    private Camera cam;
    private Vector3 homePosition;
    private Vector3 forward;
    private float maxZoomIn;

    private Vector3 targetPosition;
    private float targetZoom;
    private Vector3 velocity;

    private void Start()
    {
        cam = GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = backgroundColor;

        homePosition = transform.position;
        forward = transform.forward;
        targetPosition = homePosition;
        maxZoomIn = ComputeMaxZoomIn();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return; // pause / menang / kalah

        float dt = Time.unscaledDeltaTime;
        ReadInput(dt);

        targetZoom = Mathf.Clamp(targetZoom, 0f, maxZoomIn);
        targetPosition = ClampToArena(targetPosition, targetZoom);

        Vector3 desired = PoseFor(targetPosition, targetZoom);
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime, Mathf.Infinity, dt);
    }

    // targetPosition = posisi kamera "kalau tidak di-zoom" (bidang geser), zoom ditambahkan searah pandangan
    private Vector3 PoseFor(Vector3 panPosition, float zoom)
    {
        return panPosition + forward * zoom;
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
                float zoomFactor = Mathf.Lerp(1f, 0.45f, maxZoomIn > 0f ? targetZoom / maxZoomIn : 0f);
                Vector2 delta = move.normalized * speed * zoomFactor * dt;
                targetPosition += new Vector3(delta.x, 0f, delta.y);
            }

            if (kb[resetKey].wasPressedThisFrame)
            {
                ResetView();
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

    /// <summary>
    /// Geser posisi supaya tanah yang terlihat di tepi kiri/kanan/bawah layar tetap di dalam arena.
    /// Kalau arena lebih sempit dari layar di satu sumbu, kamera ditengahkan di sumbu itu.
    /// </summary>
    private Vector3 ClampToArena(Vector3 panPosition, float zoom)
    {
        GridManager grid = GridManager.Instance;
        if (grid == null || !grid.IsGridReady) return homePosition;

        Rect arena = grid.WorldBounds;
        Vector3 pose = PoseFor(panPosition, zoom);

        // Titik tanah (y=0) di tepi AREA PANDANG (di antara HUD atas & kartu bawah), relatif terhadap kamera.
        // Jadi baris paling belakang bisa dibawa sampai tepat di bawah top bar, baris depan tepat di atas kartu.
        if (!GroundOffset(new Vector2(0f, usableBottom), pose.y, out Vector2 bottomLeft) ||
            !GroundOffset(new Vector2(1f, usableBottom), pose.y, out Vector2 bottomRight))
        {
            return panPosition;
        }
        bool hasTop = GroundOffset(new Vector2(0.5f, usableTop), pose.y, out Vector2 topCenter);

        // Sumbu X: tepi bawah kiri & kanan harus di dalam arena (bagian atas layar memang lebih lebar karena perspektif)
        float minX = arena.xMin - sideMargin - bottomLeft.x;
        float maxX = arena.xMax + sideMargin - bottomRight.x;
        float x = minX <= maxX ? Mathf.Clamp(pose.x, minX, maxX) : (arena.center.x - (bottomLeft.x + bottomRight.x) * 0.5f);

        // Sumbu Z: tepi bawah tidak boleh lewat arena (prioritas), tepi atas sebisa mungkin
        float minZ = arena.yMin - bottomMargin - bottomLeft.y;
        float maxZ = hasTop ? arena.yMax + topMargin - topCenter.y : float.PositiveInfinity;
        float z = pose.z;
        if (minZ <= maxZ) z = Mathf.Clamp(z, minZ, maxZ);
        else z = Mathf.Max(minZ, Mathf.Min(z, homePosition.z + forward.z * zoom)); // tampilan awal tetap boleh

        Vector3 clampedPose = new Vector3(x, pose.y, z);
        return clampedPose - forward * zoom;
    }

    /// <summary>Selisih X/Z dari kamera ke titik tanah yang terlihat di titik viewport tertentu.</summary>
    private bool GroundOffset(Vector2 viewport, float cameraHeight, out Vector2 offset)
    {
        offset = Vector2.zero;
        Vector3 dir = cam.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f)).direction;
        if (dir.y >= -0.0001f) return false; // sinar tidak mengenai tanah (melihat cakrawala)

        float t = cameraHeight / -dir.y;
        offset = new Vector2(dir.x * t, dir.z * t);
        return true;
    }

    /// <summary>Hitung zoom in maksimal supaya sprite 64 PPU paling besar maxPixelZoom kali di tengah layar.</summary>
    private float ComputeMaxZoomIn()
    {
        if (cam == null || cam.orthographic) return 6f;

        // Jarak minimal: tinggi area terlihat = layar / (PPU * zoom pixel)
        float minVisibleHeight = Screen.height / (SpritePixelsPerUnit * maxPixelZoom);
        float minDistance = minVisibleHeight / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));

        // Jarak awal kamera ke tanah searah pandangan
        float homeDistance = forward.y < -0.0001f ? homePosition.y / -forward.y : 15f;
        return Mathf.Max(0f, homeDistance - minDistance);
    }

    /// <summary>Kembalikan kamera ke tampilan awal (dipakai tombol UI kalau perlu).</summary>
    public void ResetView()
    {
        targetPosition = homePosition;
        targetZoom = 0f;
    }
}
