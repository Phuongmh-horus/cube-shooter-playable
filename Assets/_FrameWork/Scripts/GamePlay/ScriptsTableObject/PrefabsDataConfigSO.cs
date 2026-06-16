using UnityEngine;

[CreateAssetMenu(fileName = "PrefabsDataConfigSO", menuName = "GamePlay/PrefabsDataConfigSO")]
public class PrefabsDataConfigSO : ScriptableObject
{
    [Header("Cột súng")]
    public VerticalLauncherMono VerticalLauncherPrefab;
    [Header("Tường bao quanh")]
    public WallControl WallAroundPrefab;
    [Space]
    [Header("Súng")]
    public LauncherNormalMono LauncherNormalPrefab;
    public LauncherLockMono LauncherLockPrefab;
    public LauncherKeyMono LauncherKeyPrefab;
    public LauncherScissorsMono LauncherScissorsPrefab;
    [Space]
    [Header("Dây kết nối súng")]
    public LineConnectorMono LineConnectorPrefab;
    [Space]
    [Header("Đạn súng")]
    public LauncherProjectile LauncherProjectilePrefab;
    [Space]
    [Header("Chỗ chứa súng")]
    public SlotLauncherMono SlotLauncherMonoPrefab;
    public SlotLauncherMono SlotBoosterMonoPrefab;
    [Space]
    [Header("Object của vậttheereer 3D")]
    public ObjPieceMono ObjectPiecePrefab;
    public ObjFrozenMono ObjectFrozenPrefab;
    public ObjGiftBoxMono ObjectGiftBoxPrefab;
    public ObjLargeCubeMono ObjectLargeCubePrefab;
}