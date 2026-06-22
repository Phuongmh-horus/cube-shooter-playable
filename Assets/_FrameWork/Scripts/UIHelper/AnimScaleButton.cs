using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AnimScaleButton : MonoBehaviour
{
    [SerializeField] private Button Button;
    [SerializeField] private TMP_Text ButtonText;
    [SerializeField] private Image ButtonImage;

    [Header("Button]")]
    [SerializeField] private bool animateButton = true;
    [SerializeField] private Vector3 buttonMinScale = Vector3.one;
    [SerializeField] private Vector3 buttonMaxScale = new Vector3(1.2f, 1.2f, 1.2f);

    [Header("Text")]
    [SerializeField] private bool animateText = true;
    [SerializeField] private Vector3 textMinScale = Vector3.one;
    [SerializeField] private Vector3 textMaxScale = new Vector3(1.1f, 1.1f, 1.1f);

    [Header("Image")]
    [SerializeField] private bool animateImage = true;
    [SerializeField] private Vector3 imageMinScale = Vector3.one;
    [SerializeField] private Vector3 imageMaxScale = new Vector3(1.1f, 1.1f, 1.1f);

    [SerializeField] private float scaleDuration = 0.5f;
    [SerializeField] private bool m_autoStart = true;
    private Tween buttonTween;
    private Tween textTween;
    private Tween imageTween;

    private void Start()
    {
        if (Button != null)
            Button.onClick.AddListener(GotoStore);

        if (m_autoStart)
            StartScalingAnimation();
    }

    private void OnDestroy()
    {
        if (Button != null)
            Button.onClick.RemoveListener(GotoStore);

        buttonTween?.Kill();
        textTween?.Kill();
        imageTween?.Kill();
    }

    public void GotoStore()
    {
        Luna.Unity.LifeCycle.GameEnded();
        Luna.Unity.Playable.InstallFullGame();
    }

    public void StartScalingAnimation()
    {
        if (animateButton && Button != null)
        {
            Button.transform.localScale = buttonMinScale;

            buttonTween = Button.transform
                .DOScale(buttonMaxScale, scaleDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        if (animateText && ButtonText != null)
        {
            ButtonText.transform.localScale = textMinScale;

            textTween = ButtonText.transform
                .DOScale(textMaxScale, scaleDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        if (animateImage && ButtonImage != null)
        {
            ButtonImage.transform.localScale = imageMinScale;

            imageTween = ButtonImage.transform
                .DOScale(imageMaxScale, scaleDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }
    }

    public void StopScalingAnimation()
    {
        buttonTween?.Kill();
        textTween?.Kill();
        imageTween?.Kill();
    }
}
