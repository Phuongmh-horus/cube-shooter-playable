using System.Collections;
using UnityEngine;

public class LevelSystem : MonoSingleton<LevelSystem>
{
    #region <========================= PROPERTY & FIELD =========================>

    [Header("Controller")]
    [SerializeField] private GameObject[] _controllerInput;
    [SerializeField] private SlotLauncherQueueController _slotLauncherQueueController;
    [SerializeField] private LauncherController _launcherController;
    [SerializeField] private Model3DController _model3DController;

    [Space]
    [Header("Projectile")]
    private LauncherProjectile _launcherProjectile;
    [SerializeField] private Transform _parentProjectile;

    private int _currentLevelId;
    private static bool _canCallInitOneShoot = true; //gọi 1 lần trong toàn bộ game để load thứ cần thiết
    public static int CurrentLevelProgress = 1;
    public static int CurrentLevelDisplay = 1;
    public static int GetCurrentLevelProgress() => CurrentLevelProgress > 0 ? CurrentLevelProgress : 1;
    public static int GetCurrentLevelDisplay() => CurrentLevelDisplay > 0 ? CurrentLevelDisplay : 1;

    #endregion


    #region <========================= STATIC CONTROL =========================>
    public static bool LevelHasBeenStarted;
    public static bool IsEndGame;
    public static bool IsTutorialLevel;

    //================ Tracking ===================
    public static int InGameTime;
    public static bool IsTrackingTime;

    private static int PreloseCount;
    #endregion

    #region UNITY CORE

    protected override void Awake()
    {
        base.Awake();
        _canCallInitOneShoot = true; // Reset biến static đề phòng Domain Reload tắt
        GameEventBus.OnActiveInputGameplay += OnActiveInputGameplayAC;
    }

    private void OnDestroy()
    {
        GameEventBus.OnActiveInputGameplay -= OnActiveInputGameplayAC;
    }

    private void OnActiveInputGameplayAC(bool obj)
    {
        foreach (GameObject VARIABLE in _controllerInput)
            VARIABLE.SetActive(obj);
    }

    #endregion

    #region <========================= GET & SET =========================>

    public SlotLauncherQueueController SlotLauncherQueueController => _slotLauncherQueueController;
    public LauncherController LauncherController => _launcherController;
    public Model3DController Model3DController => _model3DController;

    #endregion

    #region <========================= INIT DESPAWN =========================>

    public void OnInitTracingStartGame()
    {
        PreloseCount = 0;
        StartTrackingTime();
    }

    /// <summary>
    /// Hàm dành riêng cho Playable Ads để load level từ ScriptableObject
    /// </summary>
    public IEnumerator LoadLevelFromPLA(RoundDataAsset roundDataAsset)
    {
        ShowOnlyModel3D(false);
        ClearLevel();

        if (_canCallInitOneShoot)
        {
            // Note: Since we are converting to Coroutines, the Controllers must implement IEnumerator OnInitPool()





            yield return StartCoroutine(_model3DController.OnInitPoolAsync());
            yield return StartCoroutine(_launcherController.OnInitPoolAsync());
            yield return StartCoroutine(_slotLauncherQueueController.OnInitPoolAsync());

            _canCallInitOneShoot = false;
            _launcherProjectile = ConfigHolder.Instance.PrefabsDataConfigSO.LauncherProjectilePrefab;
        }

        RoundDataBytes roundDataBytes = roundDataAsset.GetLevelData();

        // --- Áp dụng Scale và Rotation từ SO vào Model3D ---
        float applyScale = roundDataAsset.Scale > 0 ? roundDataAsset.Scale : 1f;
        Vector3 applyRotation = roundDataAsset.ModelRotation;
        if (_model3DController != null)
        {
            _model3DController.transform.localScale = Vector3.one * applyScale;
            _model3DController.transform.localEulerAngles = applyRotation;
        }
        // -----------------------------------------------

        if (roundDataBytes != null)
        {
            yield return StartCoroutine(_slotLauncherQueueController.OnLoadLevel(roundDataBytes));
            yield return StartCoroutine(_launcherController.OnLoadLevel(roundDataBytes));
            yield return StartCoroutine(_model3DController.OnLoadLevel(roundDataBytes));

            GameEventBus.OnLoadLevelDone?.Invoke();
            Debug.Log($"[LevelSystem] Đã load level từ RoundDataAsset: {roundDataAsset.name}");
        }
        else
        {
            Debug.LogError($"[LevelSystem] RoundDataBytes is null from asset {roundDataAsset.name}");
        }
    }

    /// <summary>
    /// Dọn dẹp khi thoát level
    /// </summary>
    public void ClearLevel()
    {
        _slotLauncherQueueController.OnClear();
        _launcherController.OnClear();
        _model3DController.OnClear();
        LevelHasBeenStarted = false;
        IsEndGame = false;
        GamePlayManager.SHOW_STARTER_PACK_CONDITION = false;
        ChangeSpeedGame(false);
    }

