using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LauncherController : MonoBehaviour, BaseLevelGenerator
{
    #region <========================= PROPERTY & FIELD =========================>

    [Header("Generator level")]
    private VerticalLauncherMono _verticalLauncherPrefab;
    private WallControl _wallPrefab;
    [SerializeField] private Transform _parentLauncherProjectile;
    [Space]
    [Header("Launcher Slot Queue")]
    [SerializeField] private Transform _centerVerticalPieceLauncher;
    [SerializeField] private List<VerticalLauncherMono> _verticalPieceLauncherThisLevel = new List<VerticalLauncherMono>();
    [SerializeField] private List<WallControl> _wallsThisLevel = new List<WallControl>();
    [Space]
    [Header("Line Connector")]
    private LineConnectorMono _lineConnectorPrefab;
    [SerializeField] private Transform _parentLineConnector;

    private float _spacingVerticalLauncher = -1;
    #endregion

    #region UNITY CORE

    private void Awake()
    {
        _spacingVerticalLauncher = ConfigHolder.Instance.LauncherConfigSo.SpacingVerticalLauncher;
    }

    private void OnDestroy()
    {
        if (_wallsThisLevel != null)
        {
            foreach (var wall in _wallsThisLevel)
            {
                if (wall != null)
                {
                    Destroy(wall.gameObject);
                }
            }
            _wallsThisLevel.Clear();
        }
    }

    #endregion

    #region GET & SET
    public int GetCountUnusedLauncher()
    {
        int count = 0;
        foreach (VerticalLauncherMono VARIABLE in _verticalPieceLauncherThisLevel)
            count += VARIABLE.LauncherBaseMonos.Count;
        return count;
    }

    public List<VerticalLauncherMono> GetVerticalLaunchers() => _verticalPieceLauncherThisLevel;

    #endregion

    #region SETUP VISUAL SPACING LAUNCHER

    private void VerticalPieceLauncherArrangeHorizontal()
    {
        int launcherCount = _verticalPieceLauncherThisLevel.Count;
        int wallCount = _wallsThisLevel.Count;
        int totalCount = launcherCount + wallCount;

        if (totalCount <= 0) return;

        float startX = -(totalCount - 1) * 0.5f * _spacingVerticalLauncher;

        int currentIndex = 0;
        // Position Wall 1 (if exists) at index 0
        if (wallCount > 0 && _wallsThisLevel[0] != null)
        {
            Vector3 pos = _centerVerticalPieceLauncher.position;
            pos.x += startX + currentIndex * _spacingVerticalLauncher;
            _wallsThisLevel[0].transform.position = pos;
            currentIndex++;
        }

        // Position all Vertical Launchers
        for (int i = 0; i < launcherCount; i++)
        {
            Vector3 pos = _centerVerticalPieceLauncher.position;
            pos.x += startX + currentIndex * _spacingVerticalLauncher;
            _verticalPieceLauncherThisLevel[i].transform.position = pos;
            currentIndex++;
        }

        // Position Wall 2 (if exists) at index totalCount - 1
        if (wallCount > 1 && _wallsThisLevel[1] != null)
        {
            Vector3 pos = _centerVerticalPieceLauncher.position;
            pos.x += startX + currentIndex * _spacingVerticalLauncher;
            _wallsThisLevel[1].transform.position = pos;
        }
    }

    #endregion

    #region CONNECT LAUNCHER CONTROLLER

    /// <summary>
    /// BFS bắc cầu: tìm tất cả group kết nối, gán vào LauncherNormalMono._launchersConnect.
    /// Giống doc: SetupCollectorControllersConnect()
    /// A nối B, B nối C → group = [A, B, C]. Mỗi launcher trong group đều có cùng list.
    /// </summary>
    private void SetupLauncherConnectGroups()
    {
        if (PlayableAdsController.Instance != null && !PlayableAdsController.Instance.EnableLineConnector)
            return;

        var launcherMap = new System.Collections.Generic.Dictionary<int, LauncherNormalMono>();
        var orderedLaunchers = new System.Collections.Generic.List<LauncherNormalMono>();

        foreach (var vertical in _verticalPieceLauncherThisLevel)
        {
            foreach (var launcher in vertical.LauncherBaseMonos)
            {
                if (launcher is LauncherNormalMono normalMono && launcher.GetID() >= 0)
                {
                    launcherMap[launcher.GetID()] = normalMono;
                    orderedLaunchers.Add(normalMono);
                }
            }
        }

        var visited = new System.Collections.Generic.HashSet<int>();

        foreach (var startMono in orderedLaunchers)
        {
            int startId = startMono.GetID();
            if (visited.Contains(startId)) continue;

            var group = new System.Collections.Generic.List<LauncherNormalMono>();
            var queue = new System.Collections.Generic.Queue<int>();
            queue.Enqueue(startId);
            visited.Add(startId);

            while (queue.Count > 0)
            {
                int curId = queue.Dequeue();
                if (!launcherMap.TryGetValue(curId, out var launcher)) continue;

                group.Add(launcher);
                var connectedIDs = launcher.GetConnectedReferencesIDs();
                if (connectedIDs == null) continue;

                foreach (int nextId in connectedIDs)
                {
                    if (visited.Contains(nextId)) continue;
                    visited.Add(nextId);
                    queue.Enqueue(nextId);
                }
            }

            if (group.Count > 1)
            {
                group.Sort((a, b) =>
                {
                    int colCmp = a.ColumnIndex.CompareTo(b.ColumnIndex);
                    if (colCmp != 0) return colCmp;
                    return b.transform.position.y.CompareTo(a.transform.position.y);
                });

                foreach (var launcher in group)
                    launcher.SetLaunchersConnect(new System.Collections.Generic.List<LauncherNormalMono>(group));
            }
        }
    }

    private void SetupLineConnectors()
    {
        if (PlayableAdsController.Instance != null && !PlayableAdsController.Instance.EnableLineConnector)
            return;

        System.Collections.Generic.HashSet<LauncherNormalMono> drawnLaunchers = new System.Collections.Generic.HashSet<LauncherNormalMono>();

        foreach (var vertical in _verticalPieceLauncherThisLevel)
        {
            foreach (var launcher in vertical.LauncherBaseMonos)
            {
                if (launcher is LauncherNormalMono startMono &&
                    !drawnLaunchers.Contains(startMono) &&
                    startMono.LaunchersConnect != null &&
                    startMono.LaunchersConnect.Count > 1)
                {
                    var group = startMono.LaunchersConnect;

                    for (int i = 0; i < group.Count - 1; i++)
                    {
                        var a = group[i];
                        var b = group[i + 1];

                        drawnLaunchers.Add(a);
                        drawnLaunchers.Add(b);

                        // Instantiate LineConnector
                        var newSpawnPool = PoolHolder.Instance.Get(_lineConnectorPrefab, _parentLineConnector);
                        if (newSpawnPool is LineConnectorMono lineConnector)
                        {
                            Color colorA = ConfigHolder.Instance.ColorPallete_ForLauncher.GetColorBase(a.GetColorCodeIndex0());
                            Color colorB = ConfigHolder.Instance.ColorPallete_ForLauncher.GetColorBase(b.GetColorCodeIndex0());

                            colorA = Color.Lerp(colorA, Color.white, 0.2f);
                            colorA.a = 1f;
                            colorB = Color.Lerp(colorB, Color.white, 0.2f);
                            colorB.a = 1f;

                            lineConnector.OnInit(a.transform, b.transform, colorA, colorB);

                            // Cập nhật launcher để gán connector
                            a.AddLineConnector(lineConnector, LinePosition.Start);
                            b.AddLineConnector(lineConnector, LinePosition.End);
                        }
                        else
                            Debug.LogError($"Failed to get LineConnectorMono from pool");
                    }
                }
            }
        }
    }

    #endregion


    #region GENERATOR LEVEL

    public IEnumerator OnInitPoolAsync()
    {
        _verticalLauncherPrefab ??= ConfigHolder.Instance.PrefabsDataConfigSO.VerticalLauncherPrefab;
        _wallPrefab ??= ConfigHolder.Instance.PrefabsDataConfigSO.WallAroundPrefab;
        _lineConnectorPrefab ??= ConfigHolder.Instance.PrefabsDataConfigSO.LineConnectorPrefab;
        _spacingVerticalLauncher = ConfigHolder.Instance.LauncherConfigSo.SpacingVerticalLauncher;

        // Tối ưu Playable: Load đồng bộ (không yield return) với số lượng ít hơn để game start ngay lập tức
        PoolHolder.Instance.PreWarm(_verticalLauncherPrefab, 5, _parentLauncherProjectile);
        PoolHolder.Instance.PreWarm(_lineConnectorPrefab, 5, _parentLineConnector);
        PoolHolder.Instance.PreWarm(ConfigHolder.Instance.PrefabsDataConfigSO.LauncherProjectilePrefab, 10, _parentLauncherProjectile);
        yield break;
    }

    public IEnumerator OnLoadLevel(RoundDataBytes newRoundData)
    {
        List<VerticalLauncherData> verticalPieceLauncherDatas = newRoundData.ListVerticalLauncherData.VerticalLauncherDatas;

        // Ensure we have exactly 2 valid walls in _wallsThisLevel, reuse them if already spawned
        _wallsThisLevel.RemoveAll(w => w == null);
        while (_wallsThisLevel.Count < 2)
        {
            if (_wallPrefab != null)
            {
                var wall = Instantiate(_wallPrefab, _parentLauncherProjectile);
                _wallsThisLevel.Add(wall);
                if (_wallsThisLevel.Count == 2)
                {
                    wall.ChangeState();
                }
            }
            else
            {
                Debug.LogError("WallPrefab is null");
                break;
            }
        }

        // Spawn Vertical Launchers
        for (int colIdx = 0; colIdx < verticalPieceLauncherDatas.Count; colIdx++)
        {
            VerticalLauncherData vData = verticalPieceLauncherDatas[colIdx];
            var newGameObject = PoolHolder.Instance.Get(_verticalLauncherPrefab, _parentLauncherProjectile);
            if (newGameObject is VerticalLauncherMono verticalLauncher)
            {
                verticalLauncher.OnInit(vData, colIdx);
                _verticalPieceLauncherThisLevel.Add(verticalLauncher);
            }
            else
                Debug.LogError($"Failed to get VerticalLauncherMono from pool");
        }

        VerticalPieceLauncherArrangeHorizontal();

        // BFS truy xuất nhóm Collector – Cơ chế lookup ID → Object
        SetupLauncherConnectGroups();

        // Nối dây visual giữa các launcher kết nối
        SetupLineConnectors();

        yield break;
    }

    public void OnClear()
    {
        foreach (VerticalLauncherMono VARIABLE in _verticalPieceLauncherThisLevel)
            VARIABLE.OnDespawn();
        foreach (var action in GameEventBus.ACDespawnLauncherProjectile)
            action?.Invoke();
        _verticalPieceLauncherThisLevel.Clear();

        // Do not destroy the walls here so they can be reused when the next level is loaded.
    }

    #endregion

    public void SetRenderersActive(bool active)
    {
        foreach (var vertical in _verticalPieceLauncherThisLevel)
        {
            if (vertical != null)
            {
                vertical.SetRenderersActive(active);
            }
        }
        foreach (var wall in _wallsThisLevel)
        {
            if (wall != null)
            {
                var renderers = wall.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    if (r != null)
                    {
                        r.enabled = active;
                    }
                }
            }
        }
    }
}
