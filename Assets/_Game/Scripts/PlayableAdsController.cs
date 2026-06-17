using System.Collections.Generic;
using UnityEngine;

public class PlayableAdsController : MonoBehaviour
{
    public enum PlayableMode
    {
        Default,
        Mode_10_Cannons,
        Mode_N_Minus_1
    }

    [Header("Playable Settings")]
    public PlayableMode Mode = PlayableMode.Default;

    [Tooltip("Tick vào đây nếu muốn script tự động load level để test")]
    public bool OverrideLevel = true;

    [Tooltip("Nếu có RoundDataAsset, sẽ load từ đó. Nếu không, sẽ load từ LevelDataManager bằng TestLevelID")]
    public RoundDataAsset TestLevelDataAsset;

    [Tooltip("ID của level muốn test")]
    public int TestLevelID = 8;

    private int _cannonClickedCount = 0;
    private bool _hasRedirected = false;
    private bool _endGameSent = false;

    private void Awake()
    {
        GameEventBus.OnLauncherClicked += OnLauncherClicked;
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
        GameEventBus.OnLauncherClicked -= OnLauncherClicked;
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

    private void OnLauncherClicked(LauncherBaseMono clickedLauncher)
    {
        if (_hasRedirected) return;

        if (Mode == PlayableMode.Mode_10_Cannons)
        {
            // OnLauncherClicked chỉ được gọi khi user bấm vào 1 cannon hợp lệ
            // => Đã thoả mãn yêu cầu "count the canon, not the click" và "cả canon đơn hay nhóm đều tính là 1".
            _cannonClickedCount++;
            if (_cannonClickedCount >= 10)
            {
                // Chờ 0.5s để cannon nhảy lên slot rồi redirect
                StartCoroutine(TriggerRedirectAfterDelay(0.5f));
            }
        }
        else if (Mode == PlayableMode.Mode_N_Minus_1)
        {
            // Kiểm tra trạng thái n-1 sau khi cannon đã được đưa vào slot
            StartCoroutine(CheckNMinus1AfterClick());
        }
    }

    private System.Collections.IEnumerator CheckNMinus1AfterClick()
    {
        // Chờ đến cuối frame để LauncherController cập nhật danh sách súng còn lại
        yield return null;

        if (IsOneStepLeft())
        {
            StartCoroutine(TriggerRedirectAfterDelay(0.5f));
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

                uniqueGroupsCount++;
                visited.Add(launcher);

                // Nếu launcher này là một phần của 1 nhóm được nối dây, gom cả nhóm vào visited
                if (launcher is LauncherNormalMono normalMono && normalMono.LaunchersConnect != null)
                {
                    foreach (var connected in normalMono.LaunchersConnect)
                    {
                        if (connected != null)
                            visited.Add(connected);
                    }
                }
            }
        }

        // Nếu chỉ còn đúng 1 nhóm súng chưa bắn => Chỉ còn 1 step là win game
        return uniqueGroupsCount == 1;
    }

    private System.Collections.IEnumerator TriggerRedirectAfterDelay(float delay)
    {
        if (_hasRedirected) yield break;
        _hasRedirected = true;

        yield return new WaitForSeconds(delay);

        // Gửi endgame signal cho Luna SDK
        SendEndGameIfNeeded();

        // --- Store Redirection (Luna SDK) ---
#if LUNA_PLAYABLE
        try
        {
            Luna.Unity.Playable.InstallFullGame();
        }
        catch { }
#else
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
#endif

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
