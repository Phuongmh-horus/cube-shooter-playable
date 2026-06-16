using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameEventBus
{
    #region New Level

    public static Action OnNewLevel;
    public static Action OnWinGame;

    #endregion

    #region SoundManager

    public static Action<float> OnChangeSound;
    public static Action<float> OnChangeSoundFx;

    #endregion

    #region HelperPack

    public static Action ActiveHelperItem;

    #endregion

    #region Game Play
    public static Action OnLoadLevelDone;
    public static Action Match3Suscess;
    public static Action<Action> OnReceiveBooster;

    public static Action<LauncherBaseMono> OnLauncherClicked;
    public static Action AssignLauncher;
    public static Action<bool> BlockLauncherShoot;
    public static Action<bool> ActiveAutoRotationModel;

    public static List<Action> ACLauncherShoot = new List<Action>();
    public static List<Action> ACDespawnLauncherProjectile = new List<Action>();
    #endregion

    #region INTERACTABLE EVENT 

    public static Action<bool> OnMouseDown;
    public static Action<Vector2> OnMouseTap;
    public static Action<Vector2> OnShowTapEffect; // Delegate riêng cho hiệu ứng Tap
    public static Action<Vector2> OnDragAction;
    public static Action<bool> OnStartDragAction; // NEW: Gọi khi ấn trúng vùng dragZone hợp lệ
    public static Action<bool> OnActiveInputGameplay; // để tắt lúc endgame ra menu ật khi load level, để không bị chặn UI 

    #endregion
}