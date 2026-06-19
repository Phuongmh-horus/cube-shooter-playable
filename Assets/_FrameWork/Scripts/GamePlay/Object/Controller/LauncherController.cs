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
        // 1. Build dictionary ID → LauncherNormalMono
        var launcherMap = new Dictionary<int, LauncherNormalMono>();
        foreach (var vertical in _verticalPieceLauncherThisLevel)
            foreach (var launcher in vertical.LauncherBaseMonos)
                if (launcher is LauncherNormalMono normalMono && launcher.GetID() >= 0)
                    launcherMap[launcher.GetID()] = normalMono;

        // 2. BFS tìm groups
        var visited = new HashSet<int>();
        foreach (var kvp in launcherMap)
        {
            if (visited.Contains(kvp.Key)) continue;

            // BFS từ launcher này
            var group = new List<LauncherNormalMono>();
            var queue = new Queue<int>();
            queue.Enqueue(kvp.Key);
            visited.Add(kvp.Key);

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

            // 3. Gán connect group cho mỗi launcher trong nhóm
            // Launcher không nối ai → không gán gì (null)
            if (group.Count <= 1) continue;
            foreach (var launcher in group)
                launcher.SetLaunchersConnect(group);
        }
    }

    /// <summary>
    /// Nối dây visual giữa các LauncherNormalMono trong cùng group kết nối.
    /// Chỉ Normal mới có dây. Nối cặp liền kề: [0]↔[1], [1]↔[2], ...
    /// Dây 0-1 gán vào launcher[0], dây 1-2 gán vào launcher[1], launcher cuối = null.
    /// </summary>
    private void SetupLineConnectors()
    {
        // Build map ID → LauncherNormalMono (chỉ Normal)
        var launcherMap = new Dictionary<int, LauncherNormalMono>();
        foreach (var vertical in _verticalPieceLauncherThisLevel)
            foreach (var launcher in vertical.LauncherBaseMonos)
                if (launcher is LauncherNormalMono normalMono && launcher.GetID() >= 0)
                    launcherMap[launcher.GetID()] = normalMono;

        // BFS tìm groups
        var visited = new HashSet<int>();
        foreach (var kvp in launcherMap)
        {
            if (visited.Contains(kvp.Key)) continue;

            var group = new List<LauncherNormalMono>();
            var queue = new Queue<int>();
            queue.Enqueue(kvp.Key);
            visited.Add(kvp.Key);

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

            if (group.Count <= 1) continue;

            // Nối cặp liền kề: mỗi line được thêm vào cả launcher Start và End
            // Launcher giữa group sẽ có 2 line (1 là End, 1 là Start)
            for (int i = 0; i < group.Count - 1; i++)
            {
                var a = group[i];
                var b = group[i + 1];
                var newSpawnPool = PoolHolder.Instance.Get(_lineConnectorPrefab, _parentLineConnector);
                if (newSpawnPool is LineConnectorMono lineConnector)
                {
                    lineConnector.OnInit(a.transform, b.transform, ConfigHolder.Instance.ColorPallete_ForLauncher.GetColorBase(a.GetColorCodeIndex0()), ConfigHolder.Instance.ColorPallete_ForLauncher.GetColorBase(b.GetColorCodeIndex0()));

                    // Thêm line vào launcher a (a là Start của dây này)
                    a.AddLineConnector(lineConnector, LinePosition.Start);

                    // Thêm line vào launcher b (b là End của dây này)
                    b.AddLineConnector(lineConnector, LinePosition.End);
                }
                else
                    Debug.LogError($"Failed to get LineConnectorMono from pool");
            }
            // Launcher cuối group không có dây (đã null sẵn)
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
