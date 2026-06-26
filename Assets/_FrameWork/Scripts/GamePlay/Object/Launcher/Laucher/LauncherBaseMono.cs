using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[SelectionBase]
public abstract class LauncherBaseMono : MonoBehaviour
{
    #region <========================= PROPERTY & FIELD =========================>
    [Tooltip("Render ở đây đổi material")][SerializeField] protected Renderer[] _cachedRenderers;
    [Tooltip("Render ở đây không đổi material")][SerializeField] protected Renderer[] _cachedRenderersDontChangeMaterial;

    [Header("Animator Base")]
    [SerializeField] protected Animator _peaAnimator;
    [SerializeField] protected Animator _shooterAnimator;
    [SerializeField] protected GameObject _shooterRotateObject;
    private static readonly int DissolveAmountProperty = Shader.PropertyToID("_DissolveAmount");
    private MaterialPropertyBlock _dissolveBlock;
    private Coroutine _dissolveCts;
    private float _currentDissolveAmount = 0f;

    protected ObjectBaseMono _objectBaseMono;
    private Quaternion _initialLocalRotation;
    private bool _hasInitLocalRotation = false;

    // LocalPosition chuẩn khi launcher nằm trong slot — dùng chung cho cả
    // flow jump-from-column và flow dồn-chỗ, tránh lệch Z
    public static readonly Vector3 LocalPositionInSlot = new Vector3(0f, 0f, -0.5f);

    protected MaterialPropertyBlock DissolveBlock
    {
        get
        {
            if (_dissolveBlock == null)
                _dissolveBlock = new MaterialPropertyBlock();
            return _dissolveBlock;
        }
    }

    private bool IsTextRenderer(Renderer r)
    {
        if (r == null) return false;
        return r.GetComponent<TextMeshPro>() != null || r.GetComponent<UnityEngine.UI.Text>() != null;
    }

    public virtual void ResetDissolveState()
    {
        _currentDissolveAmount = 0f;
        if (_dissolveCts != null) StopCoroutine(_dissolveCts);
        _dissolveCts = null;

        if (_cachedRenderers == null || _cachedRenderers.Length == 0)
            _cachedRenderers = GetComponentsInChildren<Renderer>(true);

        ApplyDissolvePropertyBlock(0f);
    }

    public virtual void SetRenderersActive(bool active)
    {
        if (_dissolveCts != null) StopCoroutine(_dissolveCts);
        _dissolveCts = StartCoroutine(SetRenderersActiveAsync(active));
    }

    public virtual IEnumerator SetRenderersActiveAsync(bool active)
    {
        if (_cachedRenderers == null || _cachedRenderers.Length == 0)
            _cachedRenderers = GetComponentsInChildren<Renderer>(true);

        float targetAmount = active ? 0f : 1f;
        float startAmount = _currentDissolveAmount;
        float duration = 0.5f;

        if (active)
        {
            foreach (var r in _cachedRenderers)
                if (r != null) r.enabled = true;
            foreach (Renderer r in _cachedRenderersDontChangeMaterial)
                if (r != null && !IsTextRenderer(r)) r.enabled = true;
        }
        else
        {
            foreach (var r in _cachedRenderers)
                if (r != null && IsTextRenderer(r)) r.enabled = false;
            foreach (Renderer r in _cachedRenderersDontChangeMaterial)
                if (r != null && !IsTextRenderer(r)) r.enabled = false;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t);
            _currentDissolveAmount = Mathf.Lerp(startAmount, targetAmount, t);
            ApplyDissolvePropertyBlock(_currentDissolveAmount);
            yield return null;
        }

        _currentDissolveAmount = targetAmount;
        ApplyDissolvePropertyBlock(_currentDissolveAmount);

        foreach (var r in _cachedRenderers)
        {
            if (r != null)
            {
                if (active)
                {
                    if (IsTextRenderer(r))
                    {
                        if (r.gameObject.activeInHierarchy) r.enabled = true;
                    }
                    else r.enabled = true;
                }
                else r.enabled = false;
            }
        }

