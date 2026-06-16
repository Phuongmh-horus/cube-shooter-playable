using UnityEngine;

[CreateAssetMenu(fileName = "RotationModel3DConfigSO", menuName = "GamePlay/RotationModel3DConfigSO")]
public class RotationModel3DConfigSO : ScriptableObject
{
    [Header("Trục xoay")]
    public Vector3 RotationAxis = Vector3.up;

    [Header("Auto Rotate")]
    [Tooltip("Tốc độ xoay tự động mặc định (độ/giây)")]
    public float DefaultAutoSpeed = 10f;

    [Tooltip("Tốc độ xoay tự động khi end game (độ/giây)")]
    public float EndGameAutoSpeed = 10f;

    [Tooltip("Thời gian lerp khi chuyển đổi tốc độ auto (giây)")]
    public float SpeedTransitionDuration = 0.5f;

    [Header("Manual Rotate")]
    [Tooltip("Tốc độ xoay bằng tay (hệ số nhân delta touch)")]
    public float ManualRotateSpeed = 0.5f;

    [Header("Delay sau khi thả tay")]
    [Tooltip("Sau khi thả tay bao nhiêu giây thì auto xoay tiếp")]
    public float DelayBeforeAutoResume = 1.5f;

    [Header("Smooth Drag settings")]
    [Tooltip("Hệ số mượt mà khi slerp xoay")]
    public float SmoothFactor = 7f;

    [Header("Warning & Target Finding Settings")]
    [Tooltip("Tốc độ xoay tự động khi cảnh báo tìm màu (độ/giây)")]
    public float SpeedRotationFindColor = 45f;
}
