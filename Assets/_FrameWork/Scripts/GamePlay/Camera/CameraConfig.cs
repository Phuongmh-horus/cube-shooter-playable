using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CameraConfig", menuName = "ScriptableObjects/CameraConfig")]
public class CameraConfig : ScriptableObject
{
    [Header("Data dùng chung nè")]
    public float Speed = 1f;

    [Header("Data dùng riêng từng mode nè")]
    public List<CameraConfigData> CameraConfigDatas;

    public CameraConfigData GetConfigData(CameraState state)
    {
        if (CameraConfigDatas == null) return null;
        for (int i = 0; i < CameraConfigDatas.Count; i++)
        {
            if (CameraConfigDatas[i].CameraState == state)
            {
                return CameraConfigDatas[i];
            }
        }
        return null;
    }
}

[Serializable]
public class CameraConfigData
{
    public CameraState CameraState;
    public Vector3 CamPosition;
    public Vector3 CamRotation;
}

public enum CameraState
{
    MainMenuView,
    GamePlayView,
}