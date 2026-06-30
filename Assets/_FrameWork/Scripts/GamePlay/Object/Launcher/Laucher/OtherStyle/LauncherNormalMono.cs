using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum LinePosition
{
    None,   // Không có line
    Start,  // Launcher là đầu của line (nối tới launcher kế tiếp)
    End     // Launcher là cuối của line (nối từ launcher trước đó)
}

public class LauncherNormalMono : LauncherBaseMono
{
    [Header("References")]
    [SerializeField] private GameObject[] _launcherNormalLayerChange;
    [SerializeField] private TextMeshPro _tmpBulletAmount;
    [SerializeField] private Transform _pointSpawnBullet;

    [Header("Model - hạt đậu - cây")]
    [SerializeField] private GameObject _peaModel; // hạt đậu dưới hàng chờ
    [SerializeField] private GameObject _shooterModel; // cây súng trên chỗ bắn

    private SlotLauncherMono _slotLauncherMonoParent; // gọi reset slot khi hết đạn
    private List<LauncherNormalMono> _launchersConnect;

    private static VfxBase VfxRemoveHiddenShooter;
    private static VfxBase VFX_Shooter_Disapear;
    [SerializeField] private Transform _pointVfxReviceHidden;
    [SerializeField] private Transform _pointVFX_Shooter_Disapear;

    public void OnInit()
    {
        VfxRemoveHiddenShooter = ConfigHolder.Instance.PrefabsDataConfigSO.Vfx_RemoveHiddenShooter;
        VFX_Shooter_Disapear = ConfigHolder.Instance.PrefabsDataConfigSO.VFX_Shooter_Disapear;
    }
    public static readonly List<LauncherNormalMono> ActiveLaunchers = new List<LauncherNormalMono>(100);

    private void OnEnable()
    {
        if (!ActiveLaunchers.Contains(this)) ActiveLaunchers.Add(this);
    }

    private static readonly string[] NumberCache = new string[101];

    static LauncherNormalMono()
    {
        for (int i = 0; i <= 100; i++)
        {
            NumberCache[i] = i.ToString();
        }
    }

    private void OnDisable()
    {
        int idx = ActiveLaunchers.IndexOf(this);
        if (idx >= 0)
        {
            int lastIdx = ActiveLaunchers.Count - 1;
            ActiveLaunchers[idx] = ActiveLaunchers[lastIdx];
            ActiveLaunchers.RemoveAt(lastIdx);
        }
    }

    /// <summary>
    /// Dictionary lưu line connector và vị trí của launcher trên line đó
    /// Key: LineConnectorMono, Value: LinePosition (Start hoặc End)
    /// </summary>
    private Dictionary<LineConnectorMono, LinePosition> _lineConnectorPositions = new();
    private bool _canShoot;
    private bool _doneShoot; // đã bắn hết đạn chưa

    public List<LauncherNormalMono> LaunchersConnect => _launchersConnect;
    public bool DoneShoot => _doneShoot;

    public static void UpdateAllLaunchersRotation()
    {
        for (int i = ActiveLaunchers.Count - 1; i >= 0; i--)
        {
            var l = ActiveLaunchers[i];
            if (l == null || !l.gameObject.activeInHierarchy)
            {
                int lastIdx = ActiveLaunchers.Count - 1;
                ActiveLaunchers[i] = ActiveLaunchers[lastIdx];
                ActiveLaunchers.RemoveAt(lastIdx);
                continue;
            }

            if (l.enabled && l._objectBaseMono != null && l._objectBaseMono.gameObject.activeInHierarchy && l._shooterModel.activeSelf)
            {
                l.RotateToObjectBaseMono();
            }
        }
    }

    public void SetLaunchersConnect(List<LauncherNormalMono> group)
    {
        _launchersConnect = group;
    }

    /// <summary>
    /// Thêm line connector vào dictionary với vị trí tương ứng
    /// Nếu launcher hidden, sẽ set màu line ngay lập tức
    /// </summary>
    public void AddLineConnector(LineConnectorMono connector, LinePosition position)
    {
        if (connector != null && !_lineConnectorPositions.ContainsKey(connector))
        {
            _lineConnectorPositions[connector] = position;
            connector.OnDespawnEvent += HandleConnectorDespawn;

            // Nếu launcher hidden, set màu line ngay
            if (_isHidden)
            {
                Color hiddenColor = ConfigHolder.Instance?.ColorPallete_ForLauncher?.HiddenLineColor ?? Color.gray;
                if (position == LinePosition.Start)
                    connector.SetStartColor(hiddenColor);
                else if (position == LinePosition.End)
                    connector.SetEndColor(hiddenColor);
            }
        }
    }

