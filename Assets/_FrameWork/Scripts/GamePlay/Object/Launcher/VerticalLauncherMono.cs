using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VerticalLauncherMono : MonoBehaviour
{
    [SerializeField] protected Renderer[] _cachedRenderers;
    #region <========================= PROPERTY & FIELD =========================>

    [SerializeField] private Transform _verticalPieceLauncherCenter;
    [SerializeField] private List<LauncherBaseMono> _launcherBaseMonos = new();
    public List<LauncherBaseMono> LauncherBaseMonos { get => _launcherBaseMonos; set => _launcherBaseMonos = value; }
    public int ColumnIndex { get; set; } = -1;

    [Header("Cache Prefabs Data")]
    private LauncherNormalMono _launcherNormalPrefab;
    private LauncherLockMono _launcherLockPrefab;
    private LauncherKeyMono _launcherKeyPrefab;
    private LauncherScissorsMono _launcherScissorsPrefab;

    private float _spacingLauncher = 0;
    #endregion


    private void Awake()
    {
        _spacingLauncher = ConfigHolder.Instance.LauncherConfigSo.SpacingLauncher;
    }

    #region <========================= GET & SET =========================>

    public void RemoveLauncher(LauncherBaseMono launcherBaseMono)
    {
        LauncherBaseMonos.Remove(launcherBaseMono);
        ArrangeVerticalTopCenter(_spacingLauncher, false);

        // Launcher mới ở đầu cột → gọi OnBecomeTop
        if (LauncherBaseMonos.Count > 0)
            LauncherBaseMonos[0].OnBecomeTop();
    }

    /// <summary>
    /// Bỏ súng khỏi list mà không sắp xếp cột (dùng cho auto-fill)
    /// </summary>
    public void RemoveLauncherDirect(LauncherBaseMono launcherBaseMono)
    {
        LauncherBaseMonos.Remove(launcherBaseMono);
        // Không gọi ArrangeVerticalTopCenter - bay lên luôn không sắp xếp
    }

    #endregion

    #region <========================= INIT DESPAWN =========================>

    public void OnInit(VerticalLauncherData verticalLauncherData, int columnIndex)
    {
        _launcherNormalPrefab ??= ConfigHolder.Instance.PrefabsDataConfigSO.LauncherNormalPrefab;
        _launcherLockPrefab ??= ConfigHolder.Instance.PrefabsDataConfigSO.LauncherLockPrefab;
        _launcherKeyPrefab ??= ConfigHolder.Instance.PrefabsDataConfigSO.LauncherKeyPrefab;
        _launcherScissorsPrefab ??= ConfigHolder.Instance.PrefabsDataConfigSO.LauncherScissorsPrefab;

        foreach (LauncherData VARIABLE in verticalLauncherData.LauncherDataDatas)
        {
            LauncherBaseMono launcherBaseMono = null;
            switch (VARIABLE.LauncherMode)
            {
                case LauncherMode.Normal:
                    launcherBaseMono = _launcherNormalPrefab;
                    break;
                case LauncherMode.Lock:
                    launcherBaseMono = _launcherLockPrefab;
                    break;
                case LauncherMode.Key:
                    launcherBaseMono = _launcherKeyPrefab;
                    break;
                case LauncherMode.Scissors:
                    launcherBaseMono = _launcherScissorsPrefab;
                    break;
                default:
                    Debug.LogError($"Invalid LauncherMode: {VARIABLE.LauncherMode}");
                    break;
            }

            if (launcherBaseMono == null)
                Debug.LogError($"Invalid LauncherMode: {VARIABLE.LauncherMode}");

            var newSpawnLaucher = PoolHolder.Instance.Get(launcherBaseMono, transform);
            if (newSpawnLaucher is LauncherBaseMono newLauncher)
            {
                newLauncher.OnInit(VARIABLE);
                _launcherBaseMonos.Add(newLauncher);
            }
            else
                Debug.LogError($"Failed to get LauncherBaseMono from pool for mode {VARIABLE.LauncherMode}");
        }

        ColumnIndex = columnIndex;
        ArrangeVerticalTopCenter(_spacingLauncher, true);
    }

    public void OnDespawn()
    {
        foreach (LauncherBaseMono VARIABLE in _launcherBaseMonos)
            VARIABLE.OnDespawn();
        _launcherBaseMonos.Clear();
        ColumnIndex = -1;
        PoolHolder.Instance.Release(this);
    }

    #endregion

    #region MAIN CORE

    #region VISUAL

    /// <summary>
    /// sắp xếp súng theo cột
    /// </summary>
    /// <param name="spacing"></param>
    public void ArrangeVerticalTopCenter(float spacing, bool onInit = false)
    {
        int count = _launcherBaseMonos.Count;

        List<Vector3> positions = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = _verticalPieceLauncherCenter.position;
            pos.y -= i * spacing;
            positions.Add(pos);
        }

        List<Coroutine> jumpTasks = new List<Coroutine>();

        for (int i = 0; i < count; i++)
        {
            _launcherBaseMonos[i].SetParentColumn(this);

            if (onInit || LevelSystem.IsTutorialLevel)
            {
                _launcherBaseMonos[i].transform.position = positions[i];
            }
            else
            {
                jumpTasks.Add(StartCoroutine(RabbitJumpToPosition(_launcherBaseMonos[i], positions[i])));
            }
        }

        if (!onInit && jumpTasks.Count > 0)
        {
            StartCoroutine(WaitAll(jumpTasks)) /* .Forget() removed */ ;
        }
    }

    private IEnumerator RabbitJumpToPosition(LauncherBaseMono launcherBaseMono, Vector3 targetPos)
    {
        if (launcherBaseMono == null || ConfigHolder.Instance == null || ConfigHolder.Instance.LauncherConfigSo == null)
            yield break;

        var targetTransform = launcherBaseMono.transform;
        float stepDistance = ConfigHolder.Instance.LauncherConfigSo.JumpStepDistance;
        float stepDuration = ConfigHolder.Instance.LauncherConfigSo.JumpStepDuration;
        float jumpHeight = ConfigHolder.Instance.LauncherConfigSo.JumpStepHeight;

        if (stepDistance <= 0) stepDistance = 1f;

        Vector3 startPos = targetTransform.position;
        float totalDistance = Vector3.Distance(startPos, targetPos);
        if (totalDistance < 0.01f)
        {
            targetTransform.position = targetPos;
            yield return null;
        }

        int steps = Mathf.CeilToInt(totalDistance / stepDistance);
        if (steps <= 0) steps = 1;

        for (int i = 1; i <= steps; i++)
        {
            if (targetTransform == null) yield return null;
            launcherBaseMono.PlayAnimPeaBunny();
            Vector3 currentStartPos = targetTransform.position;
            Vector3 currentTargetPos = Vector3.Lerp(startPos, targetPos, (float)i / steps);

            float elapsed = 0f;
            while (elapsed < stepDuration)
            {
                if (targetTransform == null) yield return null;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / stepDuration);

                float heightOffset = Mathf.Sin(t * Mathf.PI) * jumpHeight;
                Vector3 newPos = Vector3.Lerp(currentStartPos, currentTargetPos, t);
                newPos.z -= heightOffset; // Trục Z để tạo độ cong nảy ra ngoài màn hình

                targetTransform.position = newPos;
                yield return null;
            }

            if (targetTransform != null)
                targetTransform.position = currentTargetPos;
        }
    }

    public IEnumerator ArrangeVerticalTopCenterAnimAsync(float spacing, float duration)
    {
        int count = _launcherBaseMonos.Count;
        List<Coroutine> moveTasks = new List<Coroutine>();

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = _verticalPieceLauncherCenter.position;
            pos.y -= i * spacing;

            _launcherBaseMonos[i].SetParentColumn(this);
            moveTasks.Add(StartCoroutine(_launcherBaseMonos[i].MoveToPosition(null, pos, duration)));
        }

        yield return StartCoroutine(WaitAll(moveTasks));
    }

    #endregion

    /// <summary>
    /// Kiểm tra xem có nằm ở index 0 không 
    /// </summary>
    /// <returns></returns>
    public bool IsAtTop(LauncherBaseMono launcher)
    {
        if (_launcherBaseMonos.Count == 0) return false;
        return _launcherBaseMonos[0] == launcher;
    }

    #endregion

    public void SetRenderersActive(bool active)
    {
        if (_cachedRenderers == null || _cachedRenderers.Length == 0)
        {
            _cachedRenderers = GetComponentsInChildren<Renderer>(true);
        }
        foreach (var r in _cachedRenderers)
        {
            if (r != null)
            {
                r.enabled = active;
            }
        }
        foreach (var launcher in _launcherBaseMonos)
        {
            if (launcher != null)
            {
                launcher.SetRenderersActive(active);
            }
        }
    }

    private IEnumerator WaitAll(List<Coroutine> coroutines)
    {
        foreach (var c in coroutines)
        {
            if (c != null) yield return c;
        }
    }
}
