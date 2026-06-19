using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class ObjPieceMono : ObjectBaseMono
{
    [SerializeField] private MeshRenderer _meshRenderer;
    private PieceData _data;

    public Transform MeshTransform => _meshRenderer != null ? _meshRenderer.transform : transform;
    public Color CurrentBaseColor { get; private set; }
    public float CurrentSpecularSize { get; private set; }
    public float CurrentSpecularSmoothness { get; private set; }

    public override CubeShooterColor GetColor() => _data.Color;

    protected MaterialPropertyBlock _propertyBlock;

    public override void OnInit(ObjectBaseData piece)
    {
        CanDespawn = true;
        base.OnInit(piece);
        _data = piece as PieceData;
        _meshRenderer.sharedMaterial = ConfigHolder.Instance.ColorPallete_ForPiece.colorDictionary[GetColor()];
        if (_meshRenderer != null) _meshRenderer.enabled = true;
        if (_propertyBlock == null)
            _propertyBlock = new MaterialPropertyBlock();
        // RandomColor(); // Disabled to maintain GPU Instancing and reduce draw calls
    }


    public override void OnDespawn()
    {
        if (!CanDespawn)
            return;
        CanDespawn = false;
        base.OnDespawn();
        isBulletIncoming = false;
        PoolHolder.Instance.Release(this);
    }

    protected void RandomColor()
    {
        var pieceConfigSO = ConfigHolder.Instance.PieceConfigSO;
        if (pieceConfigSO == null || pieceConfigSO._pieceConfigs == null || pieceConfigSO._pieceConfigs.Count == 0)
        {
            return;
        }

        var random = Random.Range(0, pieceConfigSO._pieceConfigs.Count);
        var SpecularSize = pieceConfigSO._pieceConfigs[random].SpecularSize;
        var SpecularSmoothing = pieceConfigSO._pieceConfigs[random].SpecularSmoothing;
        var Hue = pieceConfigSO._pieceConfigs[random].H_HSVColor;
        var Saturation = pieceConfigSO._pieceConfigs[random].S_HSVColor;
        var Brightness = pieceConfigSO._pieceConfigs[random].V_HSVColor;

        CurrentSpecularSize = SpecularSize;
        CurrentSpecularSmoothness = SpecularSmoothing;

        _propertyBlock.SetFloat("_SpecularToonSize", SpecularSize);
        _propertyBlock.SetFloat("_SpecularToonSmoothness", SpecularSmoothing);

        Color baseColor = ConfigHolder.Instance.ColorPallete_ForPiece.GetColorBase(GetColor());
        float h, s, v;
        Color.RGBToHSV(baseColor, out h, out s, out v);

        CurrentBaseColor = Color.HSVToRGB(h + Hue / 255f, s + Saturation / 255f, v + Brightness / 255f);

        _propertyBlock.SetColor("_BaseColor", CurrentBaseColor);
        _meshRenderer.SetPropertyBlock(_propertyBlock);
    }
}
