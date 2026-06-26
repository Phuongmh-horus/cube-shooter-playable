using UnityEngine;
using System.Collections;

public class LunaFireworksController : MonoBehaviour
{
    [Header("Particle Systems")]
    [Tooltip("The main Fireworks Particle System")]
    [SerializeField] private ParticleSystem _fireworks;
    [Tooltip("The Sparks Particle System")]
    [SerializeField] private ParticleSystem _sparks;


    [Header("Spawn Settings")]
    [Tooltip("Minimum time between each firework explosion")]
    [SerializeField] private float _minDelay = 0.3f; // ~3 times per sec
    [Tooltip("Maximum time between each firework explosion")]
    [SerializeField] private float _maxDelay = 0.2f;  // 5 times per sec

    [Tooltip("Number of particles for the main firework")]
    [SerializeField] private int _fireworksCount = 30;
    [Tooltip("Number of particles for the sparks")]
    [SerializeField] private int _sparksCount = 15;

    [Header("Area Settings")]
    [SerializeField] private Vector2 _spawnAreaWidth = new Vector2(-3f, 3f);
    [SerializeField] private Vector2 _spawnAreaHeight = new Vector2(-2f, 2f);

    private Coroutine _fireworksRoutine;
    private Vector3 _originPos;

    private void Awake()
    {
        _originPos = transform.position;

        // Force simulation space to World so particles don't follow the emitter when it moves
        if (_fireworks != null)
        {
            var main = _fireworks.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
        }
        if (_sparks != null)
        {
            var main = _sparks.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
        }
    }

    private void OnEnable()
    {
        StartFireworks();
    }

    private void OnDisable()
    {
        StopFireworks();
    }

    public void StartFireworks()
    {
        if (_fireworksRoutine != null) StopCoroutine(_fireworksRoutine);
        _fireworksRoutine = StartCoroutine(FireworksRoutine());
    }

    public void StopFireworks()
    {
        if (_fireworksRoutine != null)
        {
            StopCoroutine(_fireworksRoutine);
            _fireworksRoutine = null;
        }
    }

    private IEnumerator FireworksRoutine()
    {
        while (true)
        {
            // Random vị trí trong khoảng cấu hình
            Vector3 randomLocalPos = new Vector3(
                Random.Range(_spawnAreaWidth.x, _spawnAreaWidth.y),
                Random.Range(_spawnAreaHeight.x, _spawnAreaHeight.y),
                0f
            );

            // Tạo màu cầu vồng random dùng HSV (Hue random từ 0-1, S=1, V=1)
            Color randomColor = Color.HSVToRGB(Random.Range(0f, 1f), 1f, 1f);
            
            // Tính toán vị trí World dựa trên gốc
            Vector3 spawnWorldPos = _originPos + transform.TransformDirection(randomLocalPos);

            if (_fireworks != null)
            {
                _fireworks.transform.position = spawnWorldPos;
                var main = _fireworks.main;
                main.startColor = randomColor;
                _fireworks.Emit(_fireworksCount);
            }

            if (_sparks != null)
            {
                _sparks.transform.position = spawnWorldPos;
                var main = _sparks.main;
                main.startColor = randomColor;
                _sparks.Emit(_sparksCount);
            }
            
            // Random delay 5-7 lần 1 giây
            yield return new WaitForSeconds(Random.Range(_minDelay, _maxDelay));
        }
    }
}
