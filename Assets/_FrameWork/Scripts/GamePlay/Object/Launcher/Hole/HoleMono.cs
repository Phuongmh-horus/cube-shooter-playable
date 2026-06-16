using UnityEngine;

public class HoleMono : MonoBehaviour
{
    [Header("Model Sides")]
    [SerializeField] private GameObject _modelLeft;
    [SerializeField] private GameObject _modelRight;
    [SerializeField] private GameObject _modelCenter;

    public void OnInit(Vector3 spawnPos, ModelSide side = ModelSide.Center)
    {
        transform.position = spawnPos;
        SetModelSide(side);
        gameObject.SetActive(true);
    }

    private void SetModelSide(ModelSide side)
    {
        if (_modelLeft != null) _modelLeft.SetActive(side == ModelSide.Left);
        if (_modelRight != null) _modelRight.SetActive(side == ModelSide.Right);
        if (_modelCenter != null) _modelCenter.SetActive(side == ModelSide.Center);
    }

    public void OnDespawn()
    {
        Destroy(gameObject);
        //gameObject.SetActive(false);
        //PoolHolder.Instance.Release(this);
    }
}
