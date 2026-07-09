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

    public static System.Collections.Generic.List<VFX_Cube_Break> ActiveVFXs = new System.Collections.Generic.List<VFX_Cube_Break>(200);

    private void Awake()
    {
        enabled = false;
    }

    public void OnInit(Vector3 pos, CubeShooterColor color)
    {
        transform.position = pos;
        SetColorsFromBase(color, ConfigHolder.Instance.ColorPallete_ForPiece);
        if (_particleSystem != null)
        {
            _particleSystem.Clear();
            _particleSystem.Play();
        }
        
        if (!ActiveVFXs.Contains(this))
        {
            ActiveVFXs.Add(this);
        }
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

    public static void UpdateAllVFXs()
    {
        for (int i = ActiveVFXs.Count - 1; i >= 0; i--)
        {
            var vfx = ActiveVFXs[i];
            if (vfx == null || !vfx.gameObject.activeInHierarchy)
            {
                int lastIdx = ActiveVFXs.Count - 1;
                ActiveVFXs[i] = ActiveVFXs[lastIdx];
                ActiveVFXs.RemoveAt(lastIdx);
                continue;
            }

            if (vfx._particleSystem != null && !vfx._particleSystem.IsAlive(true))
            {
                vfx.OnDespawn();
            }
        }
    }

    public void OnDespawn()
    {
        enabled = false;
        if (_particleSystem != null)
            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        int idx = ActiveVFXs.IndexOf(this);
        if (idx >= 0)
        {
            int lastIdx = ActiveVFXs.Count - 1;
            ActiveVFXs[idx] = ActiveVFXs[lastIdx];
            ActiveVFXs.RemoveAt(lastIdx);
        }

        PoolHolder.Instance.Release(this);
    }
}