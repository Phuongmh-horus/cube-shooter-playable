using UnityEngine;

[CreateAssetMenu(fileName = "ScriptableObjects/RemoteConfigDataSO", menuName = "ScriptableObjects/RemoteConfigDataSO", order = 1)]
public class
    RemoteConfigDataSO : ScriptableObject
{
    public int LevelBreakMainMenu = 10;
    public int AutoPlayLevelLimit = 1;
    public int LevelRequireToRequestNotification = 15;
}