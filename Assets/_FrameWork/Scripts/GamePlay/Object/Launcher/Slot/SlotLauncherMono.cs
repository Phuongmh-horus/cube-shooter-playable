using System.Collections;
using UnityEngine;

public class SlotLauncherMono : MonoBehaviour
{
    [SerializeField] private Transform _pointTargetLauncher;
    [SerializeField] protected Renderer[] _cachedRenderers;
    private LauncherBaseMono _launcherBaseMono;

    [Header("Material Settings")]
    [SerializeField] private Material _sharedMaterial; // SimpleUnlit material dùng chung
    [SerializeField] private Color _blinkColor1 = Color.white;
    [SerializeField] private Color _blinkColor2 = Color.red;
    [SerializeField] private float _blinkSpeed = 5f;

    [Header("Model Sides")]
    [SerializeField] private GameObject _modelLeft;
    [SerializeField] private GameObject _modelRight;
    [SerializeField] private GameObject _modelCenter;

    private int _index;
    private ModelSide _modelSide;
    public LauncherBaseMono CurrentLauncher => _launcherBaseMono;
    public bool IsSlotBooster = false; // check xem nó là slot booster hay deffault
    
    private bool _isResigned = false;
    
    public bool IsEmpty => CurrentLauncher == null && _isResigned == false;
    public int Index => _index;

    private void Awake()
    {
        // Lưu màu gốc của material khi first time
        if (_sharedMaterial != null)
            _originalMaterialColor = _sharedMaterial.GetColor("_BaseColor");
    }

    public void OnInit(int index, ModelSide side = ModelSide.Center)
    {
        _index = index;
        _modelSide = side;
        _launcherBaseMono = null;
        _isResigned = false;
        SetModelSide(side);
        
        // Reset material color to _blinkColor1 on initialization
        if (_sharedMaterial != null)
        {
            _originalMaterialColor = _blinkColor1;
            _sharedMaterial.SetColor("_BaseColor", _blinkColor1);
        }
    }

    private void SetModelSide(ModelSide side)
    {
        if (_modelLeft != null) _modelLeft.SetActive(side == ModelSide.Left);
        if (_modelRight != null) _modelRight.SetActive(side == ModelSide.Right);
        if (_modelCenter != null) _modelCenter.SetActive(side == ModelSide.Center);
    }

    public void OnDespawn()
    {
        _launcherBaseMono?.OnDespawn();
        _launcherBaseMono = null;
        PoolHolder.Instance.Release(this);
    }

    public void AssignLauncher(LauncherBaseMono launcher)
    {
        _launcherBaseMono = launcher;
    }
    
    public void ClearSlot()
    {
        _launcherBaseMono = null;
        _isResigned = false;
    }

    public Vector3 GetSlotPosition()
    {
        return _pointTargetLauncher.position;
    }
    
    public void SetResigneLauncher(bool b)
    {
        _isResigned = b;
    }

    private Coroutine _blinkCts;
    private Color _originalMaterialColor;

    /// <summary>
    /// Starts blinking the shared material using serialized colors and speed.
    /// </summary>
    public void StartBlinking()
    {
        StartBlinking(_blinkColor1, _blinkColor2, _blinkSpeed);
    }

    /// <summary>
    /// Starts blinking the shared material between two colors.
    /// </summary>
    public void StartBlinking(Color color1, Color color2, float speed = 5f)
    {
        StopBlinking();
        if (_sharedMaterial == null) return;
        
        _originalMaterialColor = _sharedMaterial.GetColor("_BaseColor");
        _blinkCts = StartCoroutine(BlinkMaterialAsync(color1, color2, speed));
    }

    /// <summary>
    /// Stops the blinking animation and restores original material color.
    /// </summary>
    public void StopBlinking()
    {
        if (_blinkCts != null)
        {
            StopCoroutine(_blinkCts);
            _blinkCts = null;
        }
        if (_sharedMaterial != null)
            _sharedMaterial.SetColor("_BaseColor", _originalMaterialColor);
    }

    /// <summary>
    /// The UniTask function that performs the continuous material color blinking.
    /// </summary>
    private IEnumerator BlinkMaterialAsync(Color color1, Color color2, float speed)
    {
        if (_sharedMaterial == null) yield break;

        bool toggle = false;
        while (_sharedMaterial != null)
        {
            _sharedMaterial.SetColor("_BaseColor", toggle ? color1 : color2);
            toggle = !toggle;

            float interval = speed > 0 ? 1f / speed : 0.5f;
            yield return new WaitForSeconds(interval);
        }
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
        if (_launcherBaseMono != null)
        {
            _launcherBaseMono.SetRenderersActive(active);
        }
    }
}
