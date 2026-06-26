using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlotLauncherQueueController : MonoBehaviour, BaseLevelGenerator
{
    #region <========================= CONSTANT =========================>
    /// <summary>
    /// Level unlock để sử dụng tính năng Merge 3 Launcher
    /// </summary>
    private const int LEVEL_UNLOCK_MATCH3 = 4;

    /// <summary>
    /// Level unlock để sử dụng tính năng Auto-Fill lên slot
    /// </summary>
    private const int LEVEL_UNLOCK_AUTO_FILL = 5;
    #endregion
    #region <========================= PROPERTY & FIELD =========================>
    [Header("Generator Level")]
    private SlotLauncherMono _slotLauncherMonoPrefab;
    [SerializeField] private Transform _parentGenSlotlauncher;

    [Header("Controller")]
    [SerializeField] private Transform _centerSortLaucherSlot;
    [SerializeField] private List<SlotLauncherMono> _launcherSlotThisLevel = new List<SlotLauncherMono>();
    public List<SlotLauncherMono> LauncherSlotThisLevel => _launcherSlotThisLevel;
    private Dictionary<CubeShooterColor, List<SlotLauncherMono>> _dicColorAndSlotLauncherActive = new Dictionary<CubeShooterColor, List<SlotLauncherMono>>();

    private float _spacingLauncherSlot = -1;
    private float _timeSortLauncherNormal = -1;
    private int _fireRateLauncher = -1;
    private int _countMechnicMath3Launcher = -1;
    private int _countSlotLauncherDeffault = -1;
    private bool _isAutoFillingSlots = false; // guard chống re-entry khi auto-fill
    public bool AutoFillSlotsEnabled { get; private set; } = true;
    private bool _blockLauncherShoot = false;
    private int _activeMergeCount = 0;
    private List<LauncherBaseMono> _activeMergeLaunchers = new List<LauncherBaseMono>();
    private List<LauncherBaseMono> _activeTransitioningLaunchers = new List<LauncherBaseMono>(); // Quản lý súng đang bay từ cột lên slot
    private bool _isMatchingLaunchers = false;

    // Cache snapshot list để tránh GC alloc mỗi tick bắn (thay vì new List<> mỗi lần)
    private readonly List<LauncherNormalMono> _shootSnapshot = new List<LauncherNormalMono>();

    private void RegisterTransitioningLauncher(LauncherBaseMono launcher)
    {
        if (launcher != null && !_activeTransitioningLaunchers.Contains(launcher))
        {
            _activeTransitioningLaunchers.Add(launcher);
        }
    }

    private void UnregisterTransitioningLauncher(LauncherBaseMono launcher)
    {
        if (launcher != null)
        {
            _activeTransitioningLaunchers.Remove(launcher);
        }
    }

    [Header("Holes Settings")]
    [SerializeField] private HoleMono _holePrefab;
    [SerializeField] private Transform _holeParent; // Vừa làm parent vừa làm tâm sắp xếp các hố

    private float _spacingHoles;
    private float _zPullAwayDistance;
    private float _zRiseStartDistance;
    private float _timeJumpToHole;
    private float _timeZPullAway;
    private float _timeRiseInSlot;
    private List<Transform> _spawnedHolesTransform = new List<Transform>();
    private List<HoleMono> _spawnedHoles = new List<HoleMono>();

    private bool _isPlayDonelauncher = false;//cho hết launcher lên rồi
    private bool _isLevelActive = false;

    #endregion

    #region <========================= UNITY CORE =========================>


    private void Awake()
    {
        GameEventBus.OnLauncherClicked += OnLauncherClickedMethos;
        GameEventBus.BlockLauncherShoot += BlockLauncherShootAC;
    }

    private void OnDestroy()
    {
        GameEventBus.OnLauncherClicked -= OnLauncherClickedMethos;
        GameEventBus.BlockLauncherShoot -= BlockLauncherShootAC;
    }



    #endregion

    #region <========================= INIT DESPAWN =========================>

    public void OnLoadLevelDone()
    {
        LauncherSlotArrangeHorizontal();
        StartUpdateLoop();

    }
    #endregion

    #region Visual

    private void LauncherSlotArrangeHorizontal()
    {
        int count = _launcherSlotThisLevel.Count;

        float startX = -(count - 1) * 0.5f * _spacingLauncherSlot;

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = _centerSortLaucherSlot.position;
            pos.x += startX + i * _spacingLauncherSlot;

            _launcherSlotThisLevel[i].transform.position = pos;
        }
    }

    #endregion

    #region CHECK AND ADD TO QUEUE

    public bool IsFullQueue()
    {
        for (int i = 0; i < _launcherSlotThisLevel.Count; i++)
        {
            if (_launcherSlotThisLevel[i].IsEmpty) return false;
        }
        return true;
    }

    public int GetCountSlotEmpty()
    {
        int sum = 0;
        foreach (SlotLauncherMono VARIABLE in _launcherSlotThisLevel)
            if (VARIABLE.IsEmpty)
                sum++;
        return sum;
    }

    /// <summary>
    /// Tìm slot trống liên tiếp cho group.
    /// - Nếu có đủ slot liên kề trống >= countSlot → dùng luôn, không dồn.
    /// - Nếu không → dồn launcher hiện có về trái (có animation) → trả slot trống ở cuối.
    /// </summary>
    public List<SlotLauncherMono> GetSlotAndSort(int countSlot)
    {
        List<SlotLauncherMono> result = new List<SlotLauncherMono>();

        // 1. Tìm slot trống liên tiếp đủ countSlot → dùng luôn
        int consecutiveCount = 0;
        int startIndex = -1;

        for (int i = 0; i < _launcherSlotThisLevel.Count; i++)
        {
            if (_launcherSlotThisLevel[i].IsEmpty)
            {
                if (consecutiveCount == 0) startIndex = i;
                consecutiveCount++;
                if (consecutiveCount >= countSlot)
                {
                    for (int j = 0; j < countSlot; j++)
                        result.Add(_launcherSlotThisLevel[startIndex + j]);
                    return result;
                }
            }
            else
            {
                consecutiveCount = 0;
                startIndex = -1;
            }
        }

        // 2. Không đủ liên tiếp → dồn hàng về trái + animation
        if (HasLauncherTransitioning())
        {
            Debug.LogWarning("[QueueController] Cannot sort slots because a launcher is transitioning!");
            return result;
        }

        List<LauncherBaseMono> activeLaunchers = new List<LauncherBaseMono>();
        foreach (var slot in _launcherSlotThisLevel)
        {
            if (!slot.IsEmpty && slot.CurrentLauncher != null)
            {
                activeLaunchers.Add(slot.CurrentLauncher);
                ClearLauncherSlot(slot);
            }
        }

        // Gán lại vào các slot đầu tiên + di chuyển visual
        for (int i = 0; i < activeLaunchers.Count; i++)
        {
            var slot = _launcherSlotThisLevel[i];
            var l = activeLaunchers[i];
            AssignLauncher(slot, l);

            // Animation dồn về vị trí mới
            if (l is LauncherNormalMono normal)
                StartCoroutine(normal.MoveToPosition(slot, slot.GetSlotPosition(), duration: _timeSortLauncherNormal));
        }

        // Trả về các slot trống liên tiếp sau khi dồn
        int firstEmpty = activeLaunchers.Count;
        for (int i = 0; i < countSlot; i++)
            result.Add(_launcherSlotThisLevel[firstEmpty + i]);

        return result;
    }

    /// <summary>
    /// Add LauncherBaseMono vào danh sách để bắn.
    /// Logic: xác định group kết nối → check tất cả ở đầu cột → sort theo cột nhỏ → lớn → gán slot trái → phải
    /// </summary>
    private void OnLauncherClickedMethos(LauncherBaseMono launcher)
    {
        // 1. Xác định group: dùng LaunchersConnect nếu có, không thì chỉ mình nó
        List<LauncherBaseMono> group;
        if (launcher is LauncherNormalMono normalMono && normalMono.LaunchersConnect != null)
        {
            group = new List<LauncherBaseMono>(normalMono.LaunchersConnect);
        }
        else
        {
            group = new List<LauncherBaseMono> { launcher };
        }

        // 2. Check tất cả trong group phải ở đầu cột (IsAtTopColumn)
        foreach (var l in group)
        {
            if (!l.IsAtTopColumn())
            {
                Debug.Log($"[QueueController] {l.name} (col {l.ColumnIndex}) chưa ở đầu cột!");
                return;
            }
        }

        // 3. Check đủ slot trống
        if (GetCountSlotEmpty() < group.Count)
        {
            Debug.Log($"[QueueController] Hàng đợi không đủ chỗ! Cần {group.Count}, còn {GetCountSlotEmpty()}");
            return;
        }

        // 4. Sort theo ColumnIndex nhỏ → lớn
        group.Sort((a, b) => a.ColumnIndex.CompareTo(b.ColumnIndex));

        // 5. Lấy slot đã dồn sẵn, gán từ trái qua phải
        List<SlotLauncherMono> slots = GetSlotAndSort(group.Count);
        if (slots == null || slots.Count == 0)
        {
            Debug.Log("[QueueController] Click temporarily ignored because queue is sorting and a launcher is transitioning.");
            return;
        }
        for (int i = 0; i < group.Count; i++)
        {
            var launcherBase = group[i];
            SlotLauncherMono slot = slots[i];
            if (launcherBase is LauncherNormalMono normal)
            {
                if (normal == null || slot == null)
                    return;

                ResigneLauncherSlot(slot);
                int colIndex = normal.ColumnIndex;
                if (colIndex < 0 || colIndex >= _spawnedHolesTransform.Count)
                {
                    Debug.LogError($"[QueueController] Invalid column index {colIndex} or hole not spawned!");
                    return;
                }

                Transform targetHole = _spawnedHolesTransform[colIndex];
                if (targetHole == null) return;

                // Bỏ súng khỏi cột dọc để tránh bấm lại
                normal.RemoveLauncherAtVertical();
                normal.SetupSlotLauncher(slot);

                RegisterTransitioningLauncher(normal);

                // Gọi logic di chuyển trong LauncherNormalMono
                normal.PlayJumpIntoHoleAndThenToSlot(
                    _timeJumpToHole,
                    _timeZPullAway,
                    _timeRiseInSlot,
                    _zPullAwayDistance,
                    _zRiseStartDistance,
                    targetHole.position,
                    slot,
                    onComplete: () =>
                    {
                        UnregisterTransitioningLauncher(normal);
                        if (!_isLevelActive) return; // Bảo vệ: level đã bị hủy

                        // Parent launcher vào slot và bật visual bắn
                        normal.SetupSlotLauncher(slot);
                        normal.transform.SetParent(slot.transform, true);
                        normal.transform.localPosition = LauncherBaseMono.LocalPositionInSlot;
                        normal.transform.localRotation = Quaternion.identity;
                        normal.SetupVisualNormal(true);

                        launcherBase.ChangeLayerRenderer(LayerNameGamePlay.LauncherInSlot);
                        AssignLauncher(slot, launcherBase);
                        normal.AddACShootPiece();
                        if (_isLevelActive)
                            StartCoroutine(MatchLauncherNormal());
                    }) /* .Forget() removed */ ;
            }
        }
    }

    #endregion

    #region BOOSTER SHOOTER PICKER

    /// <summary>
    /// Booster ShooterPicker: cho phép chọn launcher bất kỳ (không cần ở đầu cột).
    /// Kiểm tra đủ slot trống → move launcher lên slot → bắt đầu bắn.
    /// </summary>
    #endregion

    #region BOOOSTER BONNUS SLOT

    public bool CanUseBoosterBonusSlot()
    {
        return GetCountSlotEmpty() < _launcherSlotThisLevel.Count;
    }

    #endregion

    #region BOOSTER SHOOTER SHUFFLE

    public IEnumerator ShuffleShooterForBoosterAsync()
    {
        var allVerticals = LevelSystem.Instance.LauncherController.GetVerticalLaunchers();
        if (allVerticals == null || allVerticals.Count == 0) yield break;

        // Helper to check if a launcher is shuffleable (is Normal, is not connected, and is not hidden)
        bool IsShuffleable(LauncherBaseMono launcher)
        {
            return launcher.GetPieceLauncherMode() == LauncherMode.Normal &&
                   (launcher.GetConnectedReferencesIDs() == null || launcher.GetConnectedReferencesIDs().Count == 0) &&
                   !launcher.GetHidden();
        }

        // 1. Gather available colors and find target color from shuffleable launchers
        HashSet<CubeShooterColor> availableColors = new HashSet<CubeShooterColor>();
        foreach (var vertical in allVerticals)
        {
            foreach (var launcher in vertical.LauncherBaseMonos)
            {
                if (IsShuffleable(launcher))
                {
                    availableColors.Add(launcher.GetColorCodeIndex0());
                }
            }
        }

        if (availableColors.Count == 0) yield break;

        List<CubeShooterColor> visibleColors = new List<CubeShooterColor>();
        foreach (var color in availableColors)
        {
            if (LevelSystem.Instance.Model3DController.IsVisiblePiecesByColor(color))
            {
                visibleColors.Add(color);
            }
        }

        CubeShooterColor targetColor;
        if (visibleColors.Count > 0)
        {
            targetColor = visibleColors[UnityEngine.Random.Range(0, visibleColors.Count)];
        }
        else
        {
            List<CubeShooterColor> listColors = new List<CubeShooterColor>(availableColors);
            targetColor = listColors[UnityEngine.Random.Range(0, listColors.Count)];
        }

        // 2. Perform the constrained shuffle
        // Collect global list of shuffleable (free normal) shooters
        List<LauncherBaseMono> globalFreeShooters = new List<LauncherBaseMono>();
        foreach (var vertical in allVerticals)
        {
            foreach (var launcher in vertical.LauncherBaseMonos)
            {
                if (IsShuffleable(launcher))
                {
                    globalFreeShooters.Add(launcher);
                }
            }
        }

        // Shuffle global free list
        for (int i = 0; i < globalFreeShooters.Count; i++)
        {
            int tempIdx = UnityEngine.Random.Range(i, globalFreeShooters.Count);
            var temp = globalFreeShooters[i];
            globalFreeShooters[i] = globalFreeShooters[tempIdx];
            globalFreeShooters[tempIdx] = temp;
        }

        // For each vertical, rebuild the shooter list
        // keeping all fixed launchers (non-normal or connected normal) at their original indices
        int freePtr = 0;
        foreach (var vertical in allVerticals)
        {
            List<bool> isShuffleableSlot = new List<bool>();

            foreach (var launcher in vertical.LauncherBaseMonos)
            {
                isShuffleableSlot.Add(IsShuffleable(launcher));
            }

            // Reconstruct the full list of launchers for this vertical
            List<LauncherBaseMono> finalVerticalList = new List<LauncherBaseMono>();
            int totalIdx = 0;
            foreach (var launcher in vertical.LauncherBaseMonos)
            {
                if (isShuffleableSlot[totalIdx])
                {
                    if (freePtr < globalFreeShooters.Count)
                    {
                        finalVerticalList.Add(globalFreeShooters[freePtr++]);
                    }
                    else
                    {
                        finalVerticalList.Add(launcher);
                    }
                }
                else
                {
                    // Fixed launcher remains at its original position (both non-normal and connected normal)
                    finalVerticalList.Add(launcher);
                }
                totalIdx++;
            }

            // Assign back to LauncherBaseMonos
            vertical.LauncherBaseMonos = finalVerticalList;
        }

        // 3. Satisfy target color logic if possible
        int emptySlots = GetCountSlotEmpty();
        int targetAction = emptySlots >= 2 ? UnityEngine.Random.Range(1, 3) : 1;
        int targetIndex = targetAction - 1;

        // Check if there is already a targetColor at targetIndex in some vertical (must be shuffleable)
        bool targetColorSatisfied = false;
        foreach (var vertical in allVerticals)
        {
            if (vertical.LauncherBaseMonos.Count > targetIndex)
            {
                var launcher = vertical.LauncherBaseMonos[targetIndex];
                if (IsShuffleable(launcher) && launcher.GetColorCodeIndex0() == targetColor)
                {
                    targetColorSatisfied = true;
                    break;
                }
            }
        }

        if (!targetColorSatisfied)
        {
            // Find a shooter of targetColor and a column/index to swap with
            // Swap between S and vertical[targetIndex] is valid if:
            // - Both S and vertical[targetIndex] are shuffleable launchers.

            bool foundSwap = false;
            foreach (var verticalS in allVerticals)
            {
                for (int idxS = 0; idxS < verticalS.LauncherBaseMonos.Count; idxS++)
                {
                    var S = verticalS.LauncherBaseMonos[idxS];
                    if (!IsShuffleable(S)) continue;
                    if (S.GetColorCodeIndex0() != targetColor) continue;

                    // Now check potential destination columns
                    foreach (var destVertical in allVerticals)
                    {
                        if (destVertical.LauncherBaseMonos.Count <= targetIndex) continue;

                        var destShooter = destVertical.LauncherBaseMonos[targetIndex];
                        if (!IsShuffleable(destShooter)) continue;

                        // Swap them!
                        verticalS.LauncherBaseMonos[idxS] = destShooter;
                        destVertical.LauncherBaseMonos[targetIndex] = S;
                        foundSwap = true;
                        break;
                    }

                    if (foundSwap) break;
                }
                if (foundSwap) break;
            }
        }

        // 4. Finalize assignments and play animations
        float spacing = ConfigHolder.Instance.LauncherConfigSo.SpacingLauncher;
        float animDuration = ConfigHolder.Instance.LauncherConfigSo.TimeMoveLauncherNormal;
        if (animDuration <= 0) animDuration = 0.3f;

        List<IEnumerator> animTasks = new List<IEnumerator>();

        foreach (var vertical in allVerticals)
        {
            for (int i = 0; i < vertical.LauncherBaseMonos.Count; i++)
            {
                var shooter = vertical.LauncherBaseMonos[i];
                if (i == 0)
                {
                    shooter.OnBecomeTop();
                }
            }
            animTasks.Add(vertical.ArrangeVerticalTopCenterAnimAsync(spacing, animDuration));
        }

        yield return WaitAll(animTasks);
    }

    #endregion

    #region REVICE PRELOSE
    /// <summary>
    /// Lấy danh sách launcher cần giải phóng khỏi slot.
    /// Ưu tiên group có số lượng == countOpenUp trước.
    /// Nếu không có group vừa đủ, cộng dồn từ trái qua phải và bỏ qua group vượt quá phần còn lại.
    /// </summary>
    private List<LauncherBaseMono> GetLaunchersToRemoveForPrelose(int countOpenUp)
    {
        List<LauncherBaseMono> result = new List<LauncherBaseMono>();
        List<List<LauncherBaseMono>> groups = new List<List<LauncherBaseMono>>();
        HashSet<LauncherBaseMono> processedLaunchers = new HashSet<LauncherBaseMono>();

        for (int i = 0; i < _launcherSlotThisLevel.Count; i++)
        {
            var slot = _launcherSlotThisLevel[i];
            if (slot == null || slot.IsEmpty || slot.CurrentLauncher == null) continue;

            var launcher = slot.CurrentLauncher;
            if (processedLaunchers.Contains(launcher)) continue;

            List<LauncherBaseMono> group = new List<LauncherBaseMono>();
            if (launcher is LauncherNormalMono normalMono && normalMono.LaunchersConnect != null && normalMono.LaunchersConnect.Count > 0)
            {
                foreach (var connect in normalMono.LaunchersConnect)
                {
                    if (connect == null) continue;
                    group.Add(connect);
                    processedLaunchers.Add(connect);
                }
            }
            else
            {
                group.Add(launcher);
                processedLaunchers.Add(launcher);
            }

            if (group.Count <= 0 || group.Count > countOpenUp) continue;

            if (group.Count == countOpenUp)
            {
                result.AddRange(group);
                return result;
            }

            groups.Add(group);
        }

        int currentCount = 0;
        foreach (var group in groups)
        {
            if (currentCount + group.Count > countOpenUp) continue;

            result.AddRange(group);
            currentCount += group.Count;

            if (currentCount >= countOpenUp) break;
        }

        return result;
    }

    public IEnumerator OnRevecePrelose()
    {
        UIFullScreenBlocker.Instance.Lock(10);
        // 1. Lấy số lượng slot cần mở rộng từ cấu hình EndGameConfigSO
        int countOpenUp = ConfigHolder.Instance.EndGameConfigSo.CountOpenUpSlot;
        if (countOpenUp <= 0) yield break;
        // 2. Duyệt từ trái qua phải để gom súng cần giải phóng, không vượt quá countOpenUp
        List<LauncherBaseMono> launchersToRemove = GetLaunchersToRemoveForPrelose(countOpenUp);
        if (launchersToRemove.Count <= 0) yield break;

        // 3. Tiến hành giải phóng các launcher đã gom khỏi slot và di chuyển chúng sang booster slots
        List<IEnumerator> tasks = new List<IEnumerator>();
        HashSet<LauncherBaseMono> processedLaunchers = new HashSet<LauncherBaseMono>();

        foreach (var launcher in launchersToRemove)
        {
            if (launcher == null || processedLaunchers.Contains(launcher)) continue;

            // Tìm slot hiện tại đang chứa launcher này
            SlotLauncherMono targetSlot = null;
            foreach (var slot in _launcherSlotThisLevel)
                if (slot != null && !slot.IsEmpty && slot.CurrentLauncher == launcher)
                {
                    targetSlot = slot;
                    break;
                }

            if (targetSlot != null)
            {
                // Thêm toàn bộ nhóm liên kết vào processed để tránh gọi trùng lặp
                if (launcher is LauncherNormalMono normalMono && normalMono.LaunchersConnect != null && normalMono.LaunchersConnect.Count > 0)
                {
                    foreach (var connect in normalMono.LaunchersConnect)
                        if (connect != null) processedLaunchers.Add(connect);
                }
                else
                    processedLaunchers.Add(launcher);

                // Booster slot flow is not used in this playable branch.
            }
        }

        if (tasks.Count > 0)
            yield return WaitAll(tasks);
        Model3DController.CallBackOnRevice?.Invoke();
        UIFullScreenBlocker.Instance.Unlock(10);
    }

    #endregion

    #region MECHANIC

    /// <summary>
    /// Check xem level hiện tại đã unlock tính năng Merge 3 chưa
    /// </summary>
    private bool IsMatch3UnlockedAtCurrentLevel()
    {
        int currentLevelDisplay = LevelSystem.GetCurrentLevelDisplay();
        return currentLevelDisplay >= LEVEL_UNLOCK_MATCH3;
    }

    /// <summary>
    /// Check xem level hiện tại đã unlock tính năng Auto-Fill chưa
    /// </summary>
    private bool IsAutoFillUnlockedAtCurrentLevel()
    {
        if (!AutoFillSlotsEnabled)
            return false;

        int currentLevelDisplay = LevelSystem.GetCurrentLevelDisplay();
        return currentLevelDisplay >= LEVEL_UNLOCK_AUTO_FILL;
    }

    public void SetAutoFillSlotsEnabled(bool enabled)
    {
        AutoFillSlotsEnabled = enabled;
    }
    private IEnumerator MatchLauncherNormal()
    {
        if (!IsMatch3UnlockedAtCurrentLevel())
            yield break;

        if (_isMatchingLaunchers) yield break;
        _isMatchingLaunchers = true;

        try
        {
            bool hasMatch = true;

            while (hasMatch)
            {
                hasMatch = false;
                System.Collections.Generic.List<SlotLauncherMono> matchGroup = null;

                foreach (var pair in _dicColorAndSlotLauncherActive)
                {
                    if (pair.Value != null)
                    {
                        // Tự động dọn dẹp các slot trống hoặc bị sai lệch khỏi dictionary trước khi đếm
                        pair.Value.RemoveAll(s => s == null || s.IsEmpty || s.CurrentLauncher == null || s.CurrentLauncher.GetColorCodeIndex0() != pair.Key);

                        // Xóa các phần tử trùng lặp bằng for loop để fix lỗi Contains trên Luna
                        var distinctList = new System.Collections.Generic.List<SlotLauncherMono>();
                        for (int i = 0; i < pair.Value.Count; i++)
                        {
                            var s = pair.Value[i];
                            bool found = false;
                            for (int j = 0; j < distinctList.Count; j++)
                            {
                                if (distinctList[j] == s) { found = true; break; }
                            }
                            if (!found) distinctList.Add(s);
                        }
                        pair.Value.Clear();
                        pair.Value.AddRange(distinctList);

                        if (pair.Value.Count >= _countMechnicMath3Launcher)
                        {
                            matchGroup = pair.Value;
                            break;
                        }
                    }
                }

                if (matchGroup != null)
                {
                    hasMatch = true;
                    yield return MatchLauncherNormalLogic(matchGroup);
                }
            }
        }
        finally
        {
            _isMatchingMatch3CleanUp();
        }
    }

    private void _isMatchingMatch3CleanUp()
    {
        _isMatchingLaunchers = false;
    }

    /// <summary>
    /// hợp thể 3 launcher cùng màu cộng số đạn vào, hợp thể vào cái ở giữa
    /// </summary>
    private IEnumerator MatchLauncherNormalLogic(List<SlotLauncherMono> listSlotLauncherSameColor)
    {
        List<SlotLauncherMono> match3 = new List<SlotLauncherMono>();
        for (int i = 0; i < _countMechnicMath3Launcher; i++) match3.Add(listSlotLauncherSameColor[i]);

        match3.Sort((a, b) => a.Index.CompareTo(b.Index));

        LauncherNormalMono startLauncher = match3[0].CurrentLauncher as LauncherNormalMono;
        LauncherNormalMono middleLauncher = match3[1].CurrentLauncher as LauncherNormalMono;
        LauncherNormalMono endLauncher = match3[2].CurrentLauncher as LauncherNormalMono;

        if (startLauncher == null || middleLauncher == null || endLauncher == null)
        {
            listSlotLauncherSameColor.RemoveAll(x => x == match3[0] || x == match3[1] || x == match3[2]);
            yield break;
        }

        listSlotLauncherSameColor.RemoveAll(x => x == match3[0] || x == match3[1] || x == match3[2]);

        startLauncher.SetCanShoot(false, true);
        middleLauncher.SetCanShoot(false, true);
        endLauncher.SetCanShoot(false, true);

        _activeMergeCount += 2;
        bool foundStart = false; bool foundEnd = false;
        for (int i = 0; i < _activeMergeLaunchers.Count; i++)
        {
            if (_activeMergeLaunchers[i] == startLauncher) foundStart = true;
            if (_activeMergeLaunchers[i] == endLauncher) foundEnd = true;
        }
        if (!foundStart) _activeMergeLaunchers.Add(startLauncher);
        if (!foundEnd) _activeMergeLaunchers.Add(endLauncher);

        ClearLauncherSlot(match3[0]);
        ClearLauncherSlot(match3[2]);

        int completedCount = 0;

        startLauncher.StartCoroutine(startLauncher.MoveToPosition(
            null,
            middleLauncher.TF.position,
            0.3f,
            () =>
            {
                _activeMergeLaunchers.RemoveAll(x => x == startLauncher);
                if (middleLauncher != null && middleLauncher.ColorAndBullet != null && startLauncher != null && startLauncher.ColorAndBullet != null)
                {
                    middleLauncher.AddBulletAmount(startLauncher.ColorAndBullet.Amount);
                }
                if (startLauncher != null) startLauncher.OnDespawn();
                _activeMergeCount--;
                completedCount++;
            }));

        endLauncher.StartCoroutine(endLauncher.MoveToPosition(
            null,
            middleLauncher.TF.position,
            0.3f,
            () =>
            {
                _activeMergeLaunchers.RemoveAll(x => x == endLauncher);
                if (middleLauncher != null && middleLauncher.ColorAndBullet != null && endLauncher != null && endLauncher.ColorAndBullet != null)
                {
                    middleLauncher.AddBulletAmount(endLauncher.ColorAndBullet.Amount);
                }
                if (endLauncher != null) endLauncher.OnDespawn();
                _activeMergeCount--;
                completedCount++;
            }));

        float timeout = 1.0f;
        while (completedCount < 2 && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        SoundManager.Instance.PlayOneShot(AudioClipName.Bonus_Slot_Arrive);

        if (middleLauncher != null && middleLauncher.gameObject.activeInHierarchy)
        {
            middleLauncher.AddACShootPiece();

            CubeShooterColor colorCode = middleLauncher.GetColorCodeIndex0();
            if (!_dicColorAndSlotLauncherActive.ContainsKey(colorCode))
                _dicColorAndSlotLauncherActive[colorCode] = new List<SlotLauncherMono>();

            SlotLauncherMono currentSlot = middleLauncher.GetSlotLauncherMono;
            if (currentSlot != null)
            {
                bool found = false;
                var list = _dicColorAndSlotLauncherActive[colorCode];
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] == currentSlot) { found = true; break; }
                }
                if (!found) list.Add(currentSlot);
            }
        }
        GameEventBus.Match3Suscess?.Invoke();
    }

    #endregion

    #region ACTION SLOT LAUNCHER

    public void AssignLauncher(SlotLauncherMono slotLauncherMono, LauncherBaseMono launcherBaseMono)
    {
        GameEventBus.AssignLauncher?.Invoke();
        slotLauncherMono.AssignLauncher(launcherBaseMono);
        LevelSystem.Instance.Model3DController.ResetShootState(true);

        if (launcherBaseMono.GetCountColorAndBullet() == 0)
            return;

        if (launcherBaseMono is LauncherNormalMono normalMono && normalMono.LaunchersConnect != null && normalMono.LaunchersConnect.Count > 1)
            return;

        CubeShooterColor colorCode = launcherBaseMono.GetColorCodeIndex0();
        if (!_dicColorAndSlotLauncherActive.ContainsKey(colorCode))
            _dicColorAndSlotLauncherActive[colorCode] = new List<SlotLauncherMono>();

        bool found = false;
        var list = _dicColorAndSlotLauncherActive[colorCode];
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == slotLauncherMono) { found = true; break; }
        }
        if (!found) list.Add(slotLauncherMono);
    }

    /// <summary>
    /// Kiểm tra xem có launcher nào ở đầu cột (index 0) có thể đưa lên slot trống không.
    /// Duyệt từ trái qua phải (theo ColumnIndex).
    /// - Nếu là launcher có dây nối: tất cả trong group phải ở đầu cột + đủ slot trống.
    /// - Nếu không có dây nối: chỉ cần launcher đó ở đầu cột + có slot trống.
    /// </summary>
    public bool CanMoveAnyTopLauncherToSlot()
    {
        int emptySlots = GetCountSlotEmpty();
        if (emptySlots <= 0) return false;

        // Duyệt các cột từ trái qua phải (theo ColumnIndex)
        //TODO: tối ưu
        var verticals = LevelSystem.Instance.LauncherController.GetVerticalLaunchers();
        verticals.Sort((a, b) => a.ColumnIndex.CompareTo(b.ColumnIndex));

        foreach (var vertical in verticals)
        {
            foreach (var launcher in vertical.LauncherBaseMonos)
            {
                if (!launcher.IsAtTopColumn()) continue;

                // Xác định group
                List<LauncherBaseMono> group;
                if (launcher is LauncherNormalMono normalMono && normalMono.LaunchersConnect != null && normalMono.LaunchersConnect.Count > 1)
                {
                    group = new List<LauncherBaseMono>(normalMono.LaunchersConnect);
                }
                else
                {
                    group = new List<LauncherBaseMono> { launcher };
                }

                // Check tất cả trong group có ở đầu cột không
                bool allAtTop = true;
                foreach (var l in group)
                {
                    if (!l.IsAtTopColumn())
                    {
                        allAtTop = false;
                        break;
                    }
                }
                if (!allAtTop) continue;

                // Check đủ slot trống
                if (group.Count <= emptySlots)
                    return true; // Tìm được launcher thỏa mãn

                // Nếu group này không đủ slot, continue kiểm tra launcher khác
            }
        }

        return false;
    }

    /// <summary>
    /// Tự động đưa launcher ở đầu cột lên slot trống.
    /// Úu tiên group có dây nối (connected group) trước.
    /// Gọi sau khi: despawn launcher, hợp thể 3 launcher.
    /// Thực hiện tuần tự với delay giữa mỗi launcher bay lên.
    /// </summary>
    public IEnumerator TryAutoFillEmptySlots()
    {
        // Chỉ chạy tính năng auto-fill từ level LEVEL_UNLOCK_AUTO_FILL trở đi
        if (!IsAutoFillUnlockedAtCurrentLevel())
            yield break;

        // Guard: tránh re-entry khi đang auto-fill
        if (_isPlayDonelauncher)
            yield break;

        int countLauncherUnsafe = LevelSystem.Instance.LauncherController.GetCountUnusedLauncher();
        if (countLauncherUnsafe <= 0)
        {
            _isPlayDonelauncher = true;
            yield break;
        }
        if (_isAutoFillingSlots) yield break;

        int emptySlots = GetCountSlotEmpty();
        if (emptySlots <= 0) yield break;

        // Check xem tổng launcher chưa lên slot có <= slot trống không
        if (countLauncherUnsafe > emptySlots)
            yield break;

        UIFullScreenBlocker.Instance.Lock(36);

        _isAutoFillingSlots = true;

        try
        {
            while (!false)
            {
                int currentEmpty = GetCountSlotEmpty();
                if (currentEmpty <= 0) break;

                // Snapshot danh sách top launcher từ mỗi cột tại thời điểm hiện tại
                var topLaunchers = new List<LauncherBaseMono>();
                foreach (VerticalLauncherMono vertical in LevelSystem.Instance.LauncherController.GetVerticalLaunchers())
                {
                    foreach (LauncherBaseMono launcher in vertical.LauncherBaseMonos)
                    {
                        if (launcher.IsAtTopColumn())
                        {
                            topLaunchers.Add(launcher);
                            break; // chỉ lấy launcher đầu tiên của cột
                        }
                    }
                }

                if (topLaunchers.Count == 0) break;

                // Ưu tiên launcher có connect group trước
                topLaunchers.Sort((a, b) =>
                {
                    bool aHasConnect = a is LauncherNormalMono normalA && normalA.LaunchersConnect != null && normalA.LaunchersConnect.Count > 1;
                    bool bHasConnect = b is LauncherNormalMono normalB && normalB.LaunchersConnect != null && normalB.LaunchersConnect.Count > 1;
                    if (aHasConnect && !bHasConnect) return -1;
                    if (!aHasConnect && bHasConnect) return 1;
                    return a.ColumnIndex.CompareTo(b.ColumnIndex);
                });

                LauncherBaseMono targetLauncher = null;
                List<LauncherBaseMono> targetGroup = null;

                foreach (var launcher in topLaunchers)
                {
                    // Nếu súng bị hidden, hóa giải trước
                    if (launcher.GetHidden())
                    {
                        launcher.OnBecomeTop(); // Hóa giải hidden
                    }

                    // Súng phải sẵn sàng để chọn (không bị đóng băng)
                    if (!launcher.CanSelect) continue;

                    List<LauncherBaseMono> group;
                    if (launcher is LauncherNormalMono normalMono && normalMono.LaunchersConnect != null)
                    {
                        group = new List<LauncherBaseMono>(normalMono.LaunchersConnect);
                    }
                    else
                    {
                        group = new List<LauncherBaseMono> { launcher };
                    }

                    // Check tất cả trong group còn ở đầu cột không và có thể chọn không
                    bool allAtTopAndSelectable = true;
                    foreach (var l in group)
                    {
                        if (!l.IsAtTopColumn() || !l.CanSelect)
                        {
                            allAtTopAndSelectable = false;
                            break;
                        }
                    }
                    if (!allAtTopAndSelectable) continue;

                    if (group.Count <= currentEmpty)
                    {
                        targetLauncher = launcher;
                        targetGroup = group;
                        break;
                    }
                }

                if (targetGroup == null) break; // Không tìm thấy group nào phù hợp để đưa lên tiếp

                // Di chuyển group lên slot — tuần tự từng launcher
                targetGroup.Sort((a, b) => a.ColumnIndex.CompareTo(b.ColumnIndex));
                List<SlotLauncherMono> slots = GetSlotAndSort(targetGroup.Count);
                if (slots == null || slots.Count == 0) break;

                for (int i = 0; i < targetGroup.Count; i++)
                {
                    var l = targetGroup[i];
                    var slot = slots[i];
                    if (l is LauncherNormalMono normal)
                    {
                        if (normal == null || slot == null)
                            continue;

                        ResigneLauncherSlot(slot);
                        int colIndex = normal.ColumnIndex;
                        if (colIndex < 0 || colIndex >= _spawnedHolesTransform.Count)
                        {
                            Debug.LogError($"[QueueController] Invalid column index {colIndex} or hole not spawned!");
                            continue;
                        }

                        Transform targetHole = _spawnedHolesTransform[colIndex];
                        if (targetHole == null) continue;

                        // Bỏ súng khỏi cột không sắp xếp - bay lên luôn
                        normal.RemoveLauncherDirectWithoutArrange();
                        normal.SetupSlotLauncher(slot);

                        RegisterTransitioningLauncher(normal);

                        // Gọi logic di chuyển trong LauncherNormalMono
                        normal.PlayJumpIntoHoleAndThenToSlot(
                            _timeJumpToHole,
                            _timeZPullAway,
                            _timeRiseInSlot,
                            _zPullAwayDistance,
                            _zRiseStartDistance,
                            targetHole.position,
                            slot,
                            onComplete: () =>
                            {
                                UnregisterTransitioningLauncher(normal);

                                // Parent launcher vào slot và bật visual shooter
                                normal.SetupSlotLauncher(slot);
                                normal.transform.SetParent(slot.transform, true);
                                normal.transform.localPosition = LauncherBaseMono.LocalPositionInSlot;
                                normal.transform.localRotation = Quaternion.identity;
                                normal.SetupVisualNormal(true);

                                l.ChangeLayerRenderer(LayerNameGamePlay.LauncherInSlot);
                                AssignLauncher(slot, normal);
                                normal.AddACShootPiece();

                                if (_isLevelActive)
                                {
                                    StartCoroutine(MatchLauncherNormal());
                                }
                            }) /* .Forget() removed */ ;
                    }

                    // Delay giữa các súng bay lên (200ms)
                    yield return new WaitForSeconds(200 / 1000f);
                }
            }

            // Kiểm tra xem thực sự tất cả launcher trên các cột đã lên slot hết chưa
            int totalUnused = 0;
            foreach (var vertical in LevelSystem.Instance.LauncherController.GetVerticalLaunchers())
            {
                totalUnused += vertical.LauncherBaseMonos.Count;
            }

            if (totalUnused == 0)
            {
                LevelSystem.Instance.ChangeSpeedGame(true);
            }
        }

        finally
        {
            _isAutoFillingSlots = false;
        }
    }

    private bool _isPendingAutoFill = false;

    public bool HasLauncherTransitioning()
    {
        if (_activeMergeCount > 0) return true;
        if (_activeTransitioningLaunchers.Count > 0) return true;
        if (_launcherSlotThisLevel == null) return false;
        foreach (var slot in _launcherSlotThisLevel)
        {
            if (slot != null && slot.CurrentLauncher == null && !slot.IsEmpty)
            {
                return true;
            }
        }
        return false;
    }

    private void TriggerAutoFillWithDebounce()
    {
        if (_isPendingAutoFill) return;
        if (!_isLevelActive) return;
        _isPendingAutoFill = true;
        StartCoroutine(TriggerAutoFillDelayed());
    }

    private IEnumerator TriggerAutoFillDelayed()
    {
        // Chờ đến cuối khung hình để tất cả các slot trong group được dọn dẹp xong
        yield return null;

        // Đợi cho đến khi không còn súng nào đang bay dở dang
        while (HasLauncherTransitioning())
        {
            if (false) yield break;
            yield return null;
        }

        if (false) yield break;

        _isPendingAutoFill = false;
        yield return TryAutoFillEmptySlots();
    }

    public void ClearLauncherSlot(SlotLauncherMono slotLauncherMono)
    {
        if (slotLauncherMono == null || slotLauncherMono.CurrentLauncher == null)
            return;

        CubeShooterColor colorCode = slotLauncherMono.CurrentLauncher.GetColorCodeIndex0();
        if (_dicColorAndSlotLauncherActive.ContainsKey(colorCode))
        {
            _dicColorAndSlotLauncherActive[colorCode].RemoveAll(x => x == slotLauncherMono);
        }

        if (slotLauncherMono.CurrentLauncher != null)
            slotLauncherMono.CurrentLauncher.transform.SetParent(null);

        slotLauncherMono.ClearSlot();

        // Trì hoãn auto-fill để gộp các cuộc gọi từ cùng một group despawn và chờ súng bay xong
        TriggerAutoFillWithDebounce();
    }

    #endregion

    #region CALL AC SHOOT

    public void BlockLauncherShootAC(bool blockLauncherShootAC)
    {
        _blockLauncherShoot = blockLauncherShootAC;
    }

    private float _updateTimer;

    public void StartUpdateLoop()
    {
        // tránh start nhiều lần
        if (_isLevelActive) return;

        _isLevelActive = true;
        _updateTimer = 0f;
    }

    private void Update()
    {
        if (!_isLevelActive) return;

        // Centralized update for all launchers' rotations (replaces LateUpdate to reduce overhead)
        LauncherNormalMono.UpdateAllLaunchersRotation();

        if (_fireRateLauncher <= 0)
            _fireRateLauncher = ConfigHolder.Instance.SlotLauncherConfigSo.FireRateLauncherDeffault;

        _updateTimer += Time.deltaTime;
        float waitTime = _fireRateLauncher / 1000f;

        if (_updateTimer >= waitTime)
        {
            _updateTimer = 0f;

            // Snapshot để tránh lỗi Collection was modified khi ShootPiece() gọi Remove() trong lúc duyệt
            // Dùng _shootSnapshot cache (field) thay vì new List<> mỗi tick → zero GC alloc
            if (!_blockLauncherShoot)
            {
                _shootSnapshot.Clear();
                _shootSnapshot.AddRange(GameEventBus.ACLauncherShoot);
                foreach (var VARIABLE in _shootSnapshot)
                {
                    if (VARIABLE != null)
                        VARIABLE.ShootPiece();
                }
            }

            // Auto-Heal Match3: Tự động check và kích hoạt merge nếu bị sót do race-condition
            if (!_isMatchingLaunchers && IsMatch3UnlockedAtCurrentLevel())
            {
                bool needMatch = false;
                foreach (var pair in _dicColorAndSlotLauncherActive)
                {
                    var list = pair.Value;
                    if (list != null)
                    {
                        // Loại bỏ Lambda RemoveAll để tránh GC Alloc
                        for (int i = list.Count - 1; i >= 0; i--)
                        {
                            var s = list[i];
                            if (s == null || s.IsEmpty || s.CurrentLauncher == null || s.CurrentLauncher.GetColorCodeIndex0() != pair.Key)
                            {
                                list.RemoveAt(i);
                            }
                        }

                        if (list.Count >= _countMechnicMath3Launcher)
                        {
                            needMatch = true;
                            break;
                        }
                    }
                }
                if (needMatch)
                {
                    StartCoroutine(MatchLauncherNormal());
                }
            }
        }
    }

    public void StopUpdateLoop()
    {
        if (!_isLevelActive) return;
        _isLevelActive = false;
    }

    #endregion

    #region GENERATOR LEVEL

    /// <summary>
    /// = awake
    /// </summary>
    /// <returns></returns>
    public IEnumerator OnInitPoolAsync()
    {
        _slotLauncherMonoPrefab = ConfigHolder.Instance.PrefabsDataConfigSO.SlotLauncherMonoPrefab;
        _fireRateLauncher = ConfigHolder.Instance.SlotLauncherConfigSo.FireRateLauncherDeffault;
        _countMechnicMath3Launcher = ConfigHolder.Instance.SlotLauncherConfigSo.CountMechnicMathLauncher;
        _countSlotLauncherDeffault = ConfigHolder.Instance.SlotLauncherConfigSo.CountSlotlauncherDeffault;
        _timeSortLauncherNormal = ConfigHolder.Instance.LauncherConfigSo.TimeSortLauncherNormal;

        // Đọc cấu hình khoảng cách từ SlotLauncherConfigSo
        _spacingHoles = ConfigHolder.Instance.SlotLauncherConfigSo.SpacingHoles;
        _spacingLauncherSlot = ConfigHolder.Instance.SlotLauncherConfigSo.Spacing;

        // Đọc cấu hình thời gian từ LauncherConfigSoS
        _zPullAwayDistance = ConfigHolder.Instance.LauncherConfigSo.ZPullAwayDistance;
        _zRiseStartDistance = ConfigHolder.Instance.LauncherConfigSo.ZRiseStartDistance;
        _timeJumpToHole = ConfigHolder.Instance.LauncherConfigSo.TimeJumpToHole;
        _timeZPullAway = ConfigHolder.Instance.LauncherConfigSo.TimeZPullAway;
        _timeRiseInSlot = ConfigHolder.Instance.LauncherConfigSo.TimeRiseInSlot;

        PoolHolder.Instance.PreWarm(_slotLauncherMonoPrefab, _countSlotLauncherDeffault, _parentGenSlotlauncher);
        yield break;
    }

    public IEnumerator OnLoadLevel(RoundDataBytes newRoundData)
    {
        for (int i = 0; i < _countSlotLauncherDeffault; i++)
        {
            SlotLauncherMono item = PoolHolder.Instance.Get(_slotLauncherMonoPrefab, _parentGenSlotlauncher) as SlotLauncherMono;
            if (item == null)
                Debug.LogError("Bug null slot launcher mono");

            // Xác định kiểu vị trí: 2 slot đầu = Left, 2 slot cuối = Right, những cái giữa = Center
            ModelSide side = ModelSide.Center;
            if (i == 0)
                side = ModelSide.Left;
            else if (i == _countSlotLauncherDeffault - 1)
                side = ModelSide.Right;

            item?.OnInit(i, side);
            _launcherSlotThisLevel.Add(item);
        }
        OnLoadLevelDone();

        SpawnHoles(newRoundData.ListVerticalLauncherData.VerticalLauncherDatas.Count);

        yield break;
    }

    public void OnClear()
    {
        if (_isLevelActive)
        {
        }
        _dicColorAndSlotLauncherActive.Clear();
        GameEventBus.ACLauncherShoot.Clear();
        foreach (SlotLauncherMono VARIABLE in _launcherSlotThisLevel)
            VARIABLE.OnDespawn(); //returm pool ngay tại đây
        _launcherSlotThisLevel.Clear();
        _activeMergeCount = 0;
        _isMatchingLaunchers = false;

        // Reset các cờ guard chống kẹt kịch bản ở màn mới
        _isAutoFillingSlots = false;
        _isPendingAutoFill = false;
        AutoFillSlotsEnabled = true;
        _blockLauncherShoot = false;
        _isPlayDonelauncher = false;

        // Dọn dẹp các súng đang bay từ cột lên slot
        foreach (var launcher in _activeTransitioningLaunchers)
        {
            if (launcher != null)
            {
                launcher.OnDespawn(); // Hủy di chuyển và trả về pool ngay lập tức
            }
        }
        _activeTransitioningLaunchers.Clear();

        // Dọn dẹp các súng đang bay hợp thể để tránh rò rỉ và lỗi bất đồng bộ
        foreach (var launcher in _activeMergeLaunchers)
        {
            if (launcher != null)
            {
                launcher.OnDespawn();
            }
        }
        _activeMergeLaunchers.Clear();

        StopUpdateLoop();
        ClearSpawnedHoles();
    }

    #endregion

    #region <========================= HOLES MANAGEMENT =========================>
    private void SpawnHoles(int colCount)
    {
        // Xóa các hố cũ trước khi sinh hố mới
        ClearSpawnedHoles();

        // Chọn transform làm tâm sắp xếp (chính là _holeParent)
        Transform centerTransform = _holeParent != null ? _holeParent : transform;
        Vector3 centerPos = centerTransform.position;

        // Sử dụng khoảng cách _spacingHoles
        float spacing = _spacingHoles > 0 ? _spacingHoles : 1.5f;
        float startX = -(colCount - 1) * 0.5f * spacing;

        for (int i = 0; i < colCount; i++)
        {
            Vector3 spawnPos = centerPos;
            spawnPos.x += startX + i * spacing;

            // Xác định kiểu vị trí: 2 hố đầu = Left, 2 hố cuối = Right, những cái giữa = Center
            ModelSide side = ModelSide.Center;
            if (i < 1)
                side = ModelSide.Left;
            else if (i >= colCount - 1)
                side = ModelSide.Right;

            HoleMono holeMono = PoolHolder.Instance.Get(_holePrefab, _holeParent) as HoleMono;
            if (holeMono != null)
            {
                holeMono.OnInit(spawnPos, side);
                _spawnedHolesTransform.Add(holeMono.transform);
                _spawnedHoles.Add(holeMono);
            }
        }
    }

    private void ClearSpawnedHoles()
    {
        foreach (var t in _spawnedHoles)
            t.OnDespawn();
        _spawnedHoles.Clear();
        _spawnedHolesTransform.Clear();
    }

    /// <summary>
    /// ăng kí trước để thằng khác không chọn vào
    /// </summary>
    /// <param name="slot"></param>
    private void ResigneLauncherSlot(SlotLauncherMono slot)
    {
        slot.SetResigneLauncher(true);
    }
    #endregion

    #region TUTORIAL

    public void AddLauncherToSlotForTutorial(int launcherCount)
    {
        // 1. Bỏ hoàn toàn logic cũ, duyệt lấy launcher theo step 2 & 3
        var verticalLaunchers = LevelSystem.Instance.LauncherController.GetVerticalLaunchers();
        List<LauncherBaseMono> selectedLaunchers = new List<LauncherBaseMono>();

        // Duyệt lần lượt theo index từ 0 đến 5 trong từng VerticalLauncherMono
        for (int idx = 0; idx <= 5; idx++)
        {
            if (selectedLaunchers.Count >= launcherCount) break;

            for (int v = 0; v < verticalLaunchers.Count; v++)
            {
                var vertical = verticalLaunchers[v];
                if (idx < vertical.LauncherBaseMonos.Count)
                {
                    var launcher = vertical.LauncherBaseMonos[idx];
                    selectedLaunchers.Add(launcher);
                    if (selectedLaunchers.Count >= launcherCount)
                    {
                        break;
                    }
                }
            }
        }

        // Kiểm tra số lượng slot trống và giới hạn số lượng launcher chọn
        int emptySlots = GetCountSlotEmpty();
        if (selectedLaunchers.Count > emptySlots)
        {
            selectedLaunchers = selectedLaunchers.GetRange(0, emptySlots);
        }

        if (selectedLaunchers.Count == 0) return;

        // 4. Di chuyển launcher lên slot, bỏ qua animation, cập nhật vị trí súng khác (bỏ qua animation)
        bool originalTutorialLevel = LevelSystem.IsTutorialLevel;
        LevelSystem.IsTutorialLevel = true; // Bắt buộc bỏ qua animation khi cập nhật vị trí súng khác

        try
        {
            List<SlotLauncherMono> slots = GetSlotAndSort(selectedLaunchers.Count);
            if (slots != null && slots.Count >= selectedLaunchers.Count)
            {
                for (int i = 0; i < selectedLaunchers.Count; i++)
                {
                    var l = selectedLaunchers[i];
                    var slot = slots[i];
                    if (l is LauncherNormalMono normal)
                    {
                        ResigneLauncherSlot(slot);
                        normal.RemoveLauncherAtVertical(); // Sẽ kích hoạt sắp xếp lại cột không anim do IsTutorialLevel = true
                        normal.SetupSlotLauncher(slot);

                        // Snap vị trí ngay lập tức, bỏ qua animation
                        normal.transform.SetParent(slot != null ? slot.transform : null);
                        normal.transform.localPosition = LauncherBaseMono.LocalPositionInSlot;
                        normal.transform.localRotation = Quaternion.identity;
                        normal.SetupVisualNormal(true);

                        l.ChangeLayerRenderer(LayerNameGamePlay.LauncherInSlot);
                        AssignLauncher(slot, normal);
                        normal.AddACShootPiece();
                    }
                }
            }
        }
        finally
        {
            LevelSystem.IsTutorialLevel = originalTutorialLevel;
        }

        if (_isLevelActive)
            StartCoroutine(MatchLauncherNormal());
    }

    #endregion

    public void ChangeFireRateLauncher(int fireRate)
    {
        _fireRateLauncher = fireRate;
    }

    public void StartBlinkingAllSlot()
    {
        foreach (SlotLauncherMono VARIABLE in _launcherSlotThisLevel)
            VARIABLE.StartBlinking();
    }

    public void StopBlinkingAllSlot()
    {
        foreach (SlotLauncherMono VARIABLE in _launcherSlotThisLevel)
            VARIABLE.StopBlinking();
    }

    public void SetRenderersActive(bool active)
    {
        foreach (var slot in _launcherSlotThisLevel)
        {
            if (slot != null)
            {
                slot.SetRenderersActive(active);
            }
        }
        foreach (var hole in _spawnedHolesTransform)
        {
            if (hole != null)
            {
                var r = hole.GetComponentInChildren<Renderer>(true);
                if (r != null) r.enabled = active;
            }
        }
    }

    public IEnumerator PlayJumpAppearanceAnim(bool show, SlotAppearanceConfigSO config, bool fromLeft)
    {
        if (config == null)
        {
            SetRenderersActive(show);
            yield break;
        }

        SetRenderersActive(true);

        List<IEnumerator> tasks = new List<IEnumerator>();

        // 1. Slots jump from left
        int count = _launcherSlotThisLevel.Count;
        float startX = -(count - 1) * 0.5f * _spacingLauncherSlot;

        for (int i = 0; i < count; i++)
        {
            var slot = _launcherSlotThisLevel[i];
            if (slot == null) continue;

            float delay = i * config.StepDelay;

            Vector3 targetPos = _centerSortLaucherSlot.position;
            targetPos.x += startX + i * _spacingLauncherSlot;

            float offset = -config.JumpOffset; // Always from left for slots
            Vector3 startPos = show ? targetPos + new Vector3(offset, 0, 0) : targetPos;
            Vector3 endPos = show ? targetPos : targetPos + new Vector3(offset, 0, 0);

            tasks.Add(JumpSlot(slot.transform, startPos, endPos, config.JumpHeight, config.JumpDuration, delay));
        }

        // 2. Holes jump from right
        int holeCount = _spawnedHolesTransform.Count;
        float holeSpacing = _spacingHoles > 0 ? _spacingHoles : 1.5f;
        float holeStartX = -(holeCount - 1) * 0.5f * holeSpacing;
        Transform centerTransform = _holeParent != null ? _holeParent : transform;

        for (int i = 0; i < holeCount; i++)
        {
            var hole = _spawnedHolesTransform[i];
            if (hole == null) continue;

            // Reverse delay so they jump from right to left? Or just i * delay?
            float delay = i * config.StepDelay;

            Vector3 targetPos = centerTransform.position;
            targetPos.x += holeStartX + i * holeSpacing;

            float offset = config.JumpOffset; // Always from right for holes
            Vector3 startPos = show ? targetPos + new Vector3(offset, 0, 0) : targetPos;
            Vector3 endPos = show ? targetPos : targetPos + new Vector3(offset, 0, 0);

            tasks.Add(JumpSlot(hole, startPos, endPos, config.JumpHeight, config.JumpDuration, delay));
        }

        yield return WaitAll(tasks);

        if (!show)
        {
            SetRenderersActive(false);
        }
        GamePlayManager.INTRO_FADEIN_FADEOUT = false;
    }

    private IEnumerator JumpSlot(Transform target, Vector3 startPos, Vector3 endPos, float jumpHeight, float duration, float delay)
    {
        if (delay > 0)
        {
            target.position = startPos;
            yield return new WaitForSeconds(delay);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (target == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Vector3 currentPos = Vector3.Lerp(startPos, endPos, t);
            currentPos.y += Mathf.Sin(t * Mathf.PI) * jumpHeight;
            target.position = currentPos;

            yield return null;
        }

        if (target != null)
        {
            target.position = endPos;
        }
    }

    private IEnumerator WaitAll(List<IEnumerator> coroutines)
    {
        if (coroutines == null || coroutines.Count == 0) yield break;

        int totalCount = 0;
        int[] completedCount = new int[1];

        for (int i = 0; i < coroutines.Count; i++)
        {
            if (coroutines[i] != null)
            {
                totalCount++;
                StartCoroutine(RunCoroutineAndTrack(coroutines[i], completedCount));
            }
        }

        while (completedCount[0] < totalCount)
        {
            yield return null;
        }
    }

    private IEnumerator RunCoroutineAndTrack(IEnumerator routine, int[] completedCount)
    {
        while (routine.MoveNext())
        {
            yield return routine.Current;
        }
        completedCount[0]++;
    }
}