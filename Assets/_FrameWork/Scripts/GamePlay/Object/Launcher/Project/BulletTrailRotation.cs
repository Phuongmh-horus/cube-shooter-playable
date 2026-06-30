using UnityEngine;
using System.Collections.Generic;

public class BulletTrailRotation : MonoBehaviour
{
    private Vector3 _lastPosition;
    private Transform _tf;

    // Quản lý tập trung để tránh gọi LateUpdate hàng loạt
    public static readonly List<BulletTrailRotation> ActiveTrails = new List<BulletTrailRotation>(100);

    private void Awake()
    {
        _tf = transform;
    }

    private void OnEnable()
    {
        if (_tf != null)
        {
            _tf.localRotation = Quaternion.identity;
            _lastPosition = _tf.position;
        }
        _isTeleported = true;
        if (!ActiveTrails.Contains(this))
        {
            ActiveTrails.Add(this);
        }
    }

    private void OnDisable()
    {
        int idx = ActiveTrails.IndexOf(this);
        if (idx >= 0)
        {
            int lastIdx = ActiveTrails.Count - 1;
            ActiveTrails[idx] = ActiveTrails[lastIdx];
            ActiveTrails.RemoveAt(lastIdx);
        }
    }

    private bool _isTeleported = true;

    // Hàm update thủ công
    public void ManualUpdate()
    {
        if (_tf == null) return;

        Vector3 currentPosition = _tf.position;
        Vector3 direction = currentPosition - _lastPosition;

        if (!_isTeleported && direction.sqrMagnitude > 0.0001f)
        {
            //_tf.forward = direction.normalized;
            _tf.up = direction.normalized; // Đổi dòng này nếu Sprite vẽ dọc theo trục Y
        }

        _lastPosition = currentPosition;
        _isTeleported = false;
    }

    public void ResetPosition(Vector3 newPos)
    {
        if (_tf != null)
        {
            _tf.position = newPos;
            _lastPosition = newPos;
        }
        else
        {
            _lastPosition = newPos;
        }
        _isTeleported = true;
    }

    // Static function để update một cục, tối ưu cho WebGL/Luna
    public static void UpdateAllTrails()
    {
        for (int i = ActiveTrails.Count - 1; i >= 0; i--)
        {
            var trail = ActiveTrails[i];
            if (trail == null || !trail.gameObject.activeInHierarchy)
            {
                int lastIdx = ActiveTrails.Count - 1;
                ActiveTrails[i] = ActiveTrails[lastIdx];
                ActiveTrails.RemoveAt(lastIdx);
                continue;
            }
            trail.ManualUpdate();
        }
    }
}
