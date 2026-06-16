using System;
using UnityEngine;

[Serializable]
public class SoundData
{
    public AudioClipName Name;
    public AudioClip Clip;
    public float VolumeDefault = 1f;
}

public enum AudioClipName
{
    None = 0,
    Ingame_BGM = 1,
    Menu_BGM = 2,
    Button_Click = 3,
    Game_Win = 4,
    Game_Lose = 5,
    Popup_Open = 6,
    Earn_Coin = 7,
    Coin_Fly_End = 8,
    Pea_Selected = 9,
    Shooter_Arrive = 10,
    Hidden_Shooter_Reveal = 11,
    Cube_Destroy = 12,
    Booster_Selected = 13,
    Bonus_Slot_Arrive = 14,
    Shooter_Picker = 15,
    Shuffle_Shooters = 16,
    Unlock_Shooter = 17,
}
