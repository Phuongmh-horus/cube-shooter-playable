using System;
using UnityEngine;

public class LauncherProjectile : MonoBehaviour
{
    #region <========================= PROPERTY & FIELD =========================>

    public VFX_Cube_Break vfxCubeBreak;
    private static float Speed = 10f;
    private static int _lastSoundFrame = -1;
    private Transform _tf;

    private ObjectBaseMono _target;
    private CubeShooterColor _color;
    private bool _isMoving;
    private Action _onHitCallback;

    public static System.Collections.Generic.List<LauncherProjectile> ActiveProjectiles = new System.Collections.Generic.List<LauncherProjectile>(500);

    private ParticleSystem[] _particleSystems;

    public static void UpdateAllProjectiles()
    {
        for (int i = ActiveProjectiles.Count - 1; i >= 0; i--)
        {
            var proj = ActiveProjectiles[i];
            if (proj == null || !proj._isMoving || !proj.gameObject.activeInHierarchy)
            {
                int lastIdx = ActiveProjectiles.Count - 1;
                ActiveProjectiles[i] = ActiveProjectiles[lastIdx];
                ActiveProjectiles.RemoveAt(lastIdx);
                continue;
            }

            if (proj._target == null)
            {
                proj.OnDespawn();
                continue;
            }

            Vector3 targetPos = proj._target.TF.position;
            proj._tf.position = Vector3.MoveTowards(proj._tf.position, targetPos, Speed * Time.deltaTime);

            if (Vector3.SqrMagnitude(proj._tf.position - targetPos) < 0.01f)
            {
                proj._isMoving = false;
                proj.HitTarget();
            }
        }

        // Cập nhật tất cả hiệu ứng đuôi đạn và mảnh vỡ một lần (tối ưu thay vì dùng LateUpdate/Update cho từng script)
        BulletTrailRotation.UpdateAllTrails();
    }

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

        if (target != null && target.TF != null)
        {
            Vector3 dir = target.TF.position - startPos;
            if (dir.sqrMagnitude > 0.0001f)
            {
                _tf.up = dir.normalized;
            }
        }

        gameObject.SetActive(true);

        // Clear ParticleSystems to prevent streaks from previous/prewarm positions
        if (_particleSystems == null)
        {
            _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        }
        foreach (var ps in _particleSystems)
        {
            ps.Clear();
            ps.Play();
        }

        if (!ActiveProjectiles.Contains(this)) ActiveProjectiles.Add(this);

    }

    /// <summary>
    /// Đưa viên đạn về trạng thái chờ (ẩn đi)
    /// </summary>
    public void OnDespawn()
    {
        if (_target == null)
            return;
        _isMoving = false;
        _target = null;
        _onHitCallback = null;
        PoolHolder.Instance.Release(this);
    }

    #endregion

    /// <summary>
    /// Xử lý khi va chạm mục tiêu: Phá hủy mục tiêu và despawn đạn
    /// </summary>
    private void HitTarget()
    {
        _onHitCallback?.Invoke();
        _target.OnDespawn();

        var vfx = PoolHolder.Instance.Get(vfxCubeBreak, null, _tf.position);
        if (vfx is VFX_Cube_Break vfxdemo)
            vfxdemo.OnInit(_tf.position, _color);

        if (PlayableAdsUIController.Instance == null || !PlayableAdsUIController.Instance.IsShowingEndcard)
        {
            if (Time.frameCount != _lastSoundFrame)
            {
                _lastSoundFrame = Time.frameCount;
                SoundManager.Instance?.PlayOneShot(AudioClipName.Cube_Destroy);
            }
        }

        OnDespawn();
    }

}