        _dissolveCts = null;
    }

    private void ApplyDissolvePropertyBlock(float amount)
    {
        if (_cachedRenderers == null) return;
        var block = DissolveBlock;
        block.SetFloat(DissolveAmountProperty, amount);
        foreach (var r in _cachedRenderers)
            if (r != null) r.SetPropertyBlock(block);
    }

    [Header("Launcher Data")]
    protected LauncherData _data;

    protected Transform _tf;
    protected VerticalLauncherMono _verticalLauncherMonoParent;
    protected int _columnIndex = -1;
    protected Coroutine _moveCts;

    [Header("Frozened")]
    [SerializeField] private GameObject _frozenedGameObject;
    [SerializeField] private TextMeshPro _tmpFrozened;
    [SerializeField] private GameObject _objectHideIfEnableFrozened;

    protected bool _canSelect;
    #endregion

    #region <========================= GET & SET =========================>

    public Transform TF => _tf;
    public int ColumnIndex => _columnIndex;
    public bool CanSelect => _canSelect;
    public int GetCountColorAndBullet() => _data.ColorAndBulletAmound.Count;
    public ColorAndBulletAmound ColorAndBullet => (_data != null && _data.ColorAndBulletAmound != null && _data.ColorAndBulletAmound.Count > 0) ? _data.ColorAndBulletAmound[0] : null;
    public int GetID() => _data.ID;
    public LauncherMode GetPieceLauncherMode() => _data.LauncherMode;
    public CubeShooterColor GetColorCodeIndex0() => _data.ColorAndBulletAmound[0].ColorCode;
    public int GetFrozened() => _data.Frozened;
    public bool GetHidden() => _data.Hidden;
    public List<int> GetConnectedReferencesIDs() => _data?.ConnectedReferencesIDs;

    public void SetParentColumn(VerticalLauncherMono parent)
    {
        _verticalLauncherMonoParent = parent;
        _columnIndex = parent != null ? parent.ColumnIndex : -1;
    }

    public virtual void RemoveLauncherAtVertical()
    {
        if (_verticalLauncherMonoParent == null) return;
        _verticalLauncherMonoParent.RemoveLauncher(this);
        _verticalLauncherMonoParent = null;
        _columnIndex = -1;
    }

    public void RemoveLauncherDirectWithoutArrange()
    {
        if (_verticalLauncherMonoParent == null) return;
        _verticalLauncherMonoParent.RemoveLauncherDirect(this);
        _verticalLauncherMonoParent = null;
        _columnIndex = -1;
    }

    #endregion

    #region <========================= INIT DESPAWN =========================>

    public virtual void OnInit(LauncherData data)
    {
        _data = data;
        _canSelect = true;
        ResetDissolveState();
        OnInitFrozen(_data.Frozened);
        gameObject.SetActive(true);
    }

    public virtual void OnDespawn()
    {
        if (_shooterRotateObject != null && _hasInitLocalRotation)
            _shooterRotateObject.transform.localRotation = _initialLocalRotation;

        _verticalLauncherMonoParent = null;
        _columnIndex = -1;
        _canSelect = true;
        _data = null;

        if (_moveCts != null) StopCoroutine(_moveCts);
        _moveCts = null;

        if (_dissolveCts != null) StopCoroutine(_dissolveCts);
        _dissolveCts = null;

        _objectBaseMono = null;
        PoolHolder.Instance.Release(this);
    }

    #endregion

    #region <========================= ANIMATION FUNCTION =========================>

    protected void PlayAnimPeaIdle() => PlayPeaAnim(AnimHash.PEA_IDLE_STR);
    public void PlayAnimPeaBunny() => PlayPeaAnim(AnimHash.PEA_BUNNY_STR);
    protected void PlayAnimPeaJump() => PlayPeaAnim(AnimHash.PEA_JUMP_STR);

    protected void PlayAnimShooterIdle() => PlayShooterAnim(AnimHash.SHOOTER_IDLE);
    protected void PlayAnimShooterAppear()
    {
        SoundManager.Instance?.PlayOneShot(AudioClipName.Shooter_Arrive);
        PlayShooterAnim(AnimHash.SHOOTER_APPEAR);
    }
    protected void PlayAnimShooterShoot() => PlayShooterAnim(AnimHash.SHOOTER_SHOOT);

    protected void SetupAnim() => PlayAnimPeaIdle();

    protected void PlayShooterAnim(string animName) => PlayShooterAnim(Animator.StringToHash(animName));
    protected void PlayShooterAnim(int animHash)
    {
        // Animator currently disabled
    }

    protected void PlayPeaAnim(string animName)
    {
        if (_peaAnimator == null)
        {
            Debug.LogError("Animator is not assigned on " + gameObject.name);
            return;
        }
        _peaAnimator.Play(animName, 0, 0f);
    }

    protected void RotateToObjectBaseMono()
    {
        if (_objectBaseMono == null) return;
        if (_shooterRotateObject == null) return;

        if (!_hasInitLocalRotation)
        {
            _initialLocalRotation = _shooterRotateObject.transform.localRotation;
            _hasInitLocalRotation = true;
        }

        Vector3 direction = _objectBaseMono.TF.position - _shooterRotateObject.transform.position;
        if (direction != Vector3.zero)
        {
            Vector3 currentLocalEuler = _shooterRotateObject.transform.localEulerAngles;
            _shooterRotateObject.transform.rotation = Quaternion.LookRotation(direction);
            float targetLocalX = _shooterRotateObject.transform.localEulerAngles.x;
            _shooterRotateObject.transform.localEulerAngles = new Vector3(-targetLocalX, currentLocalEuler.y, currentLocalEuler.z);
        }
    }

    #endregion

    public bool IsAtTopColumn()
    {
        if (_verticalLauncherMonoParent == null) return false;
        return _verticalLauncherMonoParent.IsAtTop(this);
    }

    public virtual void ChangeLayerRenderer(LayerNameGamePlay layer) { }

    public Coroutine PlayMoveToPosition(SlotLauncherMono slotLauncherMono, Vector3 targetPos, float duration = 0.3f, Action onComplete = null)
    {
        if (_moveCts != null) StopCoroutine(_moveCts);
        _moveCts = StartCoroutine(MoveToPosition(slotLauncherMono, targetPos, duration, onComplete));
        return _moveCts;
    }

    /// <summary>
    /// Dùng cho flow DỒN CHỖ trên slot (không phải jump từ cột lên).
    /// Animate đến targetPos, sau đó snap parent + localPosition về chuẩn (0,0,-0.5)
    /// để tránh lệch Z so với launcher đi qua flow jump.
    /// </summary>
    public virtual IEnumerator MoveToPosition(SlotLauncherMono slotLauncherMono, Vector3 targetPos, float duration = 0.3f, Action onComplete = null)
    {
        _tf ??= transform;

        Vector3 startPos = _tf.position;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t); // SmoothStep
            _tf.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        _tf.position = targetPos;

        // FIX: sau khi animate xong, gán đúng parent và snap localPosition về chuẩn
        // Tránh lệch Z do world→local conversion khi SetParent với worldPositionStays=true
        if (slotLauncherMono != null)
        {
            _tf.SetParent(slotLauncherMono.transform, false); // false → dùng localPosition trực tiếp
            _tf.localPosition = LocalPositionInSlot;          // snap về (0, 0, -0.5) đúng chuẩn
            _tf.localRotation = Quaternion.identity;
        }

        onComplete?.Invoke();
        _moveCts = null;
    }

    #region MECHANIC

    public virtual void OnBecomeTop() { }

    #region Frozen

    public virtual void OnInitFrozen(int frozened)
    {
        if (frozened <= 0 && _frozenedGameObject != null)
        {
            _frozenedGameObject.SetActive(false);
            if (_objectHideIfEnableFrozened != null) _objectHideIfEnableFrozened.SetActive(true);
            _canSelect = true;
            return;
        }

        _canSelect = false;
        if (_objectHideIfEnableFrozened != null) _objectHideIfEnableFrozened.SetActive(false);
        GameEventBus.AssignLauncher += UpdateStateFrozen;
        if (_tmpFrozened != null)
            _tmpFrozened.text = _data.Frozened.ToString();
        if (_frozenedGameObject != null)
            _frozenedGameObject.SetActive(true);
    }

    public void UpdateStateFrozen()
    {
        _data.Frozened--;
        if (_data.Frozened <= 0)
            OnDestroyFrozen();
        _tmpFrozened.text = _data.Frozened.ToString();
    }

    public virtual void OnDestroyFrozen()
    {
        GameEventBus.AssignLauncher -= UpdateStateFrozen;
        if (_frozenedGameObject != null)
            _frozenedGameObject.SetActive(false);
        if (_objectHideIfEnableFrozened != null) _objectHideIfEnableFrozened.SetActive(true);
        _canSelect = true;
    }

    #endregion

    #endregion
}