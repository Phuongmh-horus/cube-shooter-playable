using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public partial class RoundDataBytes
{
    public ModelData ModelData;
    public ListVerticalLauncherData ListVerticalLauncherData;

    public RoundDataBytes()
    {
        ModelData = new ModelData();
        ListVerticalLauncherData = new ListVerticalLauncherData();
    }
}

#region PIECE LAUCHER DATA
[System.Serializable]
public partial class ListVerticalLauncherData
{
    public List<VerticalLauncherData> VerticalLauncherDatas = new();

    public ListVerticalLauncherData()
    {
        VerticalLauncherDatas = new List<VerticalLauncherData>();
    }
}

[System.Serializable]
public partial class VerticalLauncherData
{
    public List<LauncherData> LauncherDataDatas = new();

    public VerticalLauncherData()
    {
        LauncherDataDatas = new();
    }
}

[System.Serializable]
public partial class LauncherData
{
    public int ID = -1; // ID của súng, -1 là chưa gán
    public LauncherMode LauncherMode;

    [Header("Màu sắc và đạn")]
    public List<ColorAndBulletAmound> ColorAndBulletAmound; // Danh sách màu sắc và số lượng đạn tương ứng mà súng này có thể bắn

    [Header("Trạng thái súng")]
    public int Frozened; // 0 là không khóa, khác 0 là số lượng súng được bay lên Slot để mở khóa súng này
    public bool Hidden; // Ẩn thông tin màu sắc và số đạn khỏi người chơi

    [Header("Kết nối")]
    public List<int> ConnectedReferencesIDs; // Danh sách ID của các súng khác được kết nối với súng này

    public LauncherData()
    {
        ID = -1;
        ConnectedReferencesIDs = new List<int>();
    }

    public LauncherData(LauncherData _data)
    {
        ID = _data.ID;
        ColorAndBulletAmound = new List<ColorAndBulletAmound>(_data.ColorAndBulletAmound);
        Frozened = _data.Frozened;
        Hidden = _data.Hidden;
        ConnectedReferencesIDs = new List<int>(_data.ConnectedReferencesIDs);
    }
}

[System.Serializable]
public partial class ColorAndBulletAmound
{
    public CubeShooterColor ColorCode; // Mã màu mà súng này có thể thu thập
    public int Amount; // Số lượng đạn, mỗi viên thu thập được 1 màu
}

public enum LauncherMode
{
    Normal = 0, // cái bắn bình thường
    Lock = 1, // Ổ khóa
    Key = 2, // chìa khóa
    Scissors = 3, // Kéo
}
#endregion

#region MODEL 3D
[System.Serializable]
public partial class ModelData
{
    public List<PieceData> Pieces = new();
    public List<GiftBoxData> GiftBoxes = new();
    public List<FrozenCubeData> FrozenCubes = new();
    public List<LargeCubeData> LargeCubes = new();

    public ModelData()
    {
        Pieces = new List<PieceData>();
    }
}

[System.Serializable]
public struct LunaVector3
{
    public float x;
    public float y;
    public float z;

    public LunaVector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    
    public static implicit operator Vector3(LunaVector3 v) => new Vector3(v.x, v.y, v.z);
    public static implicit operator LunaVector3(Vector3 v) => new LunaVector3(v.x, v.y, v.z);
}

[System.Serializable]
public partial class ObjectBaseData
{
    public LunaVector3 Position;
    public LunaVector3 Rotation;
    public LunaVector3 Scale;
}


[System.Serializable]
public partial class PieceData : ObjectBaseData
{
    public CubeShooterColor Color;

    public PieceData()
    {
        Color = CubeShooterColor.Snow;
    }
}

[System.Serializable]
public struct LunaVector2
{
    public float x;
    public float y;

    public LunaVector2(float x, float y) { this.x = x; this.y = y; }
    
    public static implicit operator Vector2(LunaVector2 v) => new Vector2(v.x, v.y);
    public static implicit operator LunaVector2(Vector2 v) => new LunaVector2(v.x, v.y);
}

[System.Serializable]
public partial class GiftBoxData : ObjectBaseData
{
    public int CountUnlockGiftBox; // Số lượng kéo cần bay lên để mở hộp quà này
    public List<LunaVector2> ScissorsFlyPoints; // List điểm để kéo bay lên cắt 

    public GiftBoxData()
    {
        ScissorsFlyPoints = new List<LunaVector2>();
    }
}

[System.Serializable]
public partial class FrozenCubeData : ObjectBaseData
{
    public int CountUnlockFrozenCube; // Số lượng đạn bay vào để vỡ Fozen

    public FrozenCubeData()
    {
    }
}

[System.Serializable]
public partial class LargeCubeData : ObjectBaseData
{
    public int CountUnlockLargeCube; // Số lượng đạn bay vào để vỡ LargeCube

    public LargeCubeData()
    {
    }
}
#endregion
