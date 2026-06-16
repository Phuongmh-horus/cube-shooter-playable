using UnityEngine;

[SelectionBase]
public abstract class ObjectBaseMono : MonoBehaviour
{
    #region <========================= PROPERTY & FIELD =========================>
    protected Transform _tF;
    protected bool isBulletIncoming = false;
    protected bool CanDespawn = false;

    #endregion

    #region <========================= GET & SET =========================>

    public Transform TF => _tF;
    public bool IsBulletIncoming => isBulletIncoming;
    public Vector3 GetPosition() => _tF.localPosition;
    public Vector3 GetRotation() => _tF.localEulerAngles;
    public void OnBulletInComming() => isBulletIncoming = true;

    #endregion


    #region <========================= ABSTRACT CLASS =========================>

    public abstract CubeShooterColor GetColor();

    public virtual void OnInit(ObjectBaseData piece)
    {
        _tF ??= transform;
        _tF.localPosition = piece.Position;
        gameObject.SetActive(true);
    }

    #endregion

    public virtual void OnDespawn()
    {
        if (LevelSystem.Instance != null && LevelSystem.Instance.Model3DController != null)
        {
            LevelSystem.Instance.Model3DController.RemovePiece(this);
        }
    }

}
