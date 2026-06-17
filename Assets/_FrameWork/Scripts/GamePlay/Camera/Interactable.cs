using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Interactable : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    private Vector2 _startPos;
    private Vector2 _lastDragPosition;
    private float _holdTime;
    private bool _isHolding;
    private bool _hasSwiped;
    private bool _canDrag;

    [Header("Settings")]
    public float HoldThreshold = 0.3f;
    public float SwipeThreshold = 50f;

    [Header("References")]
    [SerializeField] private Image interactableImage;
    [SerializeField] private Image dragZoneImage;
    [SerializeField] private Canvas parentCanvas;

    private bool _isLocked;

    #region ===== UNITY METHODS =====

    private void Awake()
    {
        _isLocked = false;
    }

    #endregion

    #region ===== IMPLEMENT INTERFACES =====

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_isLocked) return;

        _startPos = eventData.position;
        _holdTime = 0f;
        _isHolding = true;
        _hasSwiped = false;
        _canDrag = IsPointerInsideDragZone(eventData);
        _lastDragPosition = _startPos;

        GameEventBus.OnMouseDown?.Invoke(true);
        if (_canDrag)
            GameEventBus.OnStartDragAction?.Invoke(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_isLocked) return;

        if (_isHolding && !_hasSwiped)
        {
            float totalTime = _holdTime;

            if (totalTime < HoldThreshold &&
                Vector3.Distance(_startPos, eventData.position) < SwipeThreshold)
            {
                OnMouseTap(eventData.position);
                GameEventBus.OnMouseTap?.Invoke(eventData.position);
            }
        }

        GameEventBus.OnMouseDown?.Invoke(false);
        GameEventBus.OnStartDragAction?.Invoke(false);

        _isHolding = false;
        _canDrag = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_canDrag) return;

        Vector2 delta = eventData.delta;
        if (delta.sqrMagnitude < 0.01f) return;

        _hasSwiped = true;
        _lastDragPosition = eventData.position;

        GameEventBus.OnDragAction?.Invoke(delta);
    }

    #endregion

    #region ===== HELPER METHODS =====

    private bool IsPointerInsideDragZone(PointerEventData eventData)
    {
        if (dragZoneImage == null) return false;

        return RectTransformUtility.RectangleContainsScreenPoint(
            dragZoneImage.rectTransform,
            eventData.position,
            eventData.pressEventCamera
        );
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

        if (Physics.Raycast(CameraManager.Instance.MainCamera.ScreenPointToRay(mousePos), out RaycastHit hitInfo))
        {
            var launcher = hitInfo.collider.GetComponentInParent<LauncherBaseMono>();

            if (launcher != null)
            {
                if (launcher.IsAtTopColumn())
                {
                    SoundManager.Instance?.PlayOneShot(AudioClipName.Pea_Selected);
                    GameEventBus.OnLauncherClicked?.Invoke(launcher);
                    LevelSystem.LevelHasBeenStarted = true;
                }
            }
        }
    }

    #endregion
}