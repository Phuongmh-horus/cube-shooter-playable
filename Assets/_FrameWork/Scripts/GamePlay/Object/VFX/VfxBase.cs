using System.Collections;
using UnityEngine;

public class VfxBase : MonoBehaviour
{
    [SerializeField] private ParticleSystem _particleSystem;
    [SerializeField] private Transform _transform;

    private void Awake()
    {
        _transform ??= transform;
        enabled = false;
    }

    public void OnInit(Vector3 pos)
    {
        _transform.position = pos;
        _particleSystem.Play();
        StartCoroutine(WaitAndDespawn());
    }

    private IEnumerator WaitAndDespawn()
    {
        while (_particleSystem != null && _particleSystem.IsAlive(true))
        {
            yield return new WaitForSeconds(0.25f);
        }
        OnDespawn();
    }
    public void OnDespawn()
    {
        enabled = false;
        if (_particleSystem != null)
            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        PoolHolder.Instance.Release(this);
    }
}