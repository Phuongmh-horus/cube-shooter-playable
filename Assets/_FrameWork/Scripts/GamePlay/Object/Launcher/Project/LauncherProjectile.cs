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
    private static int _vfxSpawnCountThisFrame = 0;
    private static int _lastVfxFrame = -1;

    public static void UpdateAllProjectiles()
    {
        for (int i = ActiveProjectiles.Count - 1; i >= 0; i--)
        {
            var proj = ActiveProjectiles[i];
            if (proj == null || !proj._isMoving || !proj.gameObject.activeInHierarchy)
            {
                ActiveProjectiles.RemoveAt(i);
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
        gameObject.SetActive(true);
        if (!ActiveProjectiles.Contains(this)) ActiveProjectiles.Add(this);

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

    /// <summary>
    /// Xử lý khi va chạm mục tiêu: Phá hủy mục tiêu và despawn đạn
    /// </summary>
    private void HitTarget()
    {
        _onHitCallback?.Invoke();
        _target.OnDespawn();

        if (Time.frameCount != _lastVfxFrame)
        {
            _lastVfxFrame = Time.frameCount;
            _vfxSpawnCountThisFrame = 0;
        }

        if (_vfxSpawnCountThisFrame < 8) // Hard cap 8 VFX per frame
        {
            var vfx = PoolHolder.Instance.Get(vfxCubeBreak);
            if (vfx is VFX_Cube_Break vfxdemo)
                vfxdemo.OnInit(_tf.position, _color);
            _vfxSpawnCountThisFrame++;
        }

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
