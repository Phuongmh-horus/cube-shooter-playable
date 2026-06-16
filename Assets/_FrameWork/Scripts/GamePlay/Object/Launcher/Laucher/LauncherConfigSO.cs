using UnityEngine;

[CreateAssetMenu(fileName = "LauncherConfigSO", menuName = "GamePlay/LauncherConfigSO")]
public class LauncherConfigSO : ScriptableObject
{
    [Tooltip("Khoảng cách giữa các cột súng với nhau")] public float SpacingVerticalLauncher;
    [Tooltip("Khoảng cách giữa các súng với nhau")] public float SpacingLauncher;
    [Tooltip("Thời gian di chuyển của Launcher")] public float TimeMoveLauncherNormal = 0.3f;
    [Tooltip("Thời gian dồn của Launcher")] public float TimeSortLauncherNormal = 0.15f;

    [Header("Jumping & Rising Timings")]
    [Tooltip("Thời gian súng bay vào hố")] public float TimeJumpToHole = 0.4f;
    [Tooltip("Khoảng cách súng chìm xuống hố theo trục Z")] public float ZPullAwayDistance = 5f;
    [Tooltip("Thời gian súng chìm xuống hố theo trục Z")] public float TimeZPullAway = 0.3f;
    [Tooltip("Khoảng cách súng bắt đầu trồi lên ở slot theo trục Z")] public float ZRiseStartDistance = 5f;
    [Tooltip("Thời gian súng trồi lên tại slot")] public float TimeRiseInSlot = 0.3f;

    [Header("Rabbit Jump Move")]
    [Tooltip("Khoảng cách mỗi bước nhảy")] public float JumpStepDistance = 1f;
    [Tooltip("Thời gian mỗi bước nhảy")] public float JumpStepDuration = 0.2f;
    [Tooltip("Độ cao của mỗi bước nhảy")] public float JumpStepHeight = 0.5f;
}