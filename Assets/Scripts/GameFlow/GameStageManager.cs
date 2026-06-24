using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameStageManager : MonoBehaviour
{
    private const string DefaultStageName = "default";

    public static GameStageManager Instance { get; private set; }

    private readonly HashSet<string> activatedStages = new();
    private AudioSource activeMusicSource;
    private AudioSource inactiveMusicSource;
    private Coroutine musicRoutine;
    private float activeStageVolume;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureExists();
    }

    private static void EnsureExists()
    {
        if (Instance != null)
        {
            return;
        }

        Instance = FindAnyObjectByType<GameStageManager>();
        if (Instance != null)
        {
            return;
        }

        var managerObject = new GameObject(nameof(GameStageManager));
        Instance = managerObject.AddComponent<GameStageManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple GameStageManager instances found. Keeping the first one.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureMusicSources();
    }

    private void OnEnable()
    {
        GameSettings.OnChanged += ApplySettingsVolume;
    }

    private void Start()
    {
        RefreshMusic();
    }

    private void OnDisable()
    {
        GameSettings.OnChanged -= ApplySettingsVolume;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static void RequestStage(string stageName)
    {
        if (string.IsNullOrWhiteSpace(stageName))
        {
            return;
        }

        EnsureExists();
        Instance.ActivateStage(stageName);
    }

    public static void RequestDefaultStage()
    {
        RequestStage(DefaultStageName);
    }

    public static void ResetStages()
    {
        EnsureExists();
        Instance.activatedStages.Clear();
        Instance.RefreshMusic();
    }

    private void ActivateStage(string stageName)
    {
        if (!GameStageDefinitions.TryGet(stageName, out GameStageDefinition definition))
        {
            Debug.LogWarning($"Unknown game stage '{stageName}'. Add it to GameStageDefinitions.", this);
            return;
        }

        string normalizedName = NormalizeStageName(stageName);
        if (!activatedStages.Add(normalizedName))
        {
            return;
        }

        RefreshMusic();

        if (definition.HasCutscene)
        {
            StartCoroutine(PlayStageCutscene(definition));
        }

        if (definition.PlaysEnding)
        {
            GameMenuController.RequestEnding();
        }
    }

    private IEnumerator PlayStageCutscene(GameStageDefinition definition)
    {
        if (definition.CutsceneDelay > 0f)
        {
            yield return new WaitForSeconds(definition.CutsceneDelay);
        }

        CutsceneDefinitions.Play(definition.CutsceneName);
    }

    private void RefreshMusic()
    {
        GameStageDefinition highestActiveStage = null;

        IReadOnlyList<(string name, GameStageDefinition definition)> stages = GameStageDefinitions.Ordered;
        for (int i = 0; i < stages.Count; i++)
        {
            (string name, GameStageDefinition definition) stage = stages[i];
            if (stage.definition == null || !activatedStages.Contains(NormalizeStageName(stage.name)))
            {
                continue;
            }

            highestActiveStage = stage.definition;
        }

        CrossfadeTo(highestActiveStage);
    }

    private void CrossfadeTo(GameStageDefinition stage)
    {
        EnsureMusicSources();

        AudioClip clip = GameStageDefinitions.ResolveMusicClip(stage);
        float volume = stage?.MusicVolume ?? 0f;
        float fadeSeconds = stage?.MusicFadeSeconds ?? 0f;
        bool restartMusic = stage?.RestartMusic ?? false;

        if (musicRoutine != null)
        {
            StopCoroutine(musicRoutine);
            musicRoutine = null;
        }

        if (activeMusicSource.clip == clip && !restartMusic)
        {
            activeStageVolume = volume;
            activeMusicSource.volume = EffectiveMusicVolume(activeStageVolume);
            if (clip != null && !activeMusicSource.isPlaying)
            {
                activeMusicSource.Play();
            }

            return;
        }

        musicRoutine = StartCoroutine(CrossfadeMusicRoutine(clip, volume, fadeSeconds, restartMusic));
    }

    private IEnumerator CrossfadeMusicRoutine(AudioClip nextClip, float nextVolume, float fadeSeconds, bool restartMusic)
    {
        AudioSource outgoing = activeMusicSource;
        AudioSource incoming = inactiveMusicSource;
        float outgoingStartVolume = outgoing.volume;
        bool hasNextClip = nextClip != null;

        if (hasNextClip)
        {
            incoming.clip = nextClip;
            incoming.loop = true;
            incoming.volume = 0f;
            if (!restartMusic)
            {
                SyncIncomingPlaybackPosition(incoming, outgoing);
            }

            incoming.Play();
        }

        if (fadeSeconds <= 0f)
        {
            outgoing.Stop();
            outgoing.clip = null;
            outgoing.volume = 0f;

            if (hasNextClip)
            {
                incoming.volume = EffectiveMusicVolume(nextVolume);
                activeStageVolume = nextVolume;
                SwapMusicSources();
            }
            else
            {
                activeStageVolume = 0f;
            }

            musicRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeSeconds)
        {
            elapsed = Timestep.AdvanceTimer(elapsed, fadeSeconds, Timestep.Delta);
            float t = Timestep.NormalizeTimer(elapsed, fadeSeconds);

            outgoing.volume = Mathf.Lerp(outgoingStartVolume, 0f, t);
            if (hasNextClip)
            {
                incoming.volume = Mathf.Lerp(0f, EffectiveMusicVolume(nextVolume), t);
            }

            yield return null;
        }

        outgoing.Stop();
        outgoing.clip = null;
        outgoing.volume = 0f;

        if (hasNextClip)
        {
            incoming.volume = EffectiveMusicVolume(nextVolume);
            activeStageVolume = nextVolume;
            SwapMusicSources();
        }
        else
        {
            activeStageVolume = 0f;
        }

        musicRoutine = null;
    }

    private static void SyncIncomingPlaybackPosition(AudioSource incoming, AudioSource outgoing)
    {
        if (incoming.clip == null || outgoing.clip == null || !outgoing.isPlaying)
        {
            return;
        }

        if (incoming.clip.samples <= 0 || incoming.clip.frequency <= 0 || outgoing.clip.frequency <= 0)
        {
            return;
        }

        double outgoingSeconds = (double)outgoing.timeSamples / outgoing.clip.frequency;
        int incomingSample = (int)((outgoingSeconds * incoming.clip.frequency) % incoming.clip.samples);
        incoming.timeSamples = incomingSample;
    }

    private void SwapMusicSources()
    {
        (inactiveMusicSource, activeMusicSource) = (activeMusicSource, inactiveMusicSource);
    }

    private void EnsureMusicSources()
    {
        if (activeMusicSource != null && inactiveMusicSource != null)
        {
            return;
        }

        activeMusicSource = gameObject.AddComponent<AudioSource>();
        inactiveMusicSource = gameObject.AddComponent<AudioSource>();
        ConfigureMusicSource(activeMusicSource);
        ConfigureMusicSource(inactiveMusicSource);
    }

    private static void ConfigureMusicSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = true;
        source.volume = 0f;
    }

    private void ApplySettingsVolume()
    {
        if (activeMusicSource != null)
        {
            activeMusicSource.volume = EffectiveMusicVolume(activeStageVolume);
        }
    }

    private static float EffectiveMusicVolume(float stageVolume)
    {
        return Mathf.Clamp01(stageVolume) * GameSettings.MusicVolume;
    }

    private static string NormalizeStageName(string stageName)
    {
        return string.IsNullOrWhiteSpace(stageName) ? "" : stageName.Trim();
    }
}
