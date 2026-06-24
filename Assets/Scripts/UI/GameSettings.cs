using System;
using UnityEngine;

public static class GameSettings
{
    private const string MasterVolumeKey = "Settings.MasterVolume";
    private const string MusicVolumeKey = "Settings.MusicVolume";
    private const string SfxVolumeKey = "Settings.SfxVolume";
    private const string AssistModeKey = "Settings.AssistMode";

    private static bool loaded;
    private static float masterVolume = 1f;
    private static float musicVolume = 1f;
    private static float sfxVolume = 1f;
    private static bool assistMode;

    public static event Action OnChanged;

    public static float MasterVolume
    {
        get
        {
            EnsureLoaded();
            return masterVolume;
        }
        set => SetFloat(ref masterVolume, MasterVolumeKey, value, applyAudio: true);
    }

    public static float MusicVolume
    {
        get
        {
            EnsureLoaded();
            return musicVolume;
        }
        set => SetFloat(ref musicVolume, MusicVolumeKey, value, applyAudio: false);
    }

    public static float SfxVolume
    {
        get
        {
            EnsureLoaded();
            return sfxVolume;
        }
        set => SetFloat(ref sfxVolume, SfxVolumeKey, value, applyAudio: false);
    }

    public static bool AssistMode
    {
        get
        {
            EnsureLoaded();
            return assistMode;
        }
        set
        {
            EnsureLoaded();
            if (assistMode == value)
            {
                return;
            }

            assistMode = value;
            PlayerPrefs.SetInt(AssistModeKey, assistMode ? 1 : 0);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureLoaded();
        ApplyAudioListenerVolume();
    }

    private static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
        sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
        assistMode = PlayerPrefs.GetInt(AssistModeKey, 0) == 1;
        loaded = true;
    }

    private static void SetFloat(ref float field, string key, float value, bool applyAudio)
    {
        EnsureLoaded();

        float clamped = Mathf.Clamp01(value);
        if (Mathf.Approximately(field, clamped))
        {
            return;
        }

        field = clamped;
        PlayerPrefs.SetFloat(key, clamped);
        PlayerPrefs.Save();

        if (applyAudio)
        {
            ApplyAudioListenerVolume();
        }

        OnChanged?.Invoke();
    }

    private static void ApplyAudioListenerVolume()
    {
        AudioListener.volume = masterVolume;
    }
}
