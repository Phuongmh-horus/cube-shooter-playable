using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class UIFullScreenBlocker : MonoSingleton<UIFullScreenBlocker>
{
    [SerializeField] protected Canvas canvas;

    private void Reset()
    {
        if (!canvas) canvas = GetComponent<Canvas>();
    }

    private void OnEnable()
    {
        if (canvas) Unlock(-1);
    }

    HashSet<int> _lockKey = new();
    // Last key is 9
    public void Lock(int key = 0)
    {
        _lockKey.Add(key);
        Debug.Log($"Lock {key}");
        canvas.overrideSorting = true;
        canvas.enabled = true;
    }
    public void Unlock(int key = 0)
    {
        if (key == -1) _lockKey.Clear();
        _lockKey.Remove(key);
        Debug.Log($"Unlock {key}");
        if (_lockKey.Count > 0) return;
        canvas.enabled = false;
    }
}
