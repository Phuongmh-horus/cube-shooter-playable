public class ObjLargeCubeMono : ObjectBaseMono
{
    private LargeCubeData _data;

    public override CubeShooterColor GetColor() => CubeShooterColor.Snow;

    public override void OnInit(ObjectBaseData data)
    {
        base.OnInit(data);
        _tF.localEulerAngles = data.Rotation;
        _tF.localScale = data.Scale;
        _data = data as LargeCubeData;
    }
}