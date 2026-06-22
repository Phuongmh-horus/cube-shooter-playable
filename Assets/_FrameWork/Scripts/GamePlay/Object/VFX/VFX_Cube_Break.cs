using System.Collections;
using UnityEngine;

/// <summary>
/// VFX cube vỡ — Luna/WebGL compatible.
/// Tối ưu GC và Draw Calls: Dùng sharedMaterial trực tiếp, check IsAlive trong Update thay vì Coroutine.
/// </summary>
public class VFX_Cube_Break : MonoBehaviour
{
    [SerializeField] private ParticleSystem _particleSystem;
    [SerializeField] private ParticleSystemRenderer[] _vfxs;

    private void Awake()
    {
        enabled = false;
    }

    public void OnInit(Vector3 pos, CubeShooterColor color)
    {
        transform.position = pos;
        SetColorsFromBase(color, ConfigHolder.Instance.ColorPallete_ForPiece);
        _particleSystem.Play();
        StartCoroutine(WaitAndDespawn());
    }

    /// <summary>
    /// Dùng chung sharedMaterial có sẵn trong bảng màu.
    /// Tránh đổi thuộc tính material hay dùng MaterialPropertyBlock (MPB có thể break SRP Batching ở một số case).
    /// Việc dùng sharedMaterial nguyên bản giúp gom toàn bộ VFX cùng màu vào 1 Draw Call.
    /// </summary>
    private void SetColorsFromBase(CubeShooterColor baseColorCode, ColorPallete colorPallete)
    {
        if (colorPallete == null || colorPallete.colorDictionary == null) return;
        if (!colorPallete.colorDictionary.TryGetValue(baseColorCode, out Material mat) || mat == null) return;

        foreach (var vfxRenderer in _vfxs)
        {
            if (vfxRenderer == null) continue;
            vfxRenderer.sharedMaterial = mat;
        }
    }

    private IEnumerator WaitAndDespawn()
    {
        while (_particleSystem != null && _particleSystem.IsAlive(true))
        {
            yield return new WaitForSeconds(0.25f);
        }
        OnDespawn();
    }

    public void OnDespawn()
    {
        enabled = false;
        if (_particleSystem != null)
            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        PoolHolder.Instance.Release(this);
    }
}