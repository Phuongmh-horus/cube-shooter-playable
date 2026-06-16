using UnityEngine;

[CreateAssetMenu(fileName = "SlotLauncherConfigSO", menuName = "GamePlay/SlotLauncherConfigSO")]
public class SlotLauncherConfigSO : ScriptableObject
{
    [Tooltip("Spacing giữa các slot")] public float Spacing = 10;
    [Tooltip("Tốc độ bắn của súng")] public int FireRateLauncherDeffault = 200;
    [Tooltip("Toocs độ tua nhanh khi cuối game mà tất cả súng bay lên rồi")] public int FireRateLauncherEndGame = 10;
    [Tooltip("Số lượng súng cùng màu để gộp lại thành 1 cái")] public int CountMechnicMathLauncher = 3;
    [Tooltip("Số lượng slot cơ bản của màn chơi")] public int CountSlotlauncherDeffault = 5;

    [Header("Projectile")]
    [Tooltip("Tốc độ bay của viên đạn deffault")] public float SpeedProjectileDeffault = 25f;
    [Tooltip("Tốc độ bay của viên đạn End Game")] public float SpeedProjectileEndGame = 50f;

    [Header("Holes Alignment & Z-Depth")]
    [Tooltip("Khoảng cách giữa các hố")] public float SpacingHoles = 1.5f;
}
