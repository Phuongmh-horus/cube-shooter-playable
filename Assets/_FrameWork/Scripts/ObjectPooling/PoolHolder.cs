using System.Collections.Generic;
using UnityEngine;

public class PoolHolder : MonoSingleton<PoolHolder>
{
    Dictionary<int, Queue<MonoBehaviour>> _pools = new();
    Dictionary<int, int> _capacity = new();
    HashSet<int> _monoKeys = new();
    Dictionary<int, HashSet<MonoBehaviour>> _activeObjects = new();

    private void Start()
    {
        DontDestroyOnLoad(this);
    }

    public MonoBehaviour Get(MonoBehaviour t, Transform parent = null, Vector3 position = default,
        Quaternion rotation = default, int customKey = 0)
    {
        lock (_pools)
        {
            var key = customKey == 0 ? GetKey(t) : customKey;
            if (_monoKeys.Add(key))
            {
                _pools.Add(key, new Queue<MonoBehaviour>(16));
                _capacity.Add(key, 0);
            }

            if (!_pools.TryGetValue(key, out var monoQueue)) return null;

            MonoBehaviour result = null;

            result = monoQueue.Count <= 0 ? Instantiate(t) : monoQueue.Dequeue();
            if (!result) result = Instantiate(t);

            var marker = result.GetComponent<PoolItemMarker>();
            if (!marker) marker = result.gameObject.AddComponent<PoolItemMarker>();
            marker.PrefabID = key;

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

    public void Release(MonoBehaviour t, int customKey = 0)
    {
        lock (_pools)
        {
            int key = customKey;
            if (key == 0)
            {
                var marker = t.GetComponent<PoolItemMarker>();
                if (marker) key = marker.PrefabID;
                else
                {
                    Destroy(t.gameObject);
                    return;
                }
            }

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
                Destroy(t.gameObject);
            }
        }
    }

    public void PreWarm(MonoBehaviour t, int count, Transform parent = null, int customKey = 0)
    {
        lock (_pools)
        {
            var key = customKey == 0 ? GetKey(t) : customKey;

            if (_monoKeys.Add(key))
            {
                _pools.Add(key, new Queue<MonoBehaviour>(count));
                _capacity.Add(key, 0);
            }

            if (!_pools.TryGetValue(key, out var queue)) return;

            for (int i = 0; i < count; i++)
            {
                var item = Instantiate(t, parent);
                var marker = item.GetComponent<PoolItemMarker>();
                if (!marker) marker = item.gameObject.AddComponent<PoolItemMarker>();
                marker.PrefabID = key;

                // Clear any particles emitted during Instantiate (from PlayOnAwake)
                foreach (var ps in item.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ps.Clear();
                }

                item.gameObject.SetActive(false);
                queue.Enqueue(item);
            }
        }
    }

    public System.Collections.IEnumerator PreWarmAsync(MonoBehaviour t, int count, Transform parent = null, int customKey = 0, int itemsPerFrame = 5)
    {
        var key = customKey == 0 ? GetKey(t) : customKey;
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
            var marker = item.GetComponent<PoolItemMarker>();
            if (!marker) marker = item.gameObject.AddComponent<PoolItemMarker>();
            marker.PrefabID = key;

            // Clear any particles emitted during Instantiate (from PlayOnAwake)
            foreach (var ps in item.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Clear();
            }

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

    public void SetMaxSize(MonoBehaviour t, int size, int customKey = 0)
    {
        var key = customKey == 0 ? GetKey(t) : customKey;
        _capacity[key] = size;
    }

    public static int GetKey(MonoBehaviour t)
    {
        return t.gameObject.GetInstanceID();
    }

    public void DeSpawnByKey(MonoBehaviour t, int customKey = 0)
    {
        lock (_pools)
        {
            var key = customKey == 0 ? GetKey(t) : customKey;

            if (_activeObjects.TryGetValue(key, out var activeSet))
            {
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