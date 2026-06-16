using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Random = UnityEngine.Random;

public class SoundManager : MonoSingleton<SoundManager>
{
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private AudioSource bgMusicSource;
    [SerializeField] private AudioSource fxMusicSource;
    [SerializeField] private AudioSource inGameMusicSource;

    [SerializeField] public SoundDataSO soundDataSO;
    [SerializeField] public List<AudioClip> backgroundMusics;

    private void OnValidate()
    {
        if (audioMixer == null)
        {
            var audioMixers = Resources.FindObjectsOfTypeAll<AudioMixer>();
            if (audioMixers.Length > 0) audioMixer = audioMixers[0];
        }

        if (soundDataSO == null)
        {
            var soundDataSOs = Resources.FindObjectsOfTypeAll<SoundDataSO>();
            if (soundDataSOs.Length > 0) soundDataSO = soundDataSOs[0];
        }
    }

    protected override void Awake()
    {
        base.Awake();
        GameEventBus.OnChangeSound = (OnSoundChange);
        GameEventBus.OnChangeSoundFx = (OnSoundFxChange);
    }

    private void Start()
    {
        OnSoundChange(1f);
        OnSoundFxChange(1f);
    }

    private bool _isLoopRandomBGM;
    private void Update()
    {
        if (backgroundMusics.Count <= 0)
        {
            return;
        }
        if (_isLoopRandomBGM && !bgMusicSource.isPlaying)
        {
            AudioClip clip = RandomBGM();
            bgMusicSource.clip = clip;
            bgMusicSource.Play();
        }
    }
    public void StopBackGroundMusic()
    {
        _isLoopRandomBGM = false;
        bgMusicSource.Stop();
    }

    public void PlayBackGroundMusic()
    {
        StopInGameMusic();
        _isLoopRandomBGM = true;
    }

    private List<AudioClip> _backgroundMusicTemp = new();
    private AudioClip RandomBGM()
    {
        if (_backgroundMusicTemp.Count == 0)
        {
            _backgroundMusicTemp.AddRange(backgroundMusics);
        }
        var result = _backgroundMusicTemp[Random.Range(0, _backgroundMusicTemp.Count)];
        _backgroundMusicTemp.Remove(result);
        return result;
    }

    private void OnSoundChange(float currentValue)
    {
        if (audioMixer == null) return;
        float soundValue = currentValue == 0 ? -100 : Mathf.Log10(currentValue) * 20;
        var parameterName = Enum.GetName(typeof(SoundMixerGroup), SoundMixerGroup.BGMusic);
        var checkSet = audioMixer.SetFloat(parameterName, soundValue);
#if UNITY_EDITOR
        if (!checkSet) Debug.LogError($"không set được giá trị audio mixer với parameter {parameterName}");
#endif
    }
    private void OnSoundFxChange(float currentValue)
    {
        if (audioMixer == null) return;
        float soundValue = currentValue == 0 ? -100 : Mathf.Log10(currentValue) * 20;
        var parameterName = Enum.GetName(typeof(SoundMixerGroup), SoundMixerGroup.SoundFx);
        var checkSet = audioMixer.SetFloat(parameterName, soundValue);
#if UNITY_EDITOR
        if (!checkSet) Debug.LogError($"không set được giá trị audio mixer với parameter {parameterName}");
#endif
    }

    /// <summary>
    /// Hàm phát một âm thanh với Mixer là Sound
    /// </summary>
    /// <param name="clip"></param>
    /// <param name="volume"></param>
    public void PlayOneShot(AudioClip clip, float volume = 1f)
    {
        fxMusicSource.PlayOneShot(clip, volume);
    }

    public void PlayOneShot(AudioClipName clipName)
    {
        var soundData = soundDataSO.GetSoundData(clipName);
        if (soundData == null) return;
        fxMusicSource.PlayOneShot(soundData.Clip, soundData.VolumeDefault);
    }
    public void StopOneShot()
    {
        fxMusicSource.Stop();
    }
    public void StopInGameMusic()
    {
        inGameMusicSource.Stop();
    }

    public void PlayInGameMusic()
    {
        StopBackGroundMusic();
        inGameMusicSource.loop = true;
        inGameMusicSource.Play();
    }

}
// đặt theo exposed Parameters trong audio mixer
public enum SoundMixerGroup
{
    BGMusic,
    SoundFx,
}