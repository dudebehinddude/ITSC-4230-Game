using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GameStageDefinition
{
    public string MusicClipName { get; set; } = "";
    public float MusicVolume { get; set; } = 1f;
    public float MusicFadeSeconds { get; set; } = 6f;
    public bool RestartMusic { get; set; }
    public string CutsceneName { get; set; } = "";
    public float CutsceneDelay { get; set; }
    public bool PlaysEnding { get; set; }
    public bool HasCutscene => !string.IsNullOrWhiteSpace(CutsceneName);
}

public static class GameStageDefinitions
{
    private const string MusicResourceFolder = "Music";

    private static readonly Dictionary<string, AudioClip> MusicClipCache = new();

    // Later entries have higher music priority when multiple stages have been activated.
    private static readonly (string name, GameStageDefinition definition)[] OrderedDefinitions =
    {
        ("default", new GameStageDefinition
        {
            MusicClipName = "1",
            MusicVolume = 1f,
            MusicFadeSeconds = 3.3f,
            RestartMusic = true,
        }),
        ("1.1", new GameStageDefinition
        {
            MusicClipName = "1.1",
            MusicVolume = 1f,
            MusicFadeSeconds = 1.5f,
            CutsceneName = "reveal_1.1",
            CutsceneDelay = 0.5f,
        }),
        ("1.2", new GameStageDefinition
        {
            MusicClipName = "1.2",
            MusicVolume = 1f,
            MusicFadeSeconds = 1.5f,
            CutsceneName = "reveal_1.2",
            CutsceneDelay = 0.5f,
        }),
        ("2", new GameStageDefinition
        {
            MusicClipName = "2",
            MusicVolume = 1f,
            MusicFadeSeconds = 1.5f,
            CutsceneName = "enter_room_2",
            CutsceneDelay = 0.5f,
        }),
        ("2.1", new GameStageDefinition
        {
            MusicClipName = "2",
            MusicVolume = 1f,
            MusicFadeSeconds = 1.5f,
            CutsceneName = "reveal_2.1",
            CutsceneDelay = 0.5f,
        }),
        ("2.2", new GameStageDefinition
        {
            MusicClipName = "2",
            MusicVolume = 1f,
            MusicFadeSeconds = 1.5f,
            CutsceneName = "reveal_2.2",
            CutsceneDelay = 0.5f,
        }),
        ("2.3", new GameStageDefinition
        {
            MusicClipName = "2",
            MusicVolume = 1f,
            MusicFadeSeconds = 1.5f,
            CutsceneName = "reveal_2.3",
            CutsceneDelay = 0.5f,
        }),
        ("end", new GameStageDefinition
        {
            MusicFadeSeconds = 4.5f,
            PlaysEnding = true,
        }),
    };

    public static IReadOnlyList<(string name, GameStageDefinition definition)> Ordered => OrderedDefinitions;

    public static bool TryGet(string stageName, out GameStageDefinition definition)
    {
        string normalizedName = NormalizeName(stageName);

        for (int i = 0; i < OrderedDefinitions.Length; i++)
        {
            if (OrderedDefinitions[i].name == normalizedName)
            {
                definition = OrderedDefinitions[i].definition;
                return true;
            }
        }

        definition = null;
        return false;
    }

    public static AudioClip ResolveMusicClip(GameStageDefinition definition)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.MusicClipName))
        {
            return null;
        }

        string clipName = definition.MusicClipName.Trim();
        if (MusicClipCache.TryGetValue(clipName, out AudioClip cachedClip))
        {
            return cachedClip;
        }

        AudioClip clip = Resources.Load<AudioClip>($"{MusicResourceFolder}/{clipName}");
        if (clip == null)
        {
            Debug.LogWarning(
                $"Game stage music '{clipName}' was not found. Put the clip at Assets/Resources/Music/{clipName}.");
        }

        MusicClipCache[clipName] = clip;
        return clip;
    }

    private static string NormalizeName(string name)
    {
        return string.IsNullOrWhiteSpace(name) ? "" : name.Trim();
    }
}

