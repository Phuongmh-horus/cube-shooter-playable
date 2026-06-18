using System;
using UnityEngine;

public class LauncherProjectile : MonoBehaviour
{
    #region <========================= PROPERTY & FIELD =========================>

    public VFX_Cube_Break vfxCubeBreak;
    private static float Speed = 10f;
    private Transform _tf;

    private ObjectBaseMono _target;
    private CubeShooterColor _color;
    private bool _isMoving;
    private Action _onHitCallback;

    #endregion

    #region <========================= UNITY CORE =========================>

    private void Awake()
    {
        GameEventBus.ACDespawnLauncherProjectile.Add(OnDespawn);
    }

    #endregion

    #region <========================= GET & SET =========================>

    public static void SetSpeed(float speed)
    {
        Speed = speed;
    }

    #endregion

    #region <========================= INIT DESPAWN =========================>

    /// <summary>
    /// Khởi tạo viên đạn, mục tiêu và bắt đầu di chuyển
    /// </summary>
    public void OnInit(Vector3 startPos, ObjectBaseMono target, Action onHitCallback = null)
    {
        _tf ??= transform;
        _tf.position = startPos;
        _target = target;
        _color = target.GetColor();
        _onHitCallback = onHitCallback;
        _isMoving = true;
        gameObject.SetActive(true);
        StartCoroutine(MoveToTarget());
    }

    /// <summary>
    /// Đưa viên đạn về trạng thái chờ (ẩn đi)
    /// </summary>
    public void OnDespawn()
    {
        if (_target == null)
            return;
        _tf.localPosition = Vector3.zero;
        _isMoving = false;
        _target = null;
        _onHitCallback = null;
        PoolHolder.Instance.Release(this);
    }

    #endregion

    #region <========================= PRIVATE METHOD =========================>

    private System.Collections.IEnumerator MoveToTarget()
    {
        while (_isMoving && _target != null)
        {
            Vector3 targetPos = _target.TF.position;

            _tf.position = Vector3.MoveTowards(_tf.position, targetPos, Speed * Time.deltaTime);

            // Check tới nơi
            if (Vector3.SqrMagnitude(_tf.position - targetPos) < 0.01f)
            {
                HitTarget();
                yield break;
            }

            yield return null;
        }

        // Nếu target biến mất trước khi đạn tới hoặc bị vô hiệu hóa
        //OnDespawn();
    }

    /// <summary>
    /// Xử lý khi va chạm mục tiêu: Phá hủy mục tiêu và despawn đạn
    /// </summary>
    private void HitTarget()
    {
        _onHitCallback?.Invoke();
        _target.OnDespawn();
        var vfx = PoolHolder.Instance.Get(vfxCubeBreak);

        if (PlayableAdsUIController.Instance == null || !PlayableAdsUIController.Instance.IsShowingEndcard)
        {
            SoundManager.Instance?.PlayOneShot(AudioClipName.Cube_Destroy);
        }

        if (vfx is VFX_Cube_Break vfxdemo)
            vfxdemo.OnInit(_tf.position, _color);

        OnDespawn();
    }

    #endregion

}
