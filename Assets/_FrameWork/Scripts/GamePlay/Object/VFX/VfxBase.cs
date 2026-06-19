using System.Collections;
using UnityEngine;

public class VfxBase : MonoBehaviour
{
    [Header("VFX Particle Systems")]
    [SerializeField] private ParticleSystem _particleSystem;
    [SerializeField] private float _timeDespawn;
    [SerializeField] private Transform _tf;

    private Coroutine _despawnCoroutine;

    private void Awake()
    {
        _tf ??= transform;
    }

    public void OnInit(Vector3 pos)
    {
        _tf.position = pos;
        _particleSystem.Play();

        if (_despawnCoroutine != null)
            StopCoroutine(_despawnCoroutine);
        _despawnCoroutine = StartCoroutine(DespawnAfterSeconds(_timeDespawn));
    }

    private IEnumerator DespawnAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        OnDespawn();
    }

    public void OnDespawn()
    {
        if (_despawnCoroutine != null)
        {
            StopCoroutine(_despawnCoroutine);
            _despawnCoroutine = null;
        }
        PoolHolder.Instance.Release(this);
    }
}