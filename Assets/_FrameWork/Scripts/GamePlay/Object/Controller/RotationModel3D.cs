using System.Collections;
using UnityEngine;

public class RotationModel3D : MonoBehaviour
{
    #region <========================= PROPERTY & FIELD =========================>

    [SerializeField] private RotationModel3DConfigSO _config;
    [SerializeField] private Camera _inputCamera;
    [SerializeField] private bool _blockAutoRotation = false;

    // Cache components
    private Transform _cachedTransform;

    // Pivot - điểm giữa xoay quanh
    private Vector3 _pivot;

    // Auto rotate
    private float _currentAutoSpeed;
    private bool _isAutoRotating;

    // Touch tracking
    private bool _isTouching;
    private bool _blockGamePlayInput;

    public bool IsRotationHand { get; set; } = false;

    private Coroutine _updateLoopCoroutine;
    private Coroutine _speedTransitionCoroutine;
    private Coroutine _delayResumeCoroutine;

    private Quaternion _targetRotation;

    #endregion

    #region <========================= UNITY CORE =========================>

    private void Awake()
    {
        _cachedTransform = transform;
        GameEventBus.OnStartDragAction += OnGameMouseDown;
        GameEventBus.OnDragAction += OnGameDrag;
        GameEventBus.ActiveAutoRotationModel += ActiveAutoRotationModel;
    }

    private void OnDestroy()
    {
        StopRotation();
        GameEventBus.OnStartDragAction -= OnGameMouseDown;
        GameEventBus.OnDragAction -= OnGameDrag;
        GameEventBus.ActiveAutoRotationModel -= ActiveAutoRotationModel;
    }

    private void OnBlockGamePlayInput(bool block)
    {
        _blockGamePlayInput = block;
    }

    #endregion

    #region <========================= PUBLIC METHOD =========================>

    /// <summary>
    /// Bắt đầu vòng lặp xoay (auto + touch). Truyền pivot là điểm giữa xoay quanh.
    /// </summary>
    public void StartRotation(Vector3 pivot)
    {

        if (_config == null)
        {
            Debug.LogError($"[RotationModel3D] _config is NULL! Assign TableObjectConfigSO in Inspector.", this);
            enabled = false;
            return;
        }
        StopRotation();

        _pivot = pivot;
        _isAutoRotating = true;
        IsRotationHand = false;
        _isTouching = false;
        _currentAutoSpeed = _config.DefaultAutoSpeed;

        _updateLoopCoroutine = StartCoroutine(UpdateLoopRoutine());
    }

    /// <summary>
    /// Dừng hoàn toàn vòng lặp xoay.
    /// </summary>
    public void StopRotation()
    {
        _isAutoRotating = false;
        IsRotationHand = false;
        _isTouching = false;

        if (_updateLoopCoroutine != null) StopCoroutine(_updateLoopCoroutine);
        if (_speedTransitionCoroutine != null) StopCoroutine(_speedTransitionCoroutine);
        if (_delayResumeCoroutine != null) StopCoroutine(_delayResumeCoroutine);
    }

    /// <summary>
    /// Ngừng xoay hoàn toàn và reset góc xoay local (localEulerAngles) của mô hình về 0.
    /// </summary>
    public void ResetRotation()
    {
        StopRotation();
        _cachedTransform.localEulerAngles = Vector3.zero;
        _targetRotation = _cachedTransform.rotation;
    }

    public void SetEndGameSpeed()
    {
        if (_speedTransitionCoroutine != null) StopCoroutine(_speedTransitionCoroutine);
        _speedTransitionCoroutine = StartCoroutine(ChangeAutoSpeedRoutine(_config.EndGameAutoSpeed));
    }

    public void SetDefaultSpeed(bool setSmoth = false)
    {
        if (setSmoth)
        {
            if (_speedTransitionCoroutine != null) StopCoroutine(_speedTransitionCoroutine);
            _speedTransitionCoroutine = StartCoroutine(ChangeAutoSpeedRoutine(_config.DefaultAutoSpeed));
        }
        else
            _currentAutoSpeed = _config.DefaultAutoSpeed;
    }

    public void SetFindColorSpeed()
    {
        _currentAutoSpeed = _config.SpeedRotationFindColor;
    }

    public float DelayBeforeAutoResume => _config.DelayBeforeAutoResume;

    #endregion

    #region <========================= PRIVATE METHOD - UPDATE LOOP =========================>

    private IEnumerator UpdateLoopRoutine()
    {
        while (true)
        {
            if (_isTouching)
            {
                _cachedTransform.rotation = Quaternion.Slerp(_cachedTransform.rotation, _targetRotation, Time.deltaTime * _config.SmoothFactor);
            }

            if (_isAutoRotating && !_isTouching)
            {
                AutoRotate();
            }

            yield return null;
        }
    }

    #endregion

    #region <========================= PRIVATE METHOD - AUTO ROTATE =========================>

    private void AutoRotate()
    {
        if (_blockAutoRotation) return;

        // Chọn speed dựa trên IsBoosterActive (nếu có, không có thì xài mặc định)
        float speedToUse = _currentAutoSpeed;

        // Xoay theo local axis - con sẽ xoay lệch theo cha mà không bị ảnh hưởng rotation của cha
        Vector3 deltaEuler = _config.RotationAxis * speedToUse * Time.deltaTime;
        _cachedTransform.localEulerAngles += deltaEuler;
    }

    private IEnumerator ChangeAutoSpeedRoutine(float targetSpeed)
    {
        float startSpeed = _currentAutoSpeed;
        float duration = _config.SpeedTransitionDuration;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _currentAutoSpeed = Mathf.Lerp(startSpeed, targetSpeed, t);
            yield return null;
        }

        _currentAutoSpeed = targetSpeed;
    }

    private IEnumerator DelayAutoResumeRoutine()
    {
        yield return new WaitForSeconds(_config.DelayBeforeAutoResume);
        _isAutoRotating = true;
    }

    #endregion

    #region <========================= EVENT-DRIVEN DRAG & ROTATION =========================>

    private void OnGameMouseDown(bool isDown)
    {
        if (_blockGamePlayInput) return;

        if (isDown)
        {
            // GameEventBus.BlockLauncherShoot?.Invoke(true); // Tắt block để cho phép vừa drag vừa bắn
            IsRotationHand = true;
            _isTouching = true;
            _isAutoRotating = false;
            _targetRotation = _cachedTransform.rotation;

            if (_delayResumeCoroutine != null) StopCoroutine(_delayResumeCoroutine);
        }
        else
        {
            if (_isTouching)
            {
                // GameEventBus.BlockLauncherShoot?.Invoke(false);
                IsRotationHand = false;
                _isTouching = false;

                if (_delayResumeCoroutine != null) StopCoroutine(_delayResumeCoroutine);
                _delayResumeCoroutine = StartCoroutine(DelayAutoResumeRoutine());
            }
        }
    }

    private void OnGameDrag(Vector2 delta)
    {
        if (_blockGamePlayInput || !_isTouching) return;

        float speed = _config.ManualRotateSpeed;
        Vector3 rotateDir = new Vector3(delta.y, -delta.x, 0f) * speed;
        Quaternion deltaRot = Quaternion.Euler(rotateDir);
        _targetRotation = deltaRot * _targetRotation;
    }

    private void ActiveAutoRotationModel(bool active) { _isAutoRotating = active; }

    #endregion
}
