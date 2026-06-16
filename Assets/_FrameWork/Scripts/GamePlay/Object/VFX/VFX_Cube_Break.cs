using System.Collections;
using UnityEngine;


public class VFX_Cube_Break : MonoBehaviour
{
    [Header("VFX Particle Systems")]
    [SerializeField] private ParticleSystem _particleSystem;
    [SerializeField] private ParticleSystemRenderer[] _vfxs;

    public void OnInit(Vector3 os, CubeShooterColor _color)
    {
        transform.position = os;
        _particleSystem.Play();
        SetColorsFromBase(_color, ConfigHolder.Instance.ColorPallete_ForPiece);
        DespawnAfterSeconds(.9f);
    }

    /// <summary>
    /// Sets the colors of the 5 VFX based on the base color.
    /// </summary>
    /// <param name="baseColorCode">The CubeShooterColor base color enum</param>
    /// <param name="colorPallete">The color palette to resolve baseColorCode to a real Color object</param>
    public void SetColorsFromBase(CubeShooterColor baseColorCode, ColorPallete colorPallete)
    {
        Material material = colorPallete.colorDictionary[baseColorCode];
        foreach (var VARIABLE in _vfxs)
            VARIABLE.sharedMaterial = material;
    }

    /// <summary>
    /// Waits for the specified seconds then automatically despawns this VFX.
    /// </summary>
    public void DespawnAfterSeconds(float seconds)
    {
        StartCoroutine(DespawnAfterSecondsAsync(seconds));
    }

    /// <summary>
    /// UniTask to wait for a duration in seconds then despawn this VFX.
    /// </summary>
    public IEnumerator DespawnAfterSecondsAsync(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        OnDespawn();
    }

    public void OnDespawn()
    {
        PoolHolder.Instance.Release(this);
    }
}
