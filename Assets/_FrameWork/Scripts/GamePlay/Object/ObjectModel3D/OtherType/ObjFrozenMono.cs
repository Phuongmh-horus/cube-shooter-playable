public class ObjFrozenMono : ObjectBaseMono
{
    private FrozenCubeData _data;

    public override CubeShooterColor GetColor() { return CubeShooterColor.Snow; }

    public override void OnInit(ObjectBaseData data)
    {
        base.OnInit(data);
        _tF.localEulerAngles = data.Rotation;
        _tF.localScale = data.Scale;
        _data = data as FrozenCubeData;
    }

    public void OnFrozen()
    {


    }
}

public interface IFrozenCube
{
    public void OnFrozen();
}