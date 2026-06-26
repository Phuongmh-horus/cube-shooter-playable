using GogoGaga.OptimizedRopesAndCables;
using UnityEngine;

public class LineConnectorMono : MonoBehaviour
{
    [SerializeField] protected Renderer[] _cachedRenderers;
    [SerializeField] private Rope _rope;
    [SerializeField] private RopeMesh _ropeMesh;

    private bool _isDespawned = false; // Flag để tránh despawn nhiều lần
    private Color _cachedStartColor = Color.white;  // Cache màu Start hiện tại
    private Color _cachedEndColor = Color.white;    // Cache màu End hiện tại

    public System.Action<LineConnectorMono> OnDespawnEvent;

    public Rope Rope
    {
        get
        {
            if (_rope == null)
                _rope = GetComponent<Rope>();
            return _rope;
        }
    }

    public RopeMesh RopeMesh
    {
        get
        {
            if (_ropeMesh == null)
                _ropeMesh = GetComponent<RopeMesh>();
            return _ropeMesh;
        }
    }

    /// <summary>
    /// Giống doc: SetupRope(active=true, target=nextCollectorVisual)
    /// 1. Bật rope object
    /// 2. Set đầu dây = start, cuối dây = end
    /// 3. Set màu gradient 2 đầu
    /// </summary>
    public void OnInit(Transform start, Transform end, Color startColor, Color endColor)
    {
        _isDespawned = false; // Reset flag khi init
        _cachedStartColor = startColor;  // Cache mau Start ban dau
        _cachedEndColor = endColor;      // Cache mau End ban dau
        if (_rope != null) _rope.enabled = true;
        if (_ropeMesh != null) _ropeMesh.enabled = true;
        Rope.OnInit();
        Rope.SetStartPoint(start, instantAssign: true);
        Rope.SetEndPoint(end, instantAssign: true);
        RopeMesh.SetColor(startColor, endColor);
        SetRenderersActive(false);
        gameObject.SetActive(true);
        StartCoroutine(WaitAndShowRope());
    }

    /// <summary>
    /// Init không có màu (dùng màu mặc định)
    /// </summary>
    public void OnInit(Transform start, Transform end)
    {
        _isDespawned = false; // Reset flag khi init
        if (_rope != null) _rope.enabled = true;
        if (_ropeMesh != null) _ropeMesh.enabled = true;
        Rope.OnInit();
        Rope.SetStartPoint(start, instantAssign: true);
        Rope.SetEndPoint(end, instantAssign: true);
        SetRenderersActive(false);
        gameObject.SetActive(true);
        StartCoroutine(WaitAndShowRope());
    }

    private System.Collections.IEnumerator WaitAndShowRope()
    {
        yield return null;
        if (!_isDespawned)
            SetRenderersActive(true);
    }

    /// <summary>
    /// Cập nhật màu dây (gọi khi Reveal hoặc đổi trạng thái)
    /// Giống doc: UpdateRopeColor()
    /// </summary>
    public void UpdateColor(Color startColor, Color endColor)
    {
        _cachedStartColor = startColor;  // Cache màu Start
        _cachedEndColor = endColor;      // Cache màu End
        RopeMesh.SetColor(startColor, endColor);
    }

    /// <summary>
    /// Set chỉ màu nửa start, giữ nguyên nửa end
    /// </summary>
    public void SetStartColor(Color color)
    {
        _cachedStartColor = color;  // Update cache
        RopeMesh.SetColor(color, _cachedEndColor);  // Dùng cached end color
    }

    /// <summary>
    /// Set chỉ màu nửa end, giữ nguyên nửa start
    /// </summary>
    public void SetEndColor(Color color)
    {
        _cachedEndColor = color;  // Update cache
        RopeMesh.SetColor(_cachedStartColor, color);  // Dùng cached start color
    }

    public void OnDespawn()
    {
        // Kiểm tra flag để tránh despawn nhiều lần
        if (_isDespawned) return;
        _isDespawned = true;

        OnDespawnEvent?.Invoke(this);
        OnDespawnEvent = null;

        gameObject.SetActive(false); // tắt trước để Rope/RopeMesh không Update/GenerateMesh khi point = null
        Rope.SetStartPoint(null);
        Rope.SetEndPoint(null);
        PoolHolder.Instance.Release(this);
    }
    public void SetRenderersActive(bool active)
    {
        if (_cachedRenderers == null || _cachedRenderers.Length == 0)
        {
            _cachedRenderers = GetComponentsInChildren<Renderer>(true);
        }
        foreach (var r in _cachedRenderers)
        {
            if (r != null)
            {
                r.enabled = active;
            }
        }
    }
}
