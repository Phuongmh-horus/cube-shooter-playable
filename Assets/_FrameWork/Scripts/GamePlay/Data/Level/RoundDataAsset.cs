using UnityEngine;

[CreateAssetMenu(fileName = "RoundDataAsset", menuName = "GamePlay/Level Data Asset")]
public class RoundDataAsset : ScriptableObject
{
    [Header("Level Info")]
    public int LevelId;
    public string RoundName;
    public LevelType LevelType;
    public string IconPath;
    public float Scale = 1f;
    public Vector3 ModelRotation = Vector3.zero;

    [Header("Raw Data")]
    [Tooltip("Dùng cho game thật (MemoryPack)")]
    public TextAsset LevelDataBytesAsset;

    [Tooltip("Dùng cho Playable Ads Luna (JsonUtility)")]
    public TextAsset LevelDataJsonAsset;

    [TextArea(3, 10)]
    public string Notes;

    public RoundDataBytes GetLevelData()
    {
        // Ưu tiên dùng JSON cho Playable Ads vì MemoryPack không tương thích tốt với Luna/Bridge.NET
        if (LevelDataJsonAsset != null && !string.IsNullOrEmpty(LevelDataJsonAsset.text))
        {
            try
            {
                var data = JsonUtility.FromJson<RoundDataBytes>(LevelDataJsonAsset.text);
                Debug.Log($"[RoundDataAsset] Loaded data from JSON for LevelID {LevelId}");
                return data;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[RoundDataAsset] Failed to deserialize JSON LevelID {LevelId}: {ex.Message}");
            }
        }
        return null;
    }
}
