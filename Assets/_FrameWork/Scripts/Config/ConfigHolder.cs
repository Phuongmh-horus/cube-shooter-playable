using UnityEngine;

public class ConfigHolder : MonoSingleton<ConfigHolder>
{
    public ColorPallete ColorPallete_ForPiece;
    public ColorPallete ColorPallete_ForLauncher;

    [Header("Game Play")]
    public LauncherConfigSO LauncherConfigSo;
    public SlotLauncherConfigSO SlotLauncherConfigSo;
    public PrefabsDataConfigSO PrefabsDataConfigSO;
    public EndGameConfigSO EndGameConfigSo;

    public SlotAppearanceConfigSO SlotAppearanceConfigSO;
    public PieceConfigSO PieceConfigSO;

}