    public void ShowOnlyModel3D()
    {
        ShowOnlyModel3D(true);
    }

    public void ShowOnlyModel3D(bool showOnly)
    {
        bool showLaunchersAndSlots = !showOnly;

        if (_slotLauncherQueueController != null)
        {
            if (ConfigHolder.Instance != null && ConfigHolder.Instance.SlotAppearanceConfigSO != null && !IsTutorialLevel)
                StartCoroutine(_slotLauncherQueueController.PlayJumpAppearanceAnim(showLaunchersAndSlots, ConfigHolder.Instance.SlotAppearanceConfigSO, true));
            else
                _slotLauncherQueueController.SetRenderersActive(showLaunchersAndSlots);
        }

        if (_launcherController != null)
            _launcherController.SetRenderersActive(showLaunchersAndSlots);
    }

    #endregion

    #region ACTION MAIN GAME PLAY

    public void HandleWin()
    {
        if (IsEndGame)
            return;
        StopTrackingTime();
        IsEndGame = true;
        GameEventBus.OnWinGame?.Invoke();
        StartCoroutine(HandleShowUIWinRoutine());
    }

    private IEnumerator HandleShowUIWinRoutine()
    {
        yield return new WaitForSeconds(1.5f);

        if (PlayableAdsUIController.Instance != null)
        {
            PlayableAdsUIController.Instance.ShowEndcard();
        }
    }

    public void HandleLose()
    {
        if (IsEndGame)
            return;
        StopTrackingTime();
        IsEndGame = true;

        if (PlayableAdsUIController.Instance != null)
        {
            PlayableAdsUIController.Instance.ShowEndcard();
        }
    }

    public void ReturnMainMenuInPause()
    {
        StopTrackingTime();
    }

    public void ReplayInPause()
    {
        StopTrackingTime();
    }

    public void HandlePreLose()
    {
        if (IsEndGame)
            return;
        PreloseCount++;
    }

    #endregion

    #region CHECK WIN LOSE GAME

    public void CheckWinGame()
    {
        if (_model3DController.IsClearAllObjectInModel())
            HandleWin();
    }

    public bool CheckPreloseGameAndWarning()
    {
        if (!_slotLauncherQueueController.CanMoveAnyTopLauncherToSlot())
        {
            foreach (SlotLauncherMono VARIABLE in _slotLauncherQueueController.LauncherSlotThisLevel)
            {
                if (VARIABLE.CurrentLauncher == null)
                    continue;
                if (_model3DController.IsVisiblePiecesByColor(VARIABLE.CurrentLauncher.GetColorCodeIndex0()))
                {
                    return false;
                }
            }
            _slotLauncherQueueController.StartBlinkingAllSlot();
            return true;
        }
        return false;
    }

    #endregion

    public LauncherProjectile GetPieceLauncherProjectilePool(Vector3 pos) => PoolHolder.Instance.Get(_launcherProjectile, _parentProjectile, pos) as LauncherProjectile;

    public void ChangeSpeedGame(bool _isEndGame)
    {
        if (_isEndGame)
        {
            _model3DController.RotationEndGame();
            _slotLauncherQueueController.ChangeFireRateLauncher(ConfigHolder.Instance.SlotLauncherConfigSo.FireRateLauncherEndGame);
            LauncherProjectile.SetSpeed(ConfigHolder.Instance.SlotLauncherConfigSo.SpeedProjectileEndGame);
        }
        else
        {
            _model3DController.RotationDeffault();
            _slotLauncherQueueController.ChangeFireRateLauncher(ConfigHolder.Instance.SlotLauncherConfigSo.FireRateLauncherDeffault);
            LauncherProjectile.SetSpeed(ConfigHolder.Instance.SlotLauncherConfigSo.SpeedProjectileDeffault);
        }
    }

    #region TIME TRACKING

    /// <summary>
    /// Bắt đầu theo dõi thời gian trong gameplay
    /// </summary>
    public void StartTrackingTime()
    {
        if (IsTrackingTime) return;

        IsTrackingTime = true;
        InGameTime = 0;
        StartCoroutine(UpdateTimeTrackingRoutine());
    }

    /// <summary>
    /// Dừng theo dõi thời gian trong gameplay
    /// </summary>
    public static void StopTrackingTime()
    {
        IsTrackingTime = false;
    }

    /// <summary>
    /// Coroutine loop cập nhật thời gian gameplay, chạy mỗi 1 giây
    /// </summary>
    private IEnumerator UpdateTimeTrackingRoutine()
    {
        var waitTime = new WaitForSeconds(1f);
        while (IsTrackingTime)
        {
            yield return waitTime;
            if (IsTrackingTime)
            {
                InGameTime++;
            }
        }
    }

    #endregion

    private void Update()
    {
        LauncherProjectile.UpdateAllProjectiles();
    }

}