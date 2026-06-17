using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ColorPallete", menuName = "ScriptableObjects/Color/ColorPallete")]
public class ColorPallete : ScriptableObject
{
    private Dictionary<CubeShooterColor, Material> _colorDictionary;
    public Dictionary<CubeShooterColor, Material> colorDictionary
    {
        get
        {
            if (_colorDictionary == null || _colorDictionary.Count == 0)
            {
                _colorDictionary = new Dictionary<CubeShooterColor, Material>();
                for (int i = 0; i < Mathf.Min(colorKeys.Count, colorValues.Count); i++)
                {
                    _colorDictionary[colorKeys[i]] = colorValues[i];
                }
            }
            return _colorDictionary;
        }
    }

    public List<CubeShooterColor> colorKeys = new List<CubeShooterColor>();
    public List<Material> colorValues = new List<Material>();

    public void OnEnable()
    {
        _colorDictionary = null;
    }

    [Header("Special Materials")]
    public Material HiddenMaterial;
    public Color HiddenLineColor;

    public Color GetColorBase(CubeShooterColor color)
    {
        if (!colorDictionary.TryGetValue(color, out Material material) || material == null)
        {
            Debug.LogError($"Color {color} not found in ColorPallete!");
            return Color.white;
        }

        if (material.HasProperty("_Color"))
        {
            return material.GetColor("_Color");
        }

        if (material.HasProperty("_BaseColor"))
        {
            return material.GetColor("_BaseColor");
        }

        if (material.HasProperty("_HColor")) // Toony Colors Pro 2 highlight/base color
        {
            return material.GetColor("_HColor");
        }

        Debug.LogWarning($"Material '{material.name}' with Shader '{material.shader.name}' doesn't have a recognized color property. Returning Color.white as fallback.");
        return Color.white;
    }

}