    public override void OnInit(LauncherData data)
    {
        OnInit();
        SetupVisualNormal(false);
        SetupAnim();
        _slotLauncherMonoParent = null;
        _lineConnectorPositions.Clear();
        _tf ??= transform;
        _tf.localScale = Vector3.one;
        ChangeLayerRenderer(LayerNameGamePlay.Launcher);
        _data = data;
        ResetDissolveState();
        OnInitFrozen(_data.Frozened);
        _canShoot = false;
        _doneShoot = false;

        // Chỉ init material, UI - chưa update line color (chưa có line)
        InitializeHiddenState(_data.Hidden);
        UpdateTMPBullet();
        enabled = false; // Tắt LateUpdate xoay súng cho đến khi lên slot
        gameObject.SetActive(true);
    }

    public override void OnDespawn()
    {
        PlayVFX_Shooter_Disapear();
        GameEventBus.ACLauncherShoot.Remove(this);
        _launchersConnect = null;
        _doneShoot = false;
        _canShoot = false;
        if (_data != null && _data.ConnectedReferencesIDs != null)
        {
            _data.ConnectedReferencesIDs.Clear();
        }

        // Despawn tất cả line connectors
        var connectors = new List<LineConnectorMono>(_lineConnectorPositions.Keys);
        foreach (var connector in connectors)
        {
            if (connector != null)
                connector.OnDespawn();
        }
        _lineConnectorPositions.Clear();

        base.OnDespawn();
        enabled = false; // Đảm bảo tắt hẳn LateUpdate khi despawn
    }
    public void PlayVFX_Shooter_Disapear()
    {
        if (VFX_Shooter_Disapear != null)
        {
            VfxBase spawnNewVfx = PoolHolder.Instance.Get(VFX_Shooter_Disapear, null, _pointVFX_Shooter_Disapear.position) as VfxBase;
            if (spawnNewVfx != null && _pointVFX_Shooter_Disapear != null)
                spawnNewVfx.OnInit(_pointVFX_Shooter_Disapear.position);
        }
    }

    public override void RemoveLauncherAtVertical()
    {
        base.RemoveLauncherAtVertical();

        // Xóa toàn bộ dây kết nối khi Launcher bị bốc lên (nhấp vào / di chuyển sang slot)
        // để tránh lỗi dây bị kéo giãn vô lý, gây nháy sáng trên màn hình (visual glitch).
        var connectors = new List<LineConnectorMono>(_lineConnectorPositions.Keys);
        foreach (var connector in connectors)
        {
            if (connector != null)
                connector.OnDespawn();
        }
        _lineConnectorPositions.Clear();
    }

    public void SetupVisualNormal(bool IsGoDoneSlot)
    {
        _peaModel.SetActive(!IsGoDoneSlot);
        _shooterModel.SetActive(IsGoDoneSlot);
    }

    public void UpdateTMPBullet()
    {
        if (_tmpBulletAmount != null && ColorAndBullet != null && !_isHidden)
        {
            int amount = ColorAndBullet.Amount;
            _tmpBulletAmount.text = amount >= 0 && amount <= 100 ? NumberCache[amount] : amount.ToString();
        }
    }

    public void SetCanShoot(bool canShoot, bool removeActionShoot)
    {
        _canShoot = canShoot;
        if (removeActionShoot)
            GameEventBus.ACLauncherShoot.Remove(this);
    }

