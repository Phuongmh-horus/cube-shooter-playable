using System.Collections;
using UnityEngine;

public class VfxBase : MonoBehaviour
{
    [SerializeField] private ParticleSystem _particleSystem;
    [SerializeField] private Transform _transform;
    private Coroutine _delayDespawnCoroutine;


    private void Awake()
    {
        _transform ??= transform;
        enabled = false;
    }

    public void OnInit(Vector3 pos)
    {
        _transform.position = pos;
        if (_particleSystem != null)
        {
            var allPs = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in allPs)
            {
                ps.Clear();
                ps.Play();
            }
        }
        enabled = true;
        gameObject.SetActive(true);
        if (_delayDespawnCoroutine != null) StopCoroutine(_delayDespawnCoroutine);
        _delayDespawnCoroutine = StartCoroutine(WaitAndDespawn());
    }

    private static readonly WaitForSeconds Wait025 = new WaitForSeconds(0.25f);

    private IEnumerator WaitAndDespawn()
    {
        while (_particleSystem != null && _particleSystem.IsAlive(true))
        {
            yield return Wait025;
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