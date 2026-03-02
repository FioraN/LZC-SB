using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PrototypeFPC;

/// <summary>
/// Pure-UI radar / minimap.
///
/// Reads enemy world positions each frame, maps them onto a circular UI panel
/// as red dots. No extra camera, no RenderTexture, no special layer required.
///
/// Setup:
///   1. Create a circular Image in your UI Canvas as the radar background.
///   2. Create an Image for the player direction arrow, place it centred on the radar.
///   3. Attach this script, assign fpcDependencies, radarPanel, playerArrow.
///   4. (Optional) assign a dotSprite — if left empty a circle is generated at runtime.
/// </summary>
public class RadarUI : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────

    [Header("Refs")]
    [Tooltip("FPC Dependencies — player position and facing direction.")]
    public Dependencies fpcDependencies;

    [Tooltip("The RectTransform that represents the radar area. Enemy dots are spawned as children of this.")]
    public RectTransform radarPanel;

    [Tooltip("Your existing UI Image for the player direction arrow. Script only rotates it.")]
    public RectTransform playerArrow;

    [Header("Range")]
    [Tooltip("World-unit radius the radar covers. Enemies beyond this distance are hidden.")]
    public float worldRadius = 50f;

    [Header("Dots")]
    [Tooltip("Pixel diameter of each enemy dot on the radar.")]
    public float dotSize = 12f;

    [Tooltip("Colour of enemy dots.")]
    public Color enemyColor = Color.red;

    [Tooltip("Optional: a circular sprite for the dots. If empty, a circle is generated at runtime.")]
    public Sprite dotSprite;

    [Header("Scan")]
    [Tooltip("Frames between full enemy-list refreshes (performance).")]
    public int scanInterval = 15;

    [Tooltip("Max enemy dots (object-pool size).")]
    public int maxDots = 40;

    [Header("Mode")]
    [Tooltip("true = map rotates with player (arrow always up).\nfalse = map fixed north, arrow rotates.")]
    public bool rotateMap = true;

    [Header("Debug")]
    [Tooltip("Show a green dot at the calculated center of the radar (for diagnosing offset issues).")]
    public bool showCenterDebug = true;

    // ─── Runtime ──────────────────────────────────────────────────────────

    private readonly List<MonsterHealth> _enemies = new List<MonsterHealth>();
    private readonly List<RectTransform> _pool   = new List<RectTransform>();
    private Sprite  _generatedSprite;
    private int     _frameCounter;
    private RectTransform _debugCenterDot;

    // ─────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (dotSprite == null)
            dotSprite = _generatedSprite = CreateCircleSprite(32);

        InitPool();
        CreateDebugCenterDot();
        RefreshEnemyList();
    }

    private void OnDestroy()
    {
        if (_generatedSprite != null)
        {
            if (_generatedSprite.texture != null)
                Destroy(_generatedSprite.texture);
            Destroy(_generatedSprite);
        }
    }

    private void LateUpdate()
    {
        if (fpcDependencies == null || radarPanel == null) return;

        // ── Periodic enemy scan ───────────────────────────────────────────
        _frameCounter++;
        if (_frameCounter >= scanInterval)
        {
            _frameCounter = 0;
            RefreshEnemyList();
        }

        Transform player = fpcDependencies.rb != null
            ? fpcDependencies.rb.transform
            : fpcDependencies.transform;

        Vector3 playerPos = player.position;
        float   playerYaw = fpcDependencies.orientation != null
            ? fpcDependencies.orientation.eulerAngles.y
            : player.eulerAngles.y;

        // Radar pixel half-size (use the smaller dimension for a circle)
        float radarPixelRadius = Mathf.Min(radarPanel.rect.width, radarPanel.rect.height) * 0.5f;
        float scale = radarPixelRadius / worldRadius;

        // ── Debug diagnostics (every ~2 seconds) ────────────────────────
        if (_frameCounter == 1)
        {
            Debug.Log($"[RadarUI] Panel pivot=({radarPanel.pivot.x:F2},{radarPanel.pivot.y:F2}) " +
                      $"rect=({radarPanel.rect.x:F0},{radarPanel.rect.y:F0},{radarPanel.rect.width:F0},{radarPanel.rect.height:F0}) " +
                      $"radarPixelRadius={radarPixelRadius:F1} scale={scale:F3}");
            Debug.Log($"[RadarUI] playerPos={playerPos} playerYaw={playerYaw:F1} " +
                      $"rb?={fpcDependencies.rb != null} orientation?={fpcDependencies.orientation != null}");

            if (_enemies.Count > 0 && _enemies[0] != null)
            {
                var e = _enemies[0];
                Collider dc = e.GetComponent<Collider>();
                Vector3 dp = (dc != null) ? dc.bounds.center : e.transform.position;
                float ddx = dp.x - playerPos.x;
                float ddz = dp.z - playerPos.z;
                Debug.Log($"[RadarUI] Enemy[0] transform={e.transform.position} hitbox={dp} dx={ddx:F1} dz={ddz:F1}");
            }

            if (_debugCenterDot != null)
            {
                Debug.Log($"[RadarUI] CenterDot anchoredPos={_debugCenterDot.anchoredPosition} " +
                          $"localPos={_debugCenterDot.localPosition}");
            }
        }

        // Pre-compute rotation for rotateMap mode
        float rad = playerYaw * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        // ── Player arrow (keep centred on radar) ────────────────────────
        if (playerArrow != null)
        {
            // Ensure the arrow sits exactly at the radar centre
            if (playerArrow.parent == radarPanel)
            {
                playerArrow.anchorMin        = new Vector2(0.5f, 0.5f);
                playerArrow.anchorMax        = new Vector2(0.5f, 0.5f);
                playerArrow.anchoredPosition = Vector2.zero;
            }
            else
            {
                // Arrow lives outside radarPanel — match world position of radar centre
                playerArrow.position = radarPanel.TransformPoint(radarPanel.rect.center);
            }

            if (rotateMap)
                playerArrow.localRotation = Quaternion.identity;
            else
                playerArrow.localRotation = Quaternion.Euler(0f, 0f, -playerYaw);
        }

        // ── Enemy dots ───────────────────────────────────────────────────
        int dotIdx = 0;

        for (int i = 0; i < _enemies.Count; i++)
        {
            var e = _enemies[i];
            if (e == null || e.IsDead) continue;

            // Use collider bounds centre for accurate hitbox position
            Collider col = e.GetComponent<Collider>();
            Vector3 ePos = (col != null) ? col.bounds.center : e.transform.position;
            float dx = ePos.x - playerPos.x;
            float dz = ePos.z - playerPos.z;

            // Skip if outside radar range
            if (dx * dx + dz * dz > worldRadius * worldRadius)
                continue;

            if (dotIdx >= _pool.Count) break;

            // Map world offset → UI offset
            float uiX = dx * scale;
            float uiY = dz * scale;  // world Z = forward = UI up

            if (rotateMap)
            {
                float rx = uiX * cos - uiY * sin;
                float ry = uiX * sin + uiY * cos;
                uiX = rx;
                uiY = ry;
            }

            // Clamp inside circle
            float dist = Mathf.Sqrt(uiX * uiX + uiY * uiY);
            if (dist > radarPixelRadius)
            {
                float f = radarPixelRadius / dist;
                uiX *= f;
                uiY *= f;
            }

            var dot = _pool[dotIdx];
            dot.gameObject.SetActive(true);
            dot.anchoredPosition = new Vector2(uiX, uiY);
            dot.sizeDelta        = new Vector2(dotSize, dotSize);
            dotIdx++;
        }

        // Hide unused
        for (int i = dotIdx; i < _pool.Count; i++)
            _pool[i].gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Debug
    // ─────────────────────────────────────────────────────────────────────

    private void CreateDebugCenterDot()
    {
        if (!showCenterDebug || radarPanel == null) return;

        var go = new GameObject("DebugCenter", typeof(RectTransform));
        _debugCenterDot = go.GetComponent<RectTransform>();
        _debugCenterDot.SetParent(radarPanel, false);
        _debugCenterDot.anchorMin        = new Vector2(0.5f, 0.5f);
        _debugCenterDot.anchorMax        = new Vector2(0.5f, 0.5f);
        _debugCenterDot.pivot            = new Vector2(0.5f, 0.5f);
        _debugCenterDot.anchoredPosition = Vector2.zero;
        _debugCenterDot.sizeDelta        = new Vector2(dotSize * 1.5f, dotSize * 1.5f);

        var le = go.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        var img = go.AddComponent<Image>();
        img.sprite = dotSprite;
        img.color  = Color.green;
        img.raycastTarget = false;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Pool
    // ─────────────────────────────────────────────────────────────────────

    private void InitPool()
    {
        if (radarPanel == null) return;

        for (int i = 0; i < maxDots; i++)
        {
            var go = new GameObject($"Dot_{i}", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(radarPanel, false);
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(dotSize, dotSize);

            // Prevent LayoutGroup on radarPanel from overriding dot positions
            var le = go.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            var img = go.AddComponent<Image>();
            img.sprite = dotSprite;
            img.color  = enemyColor;
            img.raycastTarget = false;

            go.SetActive(false);
            _pool.Add(rt);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Enemy scanning
    // ─────────────────────────────────────────────────────────────────────

    private void RefreshEnemyList()
    {
        _enemies.Clear();
        var all = FindObjectsOfType<MonsterHealth>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && !all[i].IsDead)
                _enemies.Add(all[i]);
        }
        Debug.Log($"[RadarUI] Scan: found {all.Length} MonsterHealth, {_enemies.Count} alive.");
    }

    // ─────────────────────────────────────────────────────────────────────
    // Procedural circle sprite
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Generates a small white filled-circle texture and wraps it as a Sprite.</summary>
    private static Sprite CreateCircleSprite(int resolution)
    {
        var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        float center = resolution * 0.5f;
        float radius = center - 1f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float d  = Mathf.Sqrt(dx * dx + dy * dy);
                float a  = Mathf.Clamp01(radius - d + 0.5f); // soft edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        return Sprite.Create(tex,
            new Rect(0f, 0f, resolution, resolution),
            new Vector2(0.5f, 0.5f),
            100f);
    }
}