    public void ShootPiece()
    {
        if (!_canShoot || ColorAndBullet.Amount <= 0)
            return;

        _objectBaseMono = LevelSystem.Instance.Model3DController.GetObjectToShoot(ColorAndBullet.ColorCode);
        if (_objectBaseMono == null)
            return;
        PlayAnimShooterShoot();
        _objectBaseMono.OnBulletInComming();
        ColorAndBullet.Amount--;

        if (ColorAndBullet.Amount == 1)
            LevelSystem.Instance.GetPieceLauncherProjectilePool(_pointSpawnBullet.position).OnInit(_pointSpawnBullet.position, _objectBaseMono, () => { LevelSystem.Instance.CheckWinGame(); });
        else
            LevelSystem.Instance.GetPieceLauncherProjectilePool(_pointSpawnBullet.position).OnInit(_pointSpawnBullet.position, _objectBaseMono);
        UpdateTMPBullet();
        if (ColorAndBullet.Amount <= 0)
        {
            // Xóa khỏi action bắn
            GameEventBus.ACLauncherShoot.Remove(this);
            _doneShoot = true;
            _canShoot = false;

            // Check xem cả group đã hết đạn chưa
            if (IsAllConnectDoneShoot())
            {
                // Tất cả hết đạn → despawn cả group
                DespawnAllConnect();
            }
        }
    }

    /// <summary>
    /// Kiểm tra tất cả launcher trong group đã DoneShoot chưa.
    /// Giống doc: HandleCompleteColorPixels() — chỉ khi TOÀN BỘ đều BulletLeft <= 0 mới complete.
    /// </summary>
    private bool IsAllConnectDoneShoot()
    {
        if (_launchersConnect == null || _launchersConnect.Count <= 1)
            return true; // không có connect → chỉ mình nó → done

        foreach (var l in _launchersConnect)
        {
            if (!l.DoneShoot) return false;
        }
        return true;
    }

    /// <summary>
    /// Despawn tất cả launcher trong group + clear slot của chúng.
    /// Giống doc: OnCompletePixel() cho từng collector → biến mất.
    /// </summary>
    private void DespawnAllConnect()
    {
        if (_launchersConnect != null && _launchersConnect.Count > 1)
        {
            // Copy list vì OnDespawn sẽ set _launchersConnect = null
            var group = new List<LauncherNormalMono>(_launchersConnect);
            foreach (var l in group)
            {
                if (l._slotLauncherMonoParent != null)
                    LevelSystem.Instance.SlotLauncherQueueController.ClearLauncherSlot(l._slotLauncherMonoParent);
                l.OnDespawn();
            }
        }
        else
        {
            // Không có connect → despawn mình nó
            if (_slotLauncherMonoParent != null)
                LevelSystem.Instance.SlotLauncherQueueController.ClearLauncherSlot(_slotLauncherMonoParent);
            OnDespawn();
        }
    }

    public void AddACShootPiece()
    {
        _canShoot = true;
        if (!GameEventBus.ACLauncherShoot.Contains(this))
            GameEventBus.ACLauncherShoot.Add(this);
    }



    public void AddBulletAmount(int amount)
    {
        ColorAndBullet.Amount += amount;
        UpdateTMPBullet();
    }
    public SlotLauncherMono GetSlotLauncherMono => _slotLauncherMonoParent;

    public override void ChangeLayerRenderer(LayerNameGamePlay layer)
    {
        base.ChangeLayerRenderer(layer);
        foreach (GameObject VARIABLE in _launcherNormalLayerChange)
            VARIABLE.layer = (int)layer;
    }

    public override IEnumerator MoveToPosition(SlotLauncherMono slotLauncherMono, Vector3 targetPos, float duration = 0.3f, Action onComplete = null)
    {
        _slotLauncherMonoParent = slotLauncherMono;
        var enumerator = base.MoveToPosition(slotLauncherMono, targetPos, duration, onComplete);
        while (enumerator.MoveNext())
        {
            yield return enumerator.Current;
        }
    }

    public void SetupSlotLauncher(SlotLauncherMono slotLauncherMono)
    {
        _slotLauncherMonoParent = slotLauncherMono;
        enabled = true; // Bật LateUpdate để xoay súng bám mục tiêu
    }


    public Coroutine PlayJumpIntoHoleAndThenToSlot(
        float timeJump,
        float timeZPull,
        float timeRise,
        float zPullDistance,
        float zRiseDistance,
        Vector3 holePosition,
        SlotLauncherMono slot,
        Action onComplete = null)
    {
        if (_moveCts != null) StopCoroutine(_moveCts);
        _moveCts = StartCoroutine(JumpIntoHoleAndThenToSlot(timeJump, timeZPull, timeRise, zPullDistance, zRiseDistance, holePosition, slot, onComplete));
        return _moveCts;
    }

    public IEnumerator JumpIntoHoleAndThenToSlot(
        float timeJump,
        float timeZPull,
        float timeRise,
        float zPullDistance,
        float zRiseDistance,
        Vector3 holePosition,
        SlotLauncherMono slot,
        Action onComplete = null)
    {
        _tf ??= transform;
        _slotLauncherMonoParent = slot;

        _tf.SetParent(null, true);

        PlayAnimPeaJump();
        Vector3 startPos = _tf.position;

        // 1a. Bay vao h (holePosition)
        float durationXY = timeJump;
        float elapsedXY = 0f;
        while (elapsedXY < durationXY)
        {
            elapsedXY += Time.deltaTime;
            float t = elapsedXY / durationXY;
            t = t * t * (3f - 2f * t); // SmoothStep

            Vector3 currentPos = Vector3.Lerp(startPos, holePosition, t);
            currentPos.z += (-zPullDistance) * Mathf.Sin(Mathf.PI * t);
            _tf.position = currentPos;

            yield return null;
        }

        //1b. Scale xuong khi vao h, giu nguyen vị trí hole
        float durationScale = timeZPull > 0 ? timeZPull : 0.1f;
        var startScale = _tf.localScale;
        var targetScale = startScale * 0.2f;
        float elapsedScale = 0f;
        while (elapsedScale < durationScale)
        {
            elapsedScale += Time.deltaTime;
            float t = elapsedScale / durationScale;
            _tf.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        // 2. Chui xuyng di slot, thay i vị trí XY thnh slot
        Vector3 slotWorldPos = slot.GetSlotPosition();

        // n di slot, scale nh?, z cch xa v m<nh h<nh hn slot  to c?m gic chui di lAn
        _tf.position = new Vector3(slotWorldPos.x, slotWorldPos.y, slotWorldPos.z - zRiseDistance);

        // 3. Ni ln trn slot, chiu dy v t~ng scale
        float durationRise = timeRise > 0 ? timeRise : 0.1f;
        float elapsedRise = 0f;
        while (elapsedRise < durationRise)
        {
            elapsedRise += Time.deltaTime;
            float t = elapsedRise / durationRise;

            _tf.localScale = Vector3.Lerp(targetScale, startScale, t);

            float newZ = Mathf.Lerp(slotWorldPos.z - zRiseDistance, slotWorldPos.z, t);
            _tf.position = new Vector3(slotWorldPos.x, slotWorldPos.y, newZ);

            yield return null;
        }

        // Ensure final state
        _tf.position = slotWorldPos;
        _tf.localScale = startScale;

        onComplete?.Invoke();
    }
    public override void OnInitFrozen(int frozened)
    {
        base.OnInitFrozen(frozened);
    }

    public override void OnDestroyFrozen()
    {
        base.OnDestroyFrozen();
        _tmpBulletAmount.gameObject.SetActive(true);
    }


    #region Hidden

    private bool _isHidden;
    private bool _lastHiddenState = false; // Cache trạng thái hidden trước đó để trace

    /// <summary>
    /// Init hidden state - chỉ set material và UI, chưa update line color
    /// </summary>
    private void InitializeHiddenState(bool hidden)
    {
        _isHidden = hidden;
        if (_isHidden)
        {
            if (ConfigHolder.Instance != null && ConfigHolder.Instance.ColorPallete_ForLauncher != null)
                foreach (var VARIABLE in _cachedRenderers)
                    VARIABLE.sharedMaterial = ConfigHolder.Instance.ColorPallete_ForLauncher.HiddenMaterial;
            if (_tmpBulletAmount != null)
            {
                _tmpBulletAmount.gameObject.SetActive(true);
                _tmpBulletAmount.text = "?";
            }
            _canSelect = false;
        }
        else
        {
            if (ConfigHolder.Instance != null && ConfigHolder.Instance.ColorPallete_ForLauncher != null && ConfigHolder.Instance.ColorPallete_ForLauncher.colorDictionary != null)
                if (ConfigHolder.Instance.ColorPallete_ForLauncher.colorDictionary.ContainsKey(GetColorCodeIndex0()))
                    foreach (var VARIABLE in _cachedRenderers)
                        VARIABLE.sharedMaterial = ConfigHolder.Instance.ColorPallete_ForLauncher.colorDictionary[GetColorCodeIndex0()];
            if (_tmpBulletAmount != null)
                _tmpBulletAmount.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Hóa giải Hidden: set lại màu bình thường, cho phép click
    /// </summary>
    private void OnRevealHidden()
    {
        if (VfxRemoveHiddenShooter != null)
        {
            VfxBase spawnNewVfx = PoolHolder.Instance.Get(VfxRemoveHiddenShooter, null, _pointVfxReviceHidden.position) as VfxBase;
            if (spawnNewVfx != null && _pointVfxReviceHidden != null)
                spawnNewVfx.OnInit(_pointVfxReviceHidden.position);
        }

        SoundManager.Instance?.PlayOneShot(AudioClipName.Hidden_Shooter_Reveal);
        _isHidden = false;
        _data.Hidden = false;
        if (ConfigHolder.Instance != null && ConfigHolder.Instance.ColorPallete_ForLauncher != null && ConfigHolder.Instance.ColorPallete_ForLauncher.colorDictionary != null)
            if (ConfigHolder.Instance.ColorPallete_ForLauncher.colorDictionary.ContainsKey(GetColorCodeIndex0()))
                foreach (var VARIABLE in _cachedRenderers)
                    VARIABLE.sharedMaterial = ConfigHolder.Instance.ColorPallete_ForLauncher.colorDictionary[GetColorCodeIndex0()];
        _tmpBulletAmount.gameObject.SetActive(true);
        int amount = ColorAndBullet.Amount;
        _tmpBulletAmount.text = amount >= 0 && amount <= 100 ? NumberCache[amount] : amount.ToString();
        _canSelect = true;

        // Trở lại màu bình thường khi hết hidden
        _lastHiddenState = false; // Cache state
        UpdateLineHiddenColor(false);
    }

    /// <summary>
    /// Cập nhật màu line khi launcher bị hidden hoặc reveal
    /// Update tất cả các line có trong list
    /// </summary>
    private void UpdateLineHiddenColor(bool isHidden)
    {
        //isHidden = _data.Hidden;
        if (isHidden)
            Debug.LogError($"UpdateLineHiddenColor: LauncherID={_data.ID}, isHidden={isHidden}");

        if (_lineConnectorPositions == null || _lineConnectorPositions.Count == 0) return;

        // Cập nhật trạng thái cache (dùng để trace lịch sậ)
        _lastHiddenState = isHidden;

        Color hiddenColor = ConfigHolder.Instance?.ColorPallete_ForLauncher?.HiddenLineColor ?? Color.gray;
        Color normalColor = ConfigHolder.Instance?.ColorPallete_ForLauncher?.GetColorBase(GetColorCodeIndex0()) ?? Color.white;
        Color targetColor = isHidden ? hiddenColor : normalColor;

        // Cập nhật tất cả các line
        foreach (var lineConnectorPosition in _lineConnectorPositions)
        {
            var lineConnector = lineConnectorPosition.Key;
            var position = lineConnectorPosition.Value;

            if (lineConnector == null) continue;

            // Kiểm tra vị trí của launcher trên line này
            // Nếu launcher là Start, update nửa Start
            // Nếu launcher là End, update nửa End
            if (position == LinePosition.Start)
            {
                lineConnector.SetStartColor(targetColor);
                Debug.Log($"Updated Start side of line for launcher {_data.ID}");
            }
            else if (position == LinePosition.End)
            {
                lineConnector.SetEndColor(targetColor);
                Debug.Log($"Updated End side of line for launcher {_data.ID}");
            }
        }
    }

    public override void OnBecomeTop()
    {
        if (_isHidden)
            OnRevealHidden();
    }

    private void HandleConnectorDespawn(LineConnectorMono connector)
    {
        if (_lineConnectorPositions.ContainsKey(connector))
        {
            _lineConnectorPositions.Remove(connector);
        }
        if (connector != null)
        {
            connector.OnDespawnEvent -= HandleConnectorDespawn;
        }
    }

    #endregion

}