public static class CutsceneDefinitions
{
    private static readonly Dictionary<string, CutsceneDefinition> Definitions = new()
    {
        ["example_crystal_reveal"] = new CutsceneDefinition
        {
            PostExplosionDelay = 1.1f,
            Steps = new[]
            {
                CutsceneStep.Pan(
                    cameraPos: new Vector2(12f, 8f),
                    cameraSize: 7f,
                    panSeconds: 0.9f,
                    holdSeconds: 1.2f,
                    tileChanges: new List<TileChange>
                    {
                        TileChange.Fill(TilemapLayer.Background, 3, 1, 5, 2, "stone_bg", "#66CCFF", 24),
                        TileChange.At(TilemapLayer.Background, 7, 3, "stone_bg", "#66CCFF", 16),
                    }),

                CutsceneStep.Pan(
                    cameraPos: new Vector2(10f, 5f),
                    cameraSize: 5.5f,
                    panSeconds: 0.6f,
                    holdSeconds: 1.8f,
                    tileChanges: new List<TileChange>
                    {
                        TileChange.At(TilemapLayer.Background, 9, 4, "stone_bg", "#FF66CC", 32),
                    }),
            },
        },
        ["reveal_1.1"] = new CutsceneDefinition
        {
            PostExplosionDelay = 0.5f,
            Steps = new[]
            {
                CutsceneStep.Pan(
                    cameraPos: new Vector2(-47f, -4f),
                    cameraSize: 7f,
                    panSeconds: 0.5f,
                    holdSeconds: 0.5f,
                    tileChanges: new List<TileChange>
                    {
                        TileChange.At(TilemapLayer.Background, -47, -4, "lantern_blue", "#66CCFF", 16),
                    }),
                CutsceneStep.Pan(
                    cameraPos: new Vector2(-76f, -23f),
                    cameraSize: 7f,
                    panSeconds: 0.5f,
                    holdSeconds: 0.5f,
                    tileChanges: new List<TileChange>
                    {
                        TileChange.At(TilemapLayer.Background, -76, -23, "lantern_blue", "#66CCFF", 16),
                    }),
                CutsceneStep.Pan(
                    cameraPos: new Vector2(-46f, -25f),
                    cameraSize: 7f,
                    panSeconds: 0.5f,
                    holdSeconds: 0.5f,
                    tileChanges: new List<TileChange>
                    {
                        TileChange.At(TilemapLayer.Background, -46, -25, "lantern_orange", "#CCFF66", 16),
                    }),
            }
        },
        ["reveal_1.2"] = new CutsceneDefinition
        {
            PostExplosionDelay = 0.5f,
            Steps = new[]
            {
                CutsceneStep.Pan(
                    cameraPos: new Vector2(35f, 0f),
                    cameraSize: 7f,
                    panSeconds: 0.5f,
                    holdSeconds: 0.5f,
                    tileChanges: new List<TileChange>
                    {
                        TileChange.At(TilemapLayer.Background, 35, 0, "lantern_orange", "#CCFF66", 16),
                    }),
                CutsceneStep.Pan(
                    cameraPos: new Vector2(57f, -3f),
                    cameraSize: 7f,
                    panSeconds: 0.5f,
                    holdSeconds: 0.5f,
                    tileChanges: new List<TileChange>
                    {
                        TileChange.At(TilemapLayer.Background, 57, -3, "lantern_orange", "#CCFF66", 16),
                    }),
                CutsceneStep.Pan(
                    cameraPos: new Vector2(66f, 8f),
                    cameraSize: 7f,
                    panSeconds: 0.5f,
                    holdSeconds: 0.5f,
                    tileChanges: new List<TileChange>
                    {
                        TileChange.At(TilemapLayer.Background, 66, 8, "lantern_orange", "#CCFF66", 16),
                    }),
            }
        },
        ["enter_room_2"] = new CutsceneDefinition
        {
            Steps = new[]
            {
                CutsceneStep.Action(
                    playerForce: new Vector2(18f, 0.05f),
                    playerControlLockDuration: 0.35f,
                    tileChangeDelay: 0.25f,
                    holdSeconds: 0.4f,
                    tileChanges: new List<TileChange>
                    {
                        TileChange.Fill(
                            TilemapLayer.Foreground,
                            68, 12,
                            66, 7,
                            "stone_47"
                        )
                    }),
            }
        },
        ["reveal_2.1"] = new CutsceneDefinition
        {
            PostExplosionDelay = 0.5f,
            Steps = new[]
            {
                CutsceneStep.Pan(
                    cameraPos: new Vector2(99f, 18f),
                    cameraSize: 40f,
                    panSeconds: 0.5f,
                    holdSeconds: 0.5f,
                    tileChanges: new List<TileChange>
                    {
                        TileChange.At(TilemapLayer.Background, 82, 3, "lantern_orange", "#CCFF66", 16),
                        TileChange.At(TilemapLayer.Background, 92, 8, "lantern_orange", "#CCFF66", 16),
                        TileChange.At(TilemapLayer.Background, 86, 18, "lantern_orange", "#CCFF66", 16),
                        TileChange.At(TilemapLayer.Background, 76, 22, "lantern_orange", "#CCFF66", 16),
                        TileChange.At(TilemapLayer.Background, 79, 34, "lantern_orange", "#CCFF66", 16),
                        TileChange.At(TilemapLayer.Background, 124, -1, "lantern_orange", "#CCFF66", 16),
                        TileChange.At(TilemapLayer.Background, 106, -2, "lantern_orange", "#CCFF66", 16),
                    }),
                CutsceneStep.Pan(
                    cameraPos: new Vector2(135f, 12.5f),
                    cameraSize: 5f,
                    panSeconds: 0.5f,
                    holdSeconds: 0.5f,
                    tileChanges: new List<TileChange>
                    {
                        TileChange.At(TilemapLayer.Background, 133, 14, "stone_bg"),
                        TileChange.At(TilemapLayer.Background, 134, 14, "stone_bg"),
                        TileChange.At(TilemapLayer.Background, 136, 14, "stone_bg"),
                        TileChange.At(TilemapLayer.Background, 133, 13, "stone_bg"),
                        TileChange.At(TilemapLayer.Background, 136, 13, "stone_bg"),
                        TileChange.At(TilemapLayer.Background, 133, 12, "stone_bg"),
                        TileChange.At(TilemapLayer.Background, 136, 12, "stone_bg"),
                    }),
            }
        },
        ["reveal_2.2"] = new CutsceneDefinition
        {
            PostExplosionDelay = 0.5f,
            Steps = new[]
            {
                CutsceneStep.Pan(
                    cameraPos: new Vector2(99f, 18f),
                    cameraSize: 40f,
                    panSeconds: 0.5f,
                    holdSeconds: 0.5f,
                    tileChanges: new List<TileChange>
                    {
                        TileChange.At(TilemapLayer.Background, 101, 30, "lantern_orange", "#CCFF66", 16),
                        TileChange.At(TilemapLayer.Background, 116, 26, "lantern_orange", "#CCFF66", 16),
                        TileChange.At(TilemapLayer.Background, 105, 46, "lantern_orange", "#CCFF66", 16),
                    }),
                CutsceneStep.Pan(
                    cameraPos: new Vector2(135f, 12.5f),
                    cameraSize: 5f,
                    panSeconds: 0.5f,
                    holdSeconds: 0.5f,
                    tileChanges: new List<TileChange>
                    {
                        TileChange.At(TilemapLayer.Background, 135, 14, "stone_bg"),
                        TileChange.At(TilemapLayer.Background, 133, 11, "stone_bg"),
                        TileChange.At(TilemapLayer.Background, 136, 11, "stone_bg"),
                        TileChange.At(TilemapLayer.Background, 133, 10, "stone_bg"),
                        TileChange.At(TilemapLayer.Background, 136, 10, "stone_bg"),
                        TileChange.At(TilemapLayer.Background, 134, 13, "stone_bg"),
                        TileChange.At(TilemapLayer.Background, 135, 13, "stone_bg"),
                    }),
            }
        },
        ["reveal_2.3"] = new CutsceneDefinition
        {
            PostExplosionDelay = 0.5f,
            Steps = new[]
            {
                CutsceneStep.Pan(
                    cameraPos: new Vector2(99f, 18f),
                    cameraSize: 40f,
                    panSeconds: 0.5f,
                    holdSeconds: 0.5f,
                    tileChanges: new List<TileChange>
                    {
                        TileChange.At(TilemapLayer.Background, 118, 3, "lantern_orange", "#CCFF66", 16),
                        TileChange.At(TilemapLayer.Background, 110, 6, "lantern_orange", "#CCFF66", 16),
                        TileChange.At(TilemapLayer.Background, 124, 11, "lantern_orange", "#CCFF66", 16),
                        TileChange.At(TilemapLayer.Background, 99, 11, "lantern_orange", "#CCFF66", 16),
                        TileChange.At(TilemapLayer.Background, 113, 14, "lantern_orange", "#CCFF66", 16),
                    }),
                CutsceneStep.Pan(
                    cameraPos: new Vector2(135f, 12.5f),
                    cameraSize: 5f,
                    panSeconds: 0.5f,
                    holdSeconds: 0.5f,
                    tileChanges: new List<TileChange>
                    {
                        TileChange.At(TilemapLayer.Background, 134, 12, "stone_bg"),
                        TileChange.At(TilemapLayer.Background, 135, 12, "stone_bg"),
                        TileChange.At(TilemapLayer.Background, 134, 11, "stone_bg"),
                        TileChange.At(TilemapLayer.Background, 135, 11, "stone_bg"),
                        TileChange.At(TilemapLayer.Background, 134, 10, "stone_bg"),
                        TileChange.At(TilemapLayer.Background, 135, 10, "stone_bg"),
                    }),
            }
        },
    };

    public static bool TryGet(string cutsceneName, out CutsceneDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(cutsceneName))
        {
            definition = null;
            return false;
        }

        return Definitions.TryGetValue(cutsceneName.Trim(), out definition);
    }

    public static void Play(string cutsceneName, Action onComplete = null)
    {
        if (!TryGet(cutsceneName, out CutsceneDefinition definition))
        {
            Debug.LogWarning($"Unknown cutscene '{cutsceneName}'. Add it to CutsceneDefinitions.cs.");
            onComplete?.Invoke();
            return;
        }

        CutscenePlayer.Play(definition, onComplete);
    }
}
