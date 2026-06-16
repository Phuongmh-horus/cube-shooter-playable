public class GamePlayManager : MonoSingleton<GamePlayManager>
{
    #region CONTROL

    public static bool INTRO_FADEIN_FADEOUT;
    public static bool SHOW_STARTER_PACK_CONDITION;

    #endregion

    protected override void Awake()
    {
        base.Awake();
    }

    private void OnDestroy()
    {
    }
}
