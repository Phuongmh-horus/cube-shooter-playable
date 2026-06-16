using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class Model3DController : MonoBehaviour, BaseLevelGenerator
{
    #region <========================= PROPERTY & FIELD =========================>

    [Header("Generator Level")]
    [SerializeField] private Transform _parentObject;

    [Header("Rotation Settings")]
    [SerializeField] private RotationModel3D _rotationModel;

    [Header("Check 8 hướng xem có object không")]
    [SerializeField] private Camera[] _cameras;

    [FormerlySerializedAs("_piecesThisLevel")]
    [Header("Khu vực - không động vào")]
    [SerializeField] private List<ObjectBaseMono> _objectBaseThisLevel = new List<ObjectBaseMono>();
    [SerializeField] private List<ObjectBaseMono> _objectBaseThisLevelCache = new List<ObjectBaseMono>();

    [Header("Cache Prefabs")]
    private ObjPieceMono _objectPiecePrefab;
    private ObjFrozenMono _objectFrozenPrefab;
    private ObjGiftBoxMono _objectGiftBoxPrefab;
    private ObjLargeCubeMono _objectLargeCubePrefab;

    [Header("Occlusion Strategies")]
    private MathTargetPickingSystem _mathPicking = new MathTargetPickingSystem();

    [Header("Job System Cache")]
    private Unity.Collections.NativeArray<PieceTargetData> _cachedJobData;
    private ObjectBaseMono[] _jobIndexToPiece;
    private Dictionary<ObjectBaseMono, int> _pieceToJobIndex = new Dictionary<ObjectBaseMono, int>();

    [Header("Logic Prelose")]
    private float _noTargetTimeoutToWarning; // Giây không có target → fire OnNoTargetTimeout -> Warning
    private float _delayWarningToPrelose; // Chờ warning -> xác nhận prelose
    private float _noTargetTimer;
    private float _preloseDelayTimer;
    private bool _isCountingNoTarget;
    private bool _hasTimeoutFired; // Sau khi timeout fired, chặn không cho đếm lại cho đến khi có target mới
    private bool _isWaitingPrelose;
    /// <summary>Fired một lần sau khi liên tục không tìm được target >= _noTargetTimeoutToWarning giây</summary>
    public event Action OnNoTargetTimeout;

    private bool _hasSetFindColorSpeed;
    private Coroutine _updateLoopCoroutine;

    [Header("Progress Level")]
    private int _totalObjestCount;

    public static Action CallBackOnRevice;

    [Header("Instancing Renderer")]
    private MaterialPropertyBlock _instancingPropertyBlock;
    private Mesh _cachedPieceMesh;

    private class InstancedBatchGroup
    {
        public Material material;
        public int activeCount = 0;
        public bool isDirty = false;

        public Matrix4x4[] localMatrices = new Matrix4x4[4000];
        public ObjPieceMono[] pieces = new ObjPieceMono[4000];

        public List<Matrix4x4[]> renderMatrices = new List<Matrix4x4[]>();
        public List<Vector4[]> renderColors = new List<Vector4[]>();
        public List<float[]> renderSpecSizes = new List<float[]>();
        public List<float[]> renderSpecSmooths = new List<float[]>();

        public void AddPiece(ObjPieceMono p, Matrix4x4 localMat, Vector4 color, float sSize, float sSmooth)
        {
            if (activeCount >= localMatrices.Length)
            {
                int newCap = Mathf.Max(1024, localMatrices.Length * 2);
                Array.Resize(ref localMatrices, newCap);
                Array.Resize(ref pieces, newCap);
            }

            localMatrices[activeCount] = localMat;
            pieces[activeCount] = p;

            int batchIndex = activeCount / 1023;
            int itemIndex = activeCount % 1023;

            while (renderMatrices.Count <= batchIndex)
            {
                renderMatrices.Add(new Matrix4x4[1023]);
                renderColors.Add(new Vector4[1023]);
                renderSpecSizes.Add(new float[1023]);
                renderSpecSmooths.Add(new float[1023]);
            }

            renderColors[batchIndex][itemIndex] = color;
            renderSpecSizes[batchIndex][itemIndex] = sSize;
            renderSpecSmooths[batchIndex][itemIndex] = sSmooth;

            activeCount++;
            isDirty = true;
        }

        public void RemovePiece(ObjPieceMono p)
        {
            int idx = Array.IndexOf(pieces, p, 0, activeCount);
            if (idx >= 0)
            {
                activeCount--;

                localMatrices[idx] = localMatrices[activeCount];
                pieces[idx] = pieces[activeCount];
                pieces[activeCount] = null;

                int dstBatch = idx / 1023;
                int dstItem = idx % 1023;
                int srcBatch = activeCount / 1023;
                int srcItem = activeCount % 1023;

                renderColors[dstBatch][dstItem] = renderColors[srcBatch][srcItem];
                renderSpecSizes[dstBatch][dstItem] = renderSpecSizes[srcBatch][srcItem];
                renderSpecSmooths[dstBatch][dstItem] = renderSpecSmooths[srcBatch][srcItem];
                renderMatrices[dstBatch][dstItem] = renderMatrices[srcBatch][srcItem];

                isDirty = true;
            }
        }
    }

    private Dictionary<CubeShooterColor, InstancedBatchGroup> _renderBatches = new Dictionary<CubeShooterColor, InstancedBatchGroup>();
    private Matrix4x4 _lastParentMatrix;

    private Matrix4x4 GetLocalMatrixRelativeToParent(ObjPieceMono p)
    {
        Matrix4x4 pLocal = Matrix4x4.TRS(p.transform.localPosition, p.transform.localRotation, p.transform.localScale);
        if (p.MeshTransform != p.transform)
        {
            Matrix4x4 childLocal = Matrix4x4.TRS(p.MeshTransform.localPosition, p.MeshTransform.localRotation, p.MeshTransform.localScale);
            return pLocal * childLocal;
        }
        return pLocal;
    }

    #endregion

    private void Awake()
    {
        CallBackOnRevice += OnRevice;
    }

    #region <========================= GET & SET =========================>

    public List<ObjectBaseMono> ObjectBase => _objectBaseThisLevel;

    public int GetProgressInThisLevel()
    {
        return Mathf.RoundToInt((float)(_totalObjestCount - _objectBaseThisLevel.Count) / _totalObjestCount * 100f);
    }

    /// <summary>
    /// Lấy danh sách các Piece còn hoạt động theo màu sắc
    /// </summary>
    public virtual ObjectBaseMono GetObjectToShoot(CubeShooterColor color)
    {
        if (!_cachedJobData.IsCreated || _cachedJobData.Length == 0) return null;

        int resultIndex = -1;
        Camera cam = CameraManager.Instance?.MainCamera ?? Camera.main;

        // Vẽ Camera phụ bằng Math Raycast (Không dùng Physics, không tốn GPU)
        // Truyền Parent World To Local Matrix để tính toán toàn bộ bằng Local Space cực nhanh!
        resultIndex = _mathPicking.GetTarget(cam, color, _cachedJobData, _jobIndexToPiece, _parentObject.worldToLocalMatrix);

        if (resultIndex != -1)
        {
            // Có target → reset bộ đếm timeout và prelose waiting
            // Chỉ gọi StopBlinkingAllSlot đúng 1 lần duy nhất khi vừa chuyển trạng thái từ Nháy màu -> Có target
            if (_hasTimeoutFired)
                LevelSystem.Instance?.SlotLauncherQueueController?.StopBlinkingAllSlot();
            _hasTimeoutFired = false;
            _isCountingNoTarget = false;
            _noTargetTimer = 0f;
            _isWaitingPrelose = false;
            _preloseDelayTimer = 0f;

            // --- Reset speed to default if it was set to SpeedRotationFindColor ---
            if (_hasSetFindColorSpeed)
            {
                _hasSetFindColorSpeed = false;
                _rotationModel.SetDefaultSpeed(true);
            }

            // Update ngay trong mảng cache để lượt bắn tiếp theo không bị trùng
            var data = _cachedJobData[resultIndex];
            data.IsBulletIncoming = true;
            _cachedJobData[resultIndex] = data;

            return _jobIndexToPiece[resultIndex];
        }

        // Null → bắt đầu đếm (chỉ tính từ lần null đầu tiên, và chỉ khi chưa fired)
        if (!_isCountingNoTarget && !_hasTimeoutFired)
        {
            _isCountingNoTarget = true;
            _noTargetTimer = 0f;
        }

        return null;
    }

    private void StartUpdateLoop()
    {
        StopUpdateLoop();
        _updateLoopCoroutine = StartCoroutine(UpdateLoopAsync());
    }

    private void StopUpdateLoop()
    {
        if (_updateLoopCoroutine != null)
        {
            StopCoroutine(_updateLoopCoroutine);
            _updateLoopCoroutine = null;
        }
    }

    private IEnumerator UpdateLoopAsync()
    {
        while (true)
        {
            if (_isCountingNoTarget)
            {
                _noTargetTimer += Time.deltaTime;
                if (_noTargetTimer >= _noTargetTimeoutToWarning)
                {
                    _isCountingNoTarget = false;
                    _hasTimeoutFired = true; // Chặn, chờ đến khi có target mới thì mới cho đếm lại
                    _noTargetTimer = 0f;

                    // --- Thay đổi tốc độ sang SpeedRotationFindColor ---
                    if (!_hasSetFindColorSpeed)
                    {
                        _hasSetFindColorSpeed = true;
                        _rotationModel.SetFindColorSpeed();
                    }

                    OnNoTargetTimeout?.Invoke();
                    if (LevelSystem.Instance.CheckPreloseGameAndWarning())
                    {
                        _isWaitingPrelose = true;
                        _preloseDelayTimer = 0f;
                    }
                }
            }

            // Xử lý prelose delay trực tiếp trong Update để dễ theo dõi
            if (_isWaitingPrelose && !_rotationModel.IsRotationHand)
            {
                _preloseDelayTimer += Time.deltaTime;
                if (_preloseDelayTimer >= _delayWarningToPrelose)
                {
                    _isWaitingPrelose = false;
                    _preloseDelayTimer = 0f;
                    LevelSystem.Instance.HandlePreLose();
                    StopUpdateLoop();
                }
            }

            yield return null;
        }
    }

    public void OnRevice()
    {
        ResetShootState(true);
        StartUpdateLoop();
    }

    public void ResetShootState(bool isStart = false)
    {
        _noTargetTimer = 0f;
        _preloseDelayTimer = 0f;
        _isCountingNoTarget = isStart;
        _hasTimeoutFired = false;
        _isWaitingPrelose = false;

        if (_hasSetFindColorSpeed)
        {
            _hasSetFindColorSpeed = false;
            if (_rotationModel != null)
            {
                _rotationModel.SetDefaultSpeed();
            }
        }
        LevelSystem.Instance?.SlotLauncherQueueController?.StopBlinkingAllSlot();
    }

    /// <summary>
    /// Cập nhật lại dữ liệu vào Cache (Dùng khi đạn bị trượt hoặc Cube đổi màu)
    /// </summary>
    public void SyncPieceStateToJob(ObjectBaseMono piece)
    {
        if (_pieceToJobIndex.TryGetValue(piece, out int jobIndex) && _cachedJobData.IsCreated)
        {
            var data = _cachedJobData[jobIndex];
            data.IsBulletIncoming = piece.IsBulletIncoming;
            data.Color = piece.GetColor();
            _cachedJobData[jobIndex] = data;
        }
    }

    public void RemovePiece(ObjectBaseMono objectBase)
    {
        // Khi xóa Piece, đánh dấu không hợp lệ trong Job cache
        if (_pieceToJobIndex.TryGetValue(objectBase, out int jobIndex))
        {
            if (_cachedJobData.IsCreated)
            {
                var data = _cachedJobData[jobIndex];
                data.IsBulletIncoming = true; // Sẽ không được pick nữa
                data.IsActive = false; // Xóa sổ hoàn toàn khỏi danh sách cản tia
                _cachedJobData[jobIndex] = data;
            }
            _pieceToJobIndex.Remove(objectBase);
            _jobIndexToPiece[jobIndex] = null;
        }

        if (objectBase is ObjPieceMono p)
        {
            if (_renderBatches.TryGetValue(p.GetColor(), out var group))
            {
                group.RemovePiece(p);
            }
        }

        _objectBaseThisLevel.Remove(objectBase);
    }

    #endregion

    #region CHECK END GAME WIN 

    public bool IsClearAllObjectInModel()
    {
        if (_objectBaseThisLevel.Count > 0)
            return false;
        // if(_frozenPiecesThisLevel.Count > 0)
        //     return false;
        // if (_giftBoxPiecesThisLevel.Count > 0)
        //     return false;
        // if(_largeCubePiecesThisLevel.Count > 0)
        //     return false;

        return true;
    }

    public void RotationEndGame() => _rotationModel.SetEndGameSpeed();
    public void RotationDeffault() => _rotationModel.SetDefaultSpeed();

    /// <summary>
    /// Check xem có bao nhiêu cube của màu đó nhìn thấy từ camera array
    /// Không ảnh hưởng đến cache job system - dùng raycast để check occlusion
    /// </summary>
    public bool IsVisiblePiecesByColor(CubeShooterColor targetColor)
    {
        if (_cameras == null || _cameras.Length == 0)
            return false;

        //var visiblePieces = new HashSet<ObjectBaseMono>();

        foreach (var cam in _cameras)
        {
            if (cam == null) continue;

            foreach (var piece in _objectBaseThisLevel)
            {
                if (piece == null || piece.GetColor() != targetColor)
                    continue;

                // Check trong viewport
                Vector3 screenPos = cam.WorldToScreenPoint(piece.transform.position);
                if (screenPos.z <= 0 ||
                    screenPos.x < 0 || screenPos.x > cam.pixelWidth ||
                    screenPos.y < 0 || screenPos.y > cam.pixelHeight)
                {
                    continue; // Ngoài viewport
                }

                // Raycast từ camera tới piece để check occlusion
                Vector3 piecePos = piece.transform.position;
                Vector3 dirToPiece = (piecePos - cam.transform.position).normalized;
                float distToPiece = Vector3.Distance(cam.transform.position, piecePos);

                if (Physics.Raycast(cam.transform.position, dirToPiece, out RaycastHit hit, distToPiece))
                {
                    // Nếu hit object là chính piece hoặc child của piece thì nhìn thấy
                    if (hit.transform == piece.transform || hit.transform.IsChildOf(piece.transform))
                        return true;//visiblePieces.Add(piece);
                }
            }
        }

        return false;//visiblePieces.Count;
    }

    #endregion

    #region GENERATOR LEVEL
    public IEnumerator OnInitPoolAsync()
    {
        _objectPiecePrefab ??= ConfigHolder.Instance.PrefabsDataConfigSO.ObjectPiecePrefab;
        _objectFrozenPrefab ??= ConfigHolder.Instance.PrefabsDataConfigSO.ObjectFrozenPrefab;
        _objectGiftBoxPrefab ??= ConfigHolder.Instance.PrefabsDataConfigSO.ObjectGiftBoxPrefab;
        _objectLargeCubePrefab ??= ConfigHolder.Instance.PrefabsDataConfigSO.ObjectLargeCubePrefab;

        _delayWarningToPrelose = ConfigHolder.Instance.EndGameConfigSo.DelayWarningToPrelose;
        _noTargetTimeoutToWarning = ConfigHolder.Instance.EndGameConfigSo.NoTargetTimeoutToWarning;

        _instancingPropertyBlock = new MaterialPropertyBlock();
        if (_objectPiecePrefab != null)
        {
            var mf = _objectPiecePrefab.GetComponentInChildren<MeshFilter>();
            if (mf != null) _cachedPieceMesh = mf.sharedMesh;
        }

        PoolHolder.Instance.PreWarm(_objectPiecePrefab, 100, _parentObject);
        yield break;
    }

    public IEnumerator OnLoadLevel(RoundDataBytes newRoundData)
    {
        var modelConfig = newRoundData?.ModelData;
        if (modelConfig == null || modelConfig.Pieces.Count == 0)
        {
            Debug.LogError($"Level data is empty");
            yield break; ;
        }
        foreach (PieceData pieceData in modelConfig.Pieces)
        {
            var newSpawnPool = PoolHolder.Instance.Get(_objectPiecePrefab, _parentObject);
            if (newSpawnPool is ObjPieceMono pieceMono)
            {
                pieceMono.OnInit(pieceData);
                _objectBaseThisLevel.Add(pieceMono);
                _objectBaseThisLevelCache.Add(pieceMono);
            }
        }

        foreach (GiftBoxData giftBoxData in modelConfig.GiftBoxes)
        {
            var newSpawnPool = PoolHolder.Instance.Get(_objectGiftBoxPrefab, _parentObject);
            if (newSpawnPool is ObjGiftBoxMono giftBoxMono)
            {
                giftBoxMono.OnInit(giftBoxData);
                _objectBaseThisLevel.Add(giftBoxMono);
                _objectBaseThisLevelCache.Add(giftBoxMono);
            }
        }

        foreach (FrozenCubeData frozenCubeData in modelConfig.FrozenCubes)
        {
            var newSpawnPool = PoolHolder.Instance.Get(_objectFrozenPrefab, _parentObject);
            if (newSpawnPool is ObjFrozenMono frozenCubeMono)
            {
                frozenCubeMono.OnInit(frozenCubeData);
                _objectBaseThisLevel.Add(frozenCubeMono);
                _objectBaseThisLevelCache.Add(frozenCubeMono);
            }
        }

        foreach (LargeCubeData largeCubeData in modelConfig.LargeCubes)
        {
            var newSpawnPool = PoolHolder.Instance.Get(_objectLargeCubePrefab, _parentObject);
            if (newSpawnPool is ObjLargeCubeMono largeCubeMono)
            {
                largeCubeMono.OnInit(largeCubeData);
                _objectBaseThisLevel.Add(largeCubeMono);
                _objectBaseThisLevelCache.Add(largeCubeMono);
            }
        }

        // --- CACHE DỮ LIỆU CHO JOB SYSTEM ---
        if (_cachedJobData.IsCreated) _cachedJobData.Dispose();

        int count = _objectBaseThisLevel.Count;
        _cachedJobData = new Unity.Collections.NativeArray<PieceTargetData>(count, Unity.Collections.Allocator.Persistent);
        _jobIndexToPiece = new ObjectBaseMono[count];
        _pieceToJobIndex.Clear();

        _mathPicking.Initialize();

        for (int i = 0; i < count; i++)
        {
            var piece = _objectBaseThisLevel[i];
            _jobIndexToPiece[i] = piece;
            _pieceToJobIndex[piece] = i;

            _cachedJobData[i] = new PieceTargetData
            {
                Index = i,
                Position = piece.transform.localPosition, // Lưu Local
                Rotation = piece.transform.localRotation, // Lưu Local
                Extents = piece.transform.localScale / 2f,
                Color = piece.GetColor(),
                IsBulletIncoming = piece.IsBulletIncoming,
                IsActive = true
            };
        }

        _renderBatches.Clear();
        foreach (var piece in _objectBaseThisLevel)
        {
            if (piece is ObjPieceMono p)
            {
                var color = p.GetColor();
                if (!_renderBatches.TryGetValue(color, out var group))
                {
                    group = new InstancedBatchGroup();
                    group.material = ConfigHolder.Instance.ColorPallete_ForPiece.colorDictionary[color];
                    _renderBatches[color] = group;
                }

                group.AddPiece(
                    p,
                    GetLocalMatrixRelativeToParent(p),
                    p.CurrentBaseColor.linear,
                    p.CurrentSpecularSize,
                    p.CurrentSpecularSmoothness
                );
            }
        }
        // ------------------------------------

        _rotationModel.StartRotation(transform.position);
        ResetShootState();
        StartUpdateLoop();
        _totalObjestCount = _objectBaseThisLevel.Count;
        yield break;
    }

    public void OnClear()
    {
        StopUpdateLoop();
        for (int i = _objectBaseThisLevel.Count - 1; i >= 0; i--)
        {
            _objectBaseThisLevel[i].OnDespawn();
        }

        _objectBaseThisLevelCache.Clear();
        _objectBaseThisLevel.Clear();
        _pieceToJobIndex.Clear();
        _renderBatches.Clear();

        if (_cachedJobData.IsCreated) _cachedJobData.Dispose();

        ResetShootState();
        _mathPicking.Release();
        _rotationModel.ResetRotation();
    }

    private void OnDestroy()
    {
        StopUpdateLoop();
        if (_cachedJobData.IsCreated) _cachedJobData.Dispose();

        _mathPicking.Release();
        _rotationModel.ResetRotation();
    }

    private void LateUpdate()
    {
        if (_cachedPieceMesh == null) return;

        Matrix4x4 currentParentMatrix = _parentObject.localToWorldMatrix;
        bool isParentMoved = currentParentMatrix != _lastParentMatrix;
        _lastParentMatrix = currentParentMatrix;

        foreach (var kvp in _renderBatches)
        {
            var group = kvp.Value;
            int totalPieces = group.activeCount;
            if (totalPieces == 0) continue;

            for (int i = 0; i < totalPieces; i += 1023)
            {
                int batchIndex = i / 1023;
                int batchSize = Mathf.Min(1023, totalPieces - i);

                Matrix4x4[] rMats = group.renderMatrices[batchIndex];

                if (isParentMoved || group.isDirty)
                {
                    for (int j = 0; j < batchSize; j++)
                    {
                        MultiplyMatrix(ref currentParentMatrix, ref group.localMatrices[i + j], ref rMats[j]);
                    }
                }

                _instancingPropertyBlock.SetVectorArray("_BaseColor", group.renderColors[batchIndex]);
                _instancingPropertyBlock.SetFloatArray("_SpecularToonSize", group.renderSpecSizes[batchIndex]);
                _instancingPropertyBlock.SetFloatArray("_SpecularToonSmoothness", group.renderSpecSmooths[batchIndex]);

                Graphics.DrawMeshInstanced(
                    _cachedPieceMesh,
                    0,
                    group.material,
                    rMats,
                    batchSize,
                    _instancingPropertyBlock,
                    UnityEngine.Rendering.ShadowCastingMode.Off,
                    false
                );
            }
            group.isDirty = false;
        }
    }

    // Tối ưu hóa cực độ: Nhân ma trận bằng tham chiếu (ref) để tránh C# sinh ra hàng nghìn bản copy struct (gây String.memcpy)
    private static void MultiplyMatrix(ref Matrix4x4 lhs, ref Matrix4x4 rhs, ref Matrix4x4 res)
    {
        res.m00 = lhs.m00 * rhs.m00 + lhs.m01 * rhs.m10 + lhs.m02 * rhs.m20 + lhs.m03 * rhs.m30;
        res.m01 = lhs.m00 * rhs.m01 + lhs.m01 * rhs.m11 + lhs.m02 * rhs.m21 + lhs.m03 * rhs.m31;
        res.m02 = lhs.m00 * rhs.m02 + lhs.m01 * rhs.m12 + lhs.m02 * rhs.m22 + lhs.m03 * rhs.m32;
        res.m03 = lhs.m00 * rhs.m03 + lhs.m01 * rhs.m13 + lhs.m02 * rhs.m23 + lhs.m03 * rhs.m33;

        res.m10 = lhs.m10 * rhs.m00 + lhs.m11 * rhs.m10 + lhs.m12 * rhs.m20 + lhs.m13 * rhs.m30;
        res.m11 = lhs.m10 * rhs.m01 + lhs.m11 * rhs.m11 + lhs.m12 * rhs.m21 + lhs.m13 * rhs.m31;
        res.m12 = lhs.m10 * rhs.m02 + lhs.m11 * rhs.m12 + lhs.m12 * rhs.m22 + lhs.m13 * rhs.m32;
        res.m13 = lhs.m10 * rhs.m03 + lhs.m11 * rhs.m13 + lhs.m12 * rhs.m23 + lhs.m13 * rhs.m33;

        res.m20 = lhs.m20 * rhs.m00 + lhs.m21 * rhs.m10 + lhs.m22 * rhs.m20 + lhs.m23 * rhs.m30;
        res.m21 = lhs.m20 * rhs.m01 + lhs.m21 * rhs.m11 + lhs.m22 * rhs.m21 + lhs.m23 * rhs.m31;
        res.m22 = lhs.m20 * rhs.m02 + lhs.m21 * rhs.m12 + lhs.m22 * rhs.m22 + lhs.m23 * rhs.m32;
        res.m23 = lhs.m20 * rhs.m03 + lhs.m21 * rhs.m13 + lhs.m22 * rhs.m23 + lhs.m23 * rhs.m33;

        res.m30 = lhs.m30 * rhs.m00 + lhs.m31 * rhs.m10 + lhs.m32 * rhs.m20 + lhs.m33 * rhs.m30;
        res.m31 = lhs.m30 * rhs.m01 + lhs.m31 * rhs.m11 + lhs.m32 * rhs.m21 + lhs.m33 * rhs.m31;
        res.m32 = lhs.m30 * rhs.m02 + lhs.m31 * rhs.m12 + lhs.m32 * rhs.m22 + lhs.m33 * rhs.m32;
        res.m33 = lhs.m30 * rhs.m03 + lhs.m31 * rhs.m13 + lhs.m32 * rhs.m23 + lhs.m33 * rhs.m33;
    }
    #endregion
}
