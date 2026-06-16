using System.Collections;
using UnityEngine;

public class CameraManager : MonoSingleton<CameraManager>
{
    public Transform ParentCameraMove;
    public Camera MainCamera;
    [Header("HightLine")]
    public Camera HightLineCamera;
    public GameObject HightLineCameraGameObject;
    [SerializeField] private Interactable_HightLine interactable_HightLine;

    [Header("Camera Config")]
    [SerializeField]
    private CameraConfig _cameraConfig;

    [Header("Booster Shooter Picker")]
    [SerializeField] private float _boosterCamOffsetY = -5f;
    [SerializeField] private float _camMoveDuration = 0.5f;

    [Header("Render Texture Debug")]
    [SerializeField] private RenderTexture rawImage;

    private Vector3 _defaultCamPosition;
    private Coroutine _camMoveCts;

    protected override void Awake()
    {
        base.Awake();
        if (rawImage != null)
        {
            rawImage.Release();
            rawImage.width = Screen.width;
            rawImage.height = Screen.height;
            rawImage.Create();

            if (HightLineCamera != null)
                HightLineCamera.targetTexture = rawImage;
        }
    }

    private void Start()
    {
        _defaultCamPosition = ParentCameraMove.position;
        SetCameraState(CameraState.GamePlayView, true);
    }

    private void OnDestroy()
    {
        if (_camMoveCts != null) StopCoroutine(_camMoveCts);

    }

    public void SetCameraState(CameraState state, bool immediate = false)
    {
        if (_cameraConfig == null || MainCamera == null) return;
        var configData = _cameraConfig.GetConfigData(state);
        if (configData == null) return;

        if (immediate)
        {
            if (_camMoveCts != null) StopCoroutine(_camMoveCts);

            _camMoveCts = null;

            MainCamera.transform.position = configData.CamPosition;
            MainCamera.transform.rotation = Quaternion.Euler(configData.CamRotation);
        }
        else
        {
            StartCoroutine(TransitionCameraTo(configData.CamPosition, configData.CamRotation, _cameraConfig.Speed));
        }
    }

    private IEnumerator TransitionCameraTo(Vector3 targetPos, Vector3 targetRot, float speed)
    {

        if (MainCamera == null) yield break;

        if (_camMoveCts != null) StopCoroutine(_camMoveCts);

        Vector3 startPos = MainCamera.transform.position;
        Quaternion startRot = MainCamera.transform.rotation;
        Quaternion endRot = Quaternion.Euler(targetRot);

        float duration = 1f / Mathf.Max(speed, 0.01f);
        float elapsed = 0f;

        while (elapsed < duration)
        {

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t); // SmoothStep

            MainCamera.transform.position = Vector3.Lerp(startPos, targetPos, t);
            MainCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, t);

            yield return null;
        }

        MainCamera.transform.position = targetPos;
        MainCamera.transform.rotation = endRot;
    }

    public void EnableHightLineCamera(bool enable, LayerNameGamePlay layerNameGamePlay)
    {
        if (enable)
            HightLineCamera.cullingMask = 1 << (int)layerNameGamePlay;
        HightLineCameraGameObject.SetActive(enable);
    }

    /// <summary>
    /// Move camera xuống theo trục Y để nhìn các launcher phía dưới
    /// </summary>
    public IEnumerator MoveCameraToBoosterView()
    {
        if (_camMoveCts != null) StopCoroutine(_camMoveCts);

        Vector3 targetPos = _defaultCamPosition;
        targetPos.y += _boosterCamOffsetY;

        yield return StartCoroutine(LerpCameraPosition(ParentCameraMove.position, targetPos, _camMoveDuration));
    }

    /// <summary>
    /// Move camera về vị trí mặc định
    /// </summary>
    public IEnumerator MoveCameraToDefaultView()
    {
        if (_camMoveCts != null) StopCoroutine(_camMoveCts);

        yield return StartCoroutine(LerpCameraPosition(ParentCameraMove.position, _defaultCamPosition, _camMoveDuration));
    }

    private IEnumerator LerpCameraPosition(Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t); // SmoothStep
            ParentCameraMove.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
        ParentCameraMove.position = to;
    }
}
