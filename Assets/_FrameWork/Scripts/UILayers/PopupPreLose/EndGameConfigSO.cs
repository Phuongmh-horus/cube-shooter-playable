using UnityEngine;

[CreateAssetMenu(fileName = "EndGameConfigSO", menuName = "GamePlay/EndGameConfigSO")]
public class EndGameConfigSO : ScriptableObject
{
    [Header("Prelose Game")]
    [Tooltip("Không có hộp nào được bắn trong ...giây này thì cảnh báo hoặc xoay nhanh")] public float NoTargetTimeoutToWarning = 5;
    [Tooltip("Delay ...giây -> check lại màu -> không có màu thì prelose")] public float DelayWarningToPrelose = 3;
    [Tooltip("Tốc độ mờ dần/hiện lại (giây) của canvas khi giữ nút")] public float FadeDuration = 0.25f;
    [Tooltip("Giá mua để chơi lại")] public int PriceRevice = 900;
    [Tooltip("Số lượng slot mở thêm khi Prelose")] public int CountOpenUpSlot = 3;

    [Header("Win Game")]
    public float TimeDelayShowUIEndGame = 1.5f;
}