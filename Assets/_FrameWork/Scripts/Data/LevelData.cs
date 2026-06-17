using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public partial class LevelContainer
{
    public int LevelDataVersion; // Phiên bản của LevelData, dùng để kiểm tra tương thích khi load 
    public List<int> LevelIds; // Danh sách ID của các level, có thể dùng để hiển thị danh sách level trong menu hoặc load level theo ID
    public List<LevelReward> LevelReward; // Phần thưởng chung cho tất cả level
}

[System.Serializable]
public partial class LevelInforOld
{
    public int LevelId;
    public LevelType LevelType;
    public string RoundName;
    public string IconPath;
}

[System.Serializable]
public partial class LevelInfor
{
    public int LevelId; // ID của level
    public LevelType LevelType;

    public string RoundName; // tên màn chơi

    public string IconPath; // Đường dẫn đến icon của level, có thể là Resources path hoặc URL

    public float Scale = 1f; // Tỉ lệ Scale mới thêm vào
    public Vector3 ModelRotation = UnityEngine.Vector3.zero; // Góc xoay ban đầu của Model
}

[System.Serializable]
public partial class LevelReward
{
    public LevelType LevelType; // Loại level để xác định phần thưởng
    public int GoldReward; // Số vàng thưởng cho leveltype này
    public int GoldRewardAds; // Số vàng thưởng cho leveltype này khi xem quảng cáo
}

public enum LevelType
{
    None = 0,
    Normal = 1,
    Medium = 2,
    Hard = 3,
    Extreme = 4
}