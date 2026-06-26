using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class ColorPalleteRestorer
{
    static ColorPalleteRestorer()
    {
        EditorApplication.delayCall += Restore;
    }

    [MenuItem("Tools/Restore Color Palletes")]
    public static void Restore()
    {
        RestorePallete("Assets/_FrameWork/Scripts/Data/ColorData/ColorPalleteSO_ForLauncher.asset", "Assets/_DongPV/Visual/Materials/Mat_ForLauncher");
        RestorePallete("Assets/_FrameWork/Scripts/Data/ColorData/ColorPalleteSO_ForObjectModel.asset", "Assets/_DongPV/Visual/Materials/Mat_ForPice");
        Debug.Log("Color Palletes Restored!");

        EditorPrefs.SetBool("ColorPalletesRestored", true);
    }

    private static void RestorePallete(string assetPath, string materialFolder)
    {
        ColorPallete pallete = AssetDatabase.LoadAssetAtPath<ColorPallete>(assetPath);
        if (pallete == null)
        {
            Debug.LogError($"Could not find pallete at {assetPath}");
            return;
        }

        pallete.colorKeys = new List<CubeShooterColor>();
        pallete.colorValues = new List<Material>();

        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { materialFolder });
        foreach (string guid in materialGuids)
        {
            string matPath = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null)
            {
                string matName = mat.name;
                if (matName.StartsWith("M_")) matName = matName.Substring(2);
                if (matName.StartsWith("Mat_")) matName = matName.Substring(4);

                if (Enum.TryParse(matName, true, out CubeShooterColor colorEnum))
                {
                    pallete.colorKeys.Add(colorEnum);
                    pallete.colorValues.Add(mat);
                }
            }
        }

        pallete.OnEnable(); // To update the dictionary immediately
        EditorUtility.SetDirty(pallete);
        AssetDatabase.SaveAssets();
    }
}
