using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Interactable : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    private Vector2 _startPos;
    private Vector3 _startMousePosition; // Cached true screen pixel coordinate
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
        
        // Cache the actual Unity screen pixel position for physics raycasting.
        // On Luna WebGL, eventData.position can be offset by Canvas scaling or letterboxing!
#if !UNITY_EDITOR && UNITY_WEBGL
        if (Input.touchCount > 0)
            _startMousePosition = Input.GetTouch(0).position;
        else
            _startMousePosition = Input.mousePosition;
#else
        _startMousePosition = Input.mousePosition;
#endif

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

        if (_isHolding)
        {
            // Use _lastDragPosition to determine if it was a drag or a tap.
            float dragDistance = Vector2.Distance(_startPos, _lastDragPosition);
            
            if (dragDistance < 50f)
            {
                // Raycast from the true screen coordinate, NOT the UI Canvas coordinate!
                OnMouseTap(_startMousePosition);
                GameEventBus.OnMouseTap?.Invoke(_startMousePosition);
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
        
        // Update last drag position safely
        _lastDragPosition = eventData.position;

        if (delta.sqrMagnitude < 0.01f) return;

        _hasSwiped = true;
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

        RaycastHit[] hits = Physics.RaycastAll(CameraManager.Instance.MainCamera.ScreenPointToRay(mousePos));
        
        // Sort hits by distance to ensure we process the closest objects first
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hitInfo in hits)
        {
            var launcher = hitInfo.collider.GetComponentInParent<LauncherBaseMono>();

            if (launcher != null)
            {
                if (launcher.IsAtTopColumn())
                {
                    SoundManager.Instance?.PlayOneShot(AudioClipName.Pea_Selected);
                    GameEventBus.OnLauncherClicked?.Invoke(launcher);
                    LevelSystem.LevelHasBeenStarted = true;
                    break;
                }
            }
        }
    }

    #endregion
}