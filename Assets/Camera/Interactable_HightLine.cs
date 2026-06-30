using UnityEngine;
using UnityEngine.EventSystems;

public class Interactable_HightLine : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Vector2 _startPos;
    private float _holdTime;
    private bool _isHolding;
    private bool _hasSwiped;

    [Header("Settings")] public float HoldThreshold = 0.3f; // Giữ bao lâu thì tính là hold
    public float SwipeThreshold = 100f; // Vuốt ít nhất bao xa để tính là swipe

    #region ===== IMPLEMENT INTERFACES =====

    public void OnPointerDown(PointerEventData eventData)
    {
        _startPos = eventData.position;
        _holdTime = 0f;
        _isHolding = true;
        _hasSwiped = false;
        // GameEventBus.OnMouseDown?.Invoke(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_isHolding && !_hasSwiped)
        {
            float totalTime = _holdTime;

            if (totalTime < HoldThreshold && Vector3.Distance(_startPos, eventData.position) < SwipeThreshold)
            {
                // Tap
                OnMouseTap(eventData.position);
            }
        }
        // GameEventBus.OnMouseDown?.Invoke(false);
        _isHolding = false;
    }

    #endregion

    #region ===== EVENT METHODS =====

    private void OnMouseTap(Vector2 mousePos)
    {
        if (CameraManager.Instance.MainCamera == null)
        {
            Debug.Log("<color=red>Camera Main is null, IB DEV please!</color>");
            return;
        }
        int layerMask = ~((1 << 2) | (1 << 5)); // Ignore Raycast and UI
        if (Physics.Raycast(CameraManager.Instance.MainCamera.ScreenPointToRay(mousePos), out RaycastHit hitInfo, Mathf.Infinity, layerMask))
        {
            var launcher = hitInfo.collider.GetComponentInParent<LauncherBaseMono>();
            if (launcher != null && launcher.IsAtTopColumn())
            {
                SoundManager.Instance?.PlayOneShot(AudioClipName.Pea_Selected);
                GameEventBus.OnLauncherClicked?.Invoke(launcher);
                LevelSystem.LevelHasBeenStarted = true;
            }
        }
    }

    #endregion
}
