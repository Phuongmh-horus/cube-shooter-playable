using System.Collections.Generic;
using UnityEngine;

public class PoolHolder : MonoSingleton<PoolHolder>
{
    Dictionary<string, Queue<MonoBehaviour>> _pools = new();
    Dictionary<string, int> _capacity = new();
    HashSet<string> _monoKeys = new();
    Dictionary<string, HashSet<MonoBehaviour>> _activeObjects = new();

    private void Start()
    {
        DontDestroyOnLoad(this);
    }

    public MonoBehaviour Get(MonoBehaviour t, Transform parent = null, Vector3 position = default,
        Quaternion rotation = default, string customKey = "")
    {
        lock (_pools)
        {
            var key = string.IsNullOrEmpty(customKey) ? GetKey(t) : customKey;
            if (_monoKeys.Add(key))
            {
                _pools.Add(key, new Queue<MonoBehaviour>(16));
                _capacity.Add(key, 0);
            }

            if (!_pools.TryGetValue(key, out var monoQueue)) return null;

            MonoBehaviour result = null;

            result = monoQueue.Count <= 0 ? Instantiate(t) : monoQueue.Dequeue();
            if (!result) result = Instantiate(t);

            result.name = key;
            var resultTransform = result.transform;
            resultTransform.SetParent(parent, false);
            resultTransform.position = position;
            resultTransform.rotation = rotation;
            result.gameObject.SetActive(true);

            if (!_activeObjects.TryGetValue(key, out var activeSet))
            {
                activeSet = new HashSet<MonoBehaviour>();
                _activeObjects.Add(key, activeSet);
            }
            activeSet.Add(result);

            return result;
        }
    }

    public void Release(MonoBehaviour t, string customKey = "")
    {
        lock (_pools)
        {
            var key = string.IsNullOrEmpty(customKey) ? t.name : customKey;
            if (_monoKeys.Add(key))
            {
                _pools.Add(key, new Queue<MonoBehaviour>(16));
            }

            if (!_pools.TryGetValue(key, out var queue)) return;

            if (_activeObjects.TryGetValue(key, out var activeSet))
            {
                activeSet.Remove(t);
            }

            var size = _capacity.GetValueOrDefault(key);
            if (size <= 0 || queue.Count < size)
            {
                queue.Enqueue(t);
                t.gameObject.SetActive(false);
            }
            else
            {
                Destroy(t);
            }
        }
    }

    /// <summary>
    /// Khởi tạo trước một số lượng object và đưa vào hàng đợi (Queue) để tránh giật lag khi Instantiate lúc đang chạy game.
    /// </summary>
    /// <param name="t">Prefab cần tạo pool</param>
    /// <param name="count">Số lượng object muốn tạo sẵn</param>
    /// <param name="parent">Transform cha chứa các object này</param>
    /// <param name="customKey">Tên key custom (nếu để trống sẽ tự lấy tên prefab)</param>
    public void PreWarm(MonoBehaviour t, int count, Transform parent = null, string customKey = "")
    {
        lock (_pools)
        {
            var key = string.IsNullOrEmpty(customKey) ? GetKey(t) : customKey;

            // Nếu pool chưa có key này thì khởi tạo Queue mới
            if (_monoKeys.Add(key))
            {
                _pools.Add(key, new Queue<MonoBehaviour>(count));
                _capacity.Add(key, 0);
            }

            if (!_pools.TryGetValue(key, out var queue)) return;

            // Instantiate trước và đưa vào Queue
            for (int i = 0; i < count; i++)
            {
                var item = Instantiate(t, parent);
                item.name = key;
                item.gameObject.SetActive(false); // Ẩn đi để nhét sẵn vào pool
                queue.Enqueue(item);
            }
        }
    }

    public System.Collections.IEnumerator PreWarmAsync(MonoBehaviour t, int count, Transform parent = null, string customKey = "", int itemsPerFrame = 5)
    {
        var key = string.IsNullOrEmpty(customKey) ? GetKey(t) : customKey;
        Queue<MonoBehaviour> targetQueue = null;

        lock (_pools)
        {
            if (_monoKeys.Add(key))
            {
                _pools.Add(key, new Queue<MonoBehaviour>(count));
                _capacity.Add(key, 0);
            }

            if (!_pools.TryGetValue(key, out targetQueue)) yield break;
        }

        for (int i = 0; i < count; i++)
        {
            var item = Instantiate(t, parent);
            item.name = key;
            item.gameObject.SetActive(false);
            
            lock (_pools)
            {
                targetQueue.Enqueue(item);
            }

            if ((i + 1) % itemsPerFrame == 0)
            {
                yield return null;
            }
        }
    }

    public void SetMaxSize(MonoBehaviour t, int size, string customKey = "")
    {
        var key = string.IsNullOrEmpty(customKey) ? GetKey(t) : customKey;
        _capacity[key] = size;
    }

    public static string GetKey(MonoBehaviour t)
    {
        return t.name + "-(PoolElement_No." + t.gameObject.GetInstanceID() + ")";
    }

    public void DeSpawnByKey(MonoBehaviour t, string customKey = "")
    {
        lock (_pools)
        {
            var key = string.IsNullOrEmpty(customKey) ? GetKey(t) : customKey;

            if (_activeObjects.TryGetValue(key, out var activeSet))
            {
                // Copy ra mảng tạm để tránh lỗi "Collection was modified" khi vòng lặp đang chạy
                var objectsToRelease = new List<MonoBehaviour>(activeSet);
                foreach (var obj in objectsToRelease)
                {
                    if (obj != null && obj.gameObject.activeInHierarchy)
                    {
                        Release(obj, key);
                    }
                }
            }
        }
    }

    private void OnDestroy()
    {
        lock (_pools)
        {
            _pools.Clear();
            _monoKeys.Clear();
            _activeObjects.Clear();
        }
    }
}