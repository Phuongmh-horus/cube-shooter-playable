using UnityEngine;
using UnityEngine.UI;

public class PlayableAdsUIController : MonoBehaviour
{
    public static PlayableAdsUIController Instance { get; private set; }

    public GameObject GamePlayTutObject;
    public GameObject EndcardPanelObject;
    public GameObject FireworksVFXObject;
    public Button PlayNowButton;
    public Image DimScreenImage;

    private bool _hasStarted = false;
    private bool _isShowingEndcard = false;

    public bool IsShowingEndcard => _isShowingEndcard;

    private void Awake()
    {
        Instance = this;

        // Bật màn hình hướng dẫn ban đầu
        if (GamePlayTutObject != null)
        {
            GamePlayTutObject.SetActive(true);
        }

        if (PlayNowButton != null)
        {
            PlayNowButton.gameObject.SetActive(true);
            PlayNowButton.onClick.AddListener(RedirectToStore);
        }

        // Tắt Endcard và Dim đi, chờ điều kiện mới bật
        if (EndcardPanelObject != null)
        {
            EndcardPanelObject.SetActive(false);
        }

        if (FireworksVFXObject != null)
        {
            FireworksVFXObject.SetActive(false);
        }
        if (DimScreenImage != null)
        {
            DimScreenImage.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // Ẩn Tutorial ngay khi user chạm vào màn hình lần đầu tiên
        if (!_hasStarted && Input.GetMouseButtonDown(0))
        {
            _hasStarted = true;
            if (GamePlayTutObject != null)
            {
                GamePlayTutObject.SetActive(false);
            }
        }
        // Nhấn vào bất kỳ đâu khi đang hiện Endcard sẽ mở Store
        else if (_isShowingEndcard && Input.GetMouseButtonDown(0))
        {
            RedirectToStore();
        }
    }

    public void ShowEndcard()
    {
        _isShowingEndcard = true;

        // Đảm bảo game trở về hoặc giữ nguyên tốc độ xoay ban đầu (không bị tua nhanh khi Win)
        LevelSystem.Instance.ChangeSpeedGame(false);

        // KHÔNG dùng GameEventBus.BlockLauncherShoot?.Invoke(true) 
        // vì các súng ĐÃ ở trên slot vẫn tiếp tục bắn đến khi hết đạn.

        // Chỉ chặn người chơi tương tác (click chọn súng mới hoặc xoay khối)
        GameEventBus.OnActiveInputGameplay?.Invoke(false);

        // Kích hoạt Dim Screen nền đen nếu có
        if (DimScreenImage != null)
        {
            DimScreenImage.gameObject.SetActive(true);
        }

        if (FireworksVFXObject != null)
        {
            FireworksVFXObject.SetActive(true);
        }

        // Bật panel có sẵn trong scene
        if (EndcardPanelObject != null)
        {
            EndcardPanelObject.SetActive(true);
            SoundManager.Instance.PlayOneShot(AudioClipName.Game_Win);

        }
        else
        {
            Debug.LogWarning("[PlayableAdsUIController] Bạn chưa kéo Endcard Panel vào Inspector!");
        }
    }

    private void RedirectToStore()
    {
#if LUNA_PLAYABLE
        try
        {
            Luna.Unity.Playable.InstallFullGame();
        }
        catch { }
#else
        try
        {
            System.Type lunaLifeCycle = System.Type.GetType("Luna.Unity.LifeCycle, Luna.Unity");
            if (lunaLifeCycle != null)
            {
                var method = lunaLifeCycle.GetMethod("TryInstallFullGame", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                method?.Invoke(null, null);
            }
        }
        catch { }
#endif
    }
}
