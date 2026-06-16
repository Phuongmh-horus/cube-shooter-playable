using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PieceConfigSO", menuName = "ScriptableObjects/PieceConfigSO")]
public class PieceConfigSO : ScriptableObject
{
    public List<PieceConfig> _pieceConfigs = new List<PieceConfig>();
}

[Serializable]
public class PieceConfig
{
    public float SpecularSize;
    public float SpecularSmoothing;
    public float H_HSVColor = 1.0f;
    public float S_HSVColor = 1.0f;
    public float V_HSVColor = 1.0f;
}