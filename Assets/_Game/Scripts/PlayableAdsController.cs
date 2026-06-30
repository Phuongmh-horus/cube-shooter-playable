using System.Collections.Generic;
using UnityEngine;

public class PlayableAdsController : MonoBehaviour
{
    public static PlayableAdsController Instance { get; private set; }

    public enum PlayableMode
    {
        Default,
        Mode_10_Cannons,
        Mode_N_Minus_1
    }

    [Header("Playable Settings")]
    public bool EnableLineConnector = false;
    public PlayableMode Mode = PlayableMode.Default;

    [Tooltip("Tick vào đây nếu muốn script tự động load level để test")]
    public bool OverrideLevel = true;

    [Tooltip("Nếu có RoundDataAsset, sẽ load từ đó")]
    public RoundDataAsset TestLevelDataAsset;

    private int _cannonClickedCount = 0;
    private bool _hasRedirected = false;
    private bool _endGameSent = false;

    private void Awake()
    {
        Instance = this;
        GameEventBus.OnLauncherAssignedToSlot += OnLauncherAssignedToSlot;
        GameEventBus.OnLoadLevelDone += ConfigurePlayableSlotBehavior;
    }

    private void Start()
    {

        if (OverrideLevel)
        {
            // Ghi đè level hiện tại bằng level ID test thông qua cơ chế chuẩn của game
            StartCoroutine(LoadCustomLevelAsync());
        }
    }

    private void OnDestroy()
    {
        GameEventBus.OnLauncherAssignedToSlot -= OnLauncherAssignedToSlot;
        GameEventBus.OnLoadLevelDone -= ConfigurePlayableSlotBehavior;
    }

    private System.Collections.IEnumerator LoadCustomLevelAsync()
    {
        // Đợi LevelSystem, ConfigHolder, PoolHolder khởi tạo xong (có timeout để tránh treo game)
        yield return new WaitUntil(() => LevelSystem.Instance != null && ConfigHolder.Instance != null && PoolHolder.Instance != null);

        ConfigurePlayableSlotBehavior();

        if (TestLevelDataAsset != null)
        {
            Debug.Log($"[PlayableAdsController] Đang load level từ SO: {TestLevelDataAsset.name}");

            // Gán level hiện tại theo SO
            LevelSystem.CurrentLevelProgress = TestLevelDataAsset.LevelId;
            LevelSystem.CurrentLevelDisplay = TestLevelDataAsset.LevelId;

            // Gọi hàm LoadLevel đặc biệt dành riêng cho PLA (không dùng LevelDataManager)
            yield return StartCoroutine(LevelSystem.Instance.LoadLevelFromPLA(TestLevelDataAsset));
            Debug.Log($"[PlayableAdsController] Load xong từ SO!");
        }

    }

    private void ConfigurePlayableSlotBehavior()
    {
        var slotQueueController = LevelSystem.Instance?.SlotLauncherQueueController;
        if (slotQueueController == null) return;

        // Tắt chức năng tự động đẩy súng lên slot ở cuối game vì nó gây xung đột với kịch bản Playable Ads
        // CHỈ TẮT nếu mode KHÔNG PHẢI là Default
        bool isDefaultMode = Mode != PlayableMode.Mode_N_Minus_1;
        slotQueueController.SetAutoFillSlotsEnabled(isDefaultMode);
    }

    private void OnLauncherAssignedToSlot(LauncherBaseMono assignedLauncher)
    {
        if (_hasRedirected) return;

        if (Mode == PlayableMode.Mode_10_Cannons)
        {
            _cannonClickedCount++;
            if (_cannonClickedCount >= 10)
            {
                _hasRedirected = true;
                LevelSystem.IsEndGame = true;
                GameEventBus.OnActiveInputGameplay?.Invoke(false);
                StartCoroutine(TriggerRedirectAfterDelay(0.2f));
            }
        }
        else if (Mode == PlayableMode.Mode_N_Minus_1)
        {
            if (IsOneStepLeft())
            {
                _hasRedirected = true;
                LevelSystem.IsEndGame = true;
                GameEventBus.OnActiveInputGameplay?.Invoke(false);
                StartCoroutine(TriggerRedirectAfterDelay(0.2f));
            }
        }
    }

    private bool IsOneStepLeft()
    {
        var launchers = LevelSystem.Instance.LauncherController.GetVerticalLaunchers();
        if (launchers == null || launchers.Count == 0) return false;

        int uniqueGroupsCount = 0;
        HashSet<LauncherBaseMono> visited = new HashSet<LauncherBaseMono>();

        foreach (var vertical in launchers)
        {
            if (vertical == null || vertical.LauncherBaseMonos == null) continue;

            foreach (var launcher in vertical.LauncherBaseMonos)
            {
                if (launcher == null || visited.Contains(launcher)) continue;

                if (launcher is LauncherNormalMono normalMono && normalMono.LaunchersConnect != null)
                {
                    foreach (var connected in normalMono.LaunchersConnect)
                    {
                        if (connected != null) visited.Add(connected);
                    }
                }

                visited.Add(launcher);
                uniqueGroupsCount++;
            }
        }

        // Nếu chỉ còn đúng 1 nhóm súng => Thỏa mãn điều kiện N-1
        // (Súng vừa bấm đã bị xóa khỏi Vertical Launchers rồi nên không cần đếm trừ ra nữa)
        return uniqueGroupsCount == 1;
    }

    private System.Collections.IEnumerator TriggerRedirectAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Gửi endgame signal cho Luna SDK
        SendEndGameIfNeeded();

        // --- Store Redirection (Luna SDK) ---
        Luna.Unity.Playable.InstallFullGame();

        // Fallback dùng Reflection phòng khi Luna có trong project nhưng chưa bật macro LUNA_PLAYABLE
        try
        {
            System.Type lunaLifeCycle = System.Type.GetType("Luna.Unity.LifeCycle, Luna.Unity");
            if (lunaLifeCycle != null)
            {
                var method = lunaLifeCycle.GetMethod("TryInstallFullGame", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                method?.Invoke(null, null);
            }
        }
        catch (System.Exception e) { Debug.Log("[PlayableAdsController] Luna SDK TryInstallFullGame Error: " + e.Message); }

        ShowEndcardAndDimScreen();
    }

    private void SendEndGameIfNeeded()
    {
        if (_endGameSent) return;
        _endGameSent = true;
        Luna.Unity.LifeCycle.GameEnded();
    }

    private void ShowEndcardAndDimScreen()
    {
        // 1. Đánh dấu endgame
        LevelSystem.IsEndGame = true;

        // 2. Hiện Endcard Panel có sẵn trong scene thông qua PlayableAdsUIController
        if (PlayableAdsUIController.Instance != null)
        {
            PlayableAdsUIController.Instance.ShowEndcard();
        }
        else
        {
            Debug.LogWarning("[PlayableAdsController] Không tìm thấy PlayableAdsUIController trong Scene. Không thể hiện Endcard Panel!");
        }
    }
}
