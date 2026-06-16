using UnityEngine;

[CreateAssetMenu(fileName = "SlotAppearanceConfigSO", menuName = "Config/SlotAppearanceConfigSO")]
public class SlotAppearanceConfigSO : ScriptableObject
{
    [Header("Jump Appearance Settings")]
    public float JumpDuration = 0.5f;
    public float JumpHeight = 2f;
    public float JumpOffset = 15f;
    public float StepDelay = 0.1f;
}
