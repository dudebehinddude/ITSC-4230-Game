using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class GameMenuController : MonoBehaviour
{
    private const float PauseDimAlpha = 0.82f;
    private const int CanvasSortingOrder = 2000;

    // Intro narration timing.
    private const float IntroBlackSeconds = 1.2f;
    private const float IntroLineFadeIn = 0.95f;
    private const float IntroLineHold = 1.7f;
    private const float IntroLineFadeOut = 0.7f;
    private const float IntroRevealSeconds = 1.7f;
    private const float EndingPushSeconds = 1.5f;
    private const float EndingFadeSeconds = 1.5f;
    private const float EndingTextFadeSeconds = 0.9f;
    private const float EndingPushSpeed = 2.25f;

    // Opening story beats, drawn from the design document (Section 4.1 / 5.1):
    // a fall through a hidden fissure, waking at the bottom of a dark chasm,
    // an unreachable thread of surface light, and the only way out being down.
    private static readonly string[] IntroLines =
    {
        "The surface forgot you the moment the fissure swallowed you whole.",
        "You fell for a long, silent time.",
        "Now you wake at the bottom of a vast, breathing dark.",
        "Far above, a single thread of sunlight marks a way home already lost.",
        "The cliffs are sheer. There is no climbing back.",
        "There is only one way left.\nDown.",
    };

    // Screen.resolutions is often incomplete in the Unity editor, so we merge in common sizes.
    private static readonly (int width, int height)[] CommonResolutions =
    {
        (640, 480),
        (800, 600),
        (1024, 768),
        (1280, 720),
        (1280, 800),
        (1366, 768),
        (1600, 900),
        (1920, 1080),
        (2560, 1440),
    };

    private static GameMenuController instance;

    private readonly List<Resolution> selectableResolutions = new List<Resolution>();

    private Canvas canvas;
    private Image screenDim;
    private GameObject menuBackdrop;
    private RawImage surfaceGlow;
    private RectTransform surfaceGlowRect;
    private float surfaceGlowBaseY;
    private GameObject mainPanel;
    private GameObject pausePanel;
    private GameObject optionsPanel;
    private Text optionsTitle;
    private Text introText;
    private Text skipHint;
    private Text endingText;
    private Text endingHint;
    private Button mainPlayButton;
    private Button pauseResumeButton;
    private Text resolutionValueText;
    private GameObject resolutionListPanel;
    private Transform resolutionListContent;
    private bool resolutionListOpen;
    private Toggle fullscreenToggle;
    private Button optionsBackButton;
    // private Text controlsSummary;

    private MenuMode mode = MenuMode.Main;
    private MenuMode optionsReturnMode = MenuMode.Main;
    private bool gameStarted;
    private bool introPlaying;
    private bool introSkipRequested;
    private bool endingPlaying;
    private bool endingReturnRequested;
    private bool endingAcceptsReturnClick;
    private bool paused;
    private float previousTimeScale = 1f;

    // Atmospheric palette shared across the menu surface.
    private static readonly Color Cream = new Color(0.95f, 0.92f, 0.84f, 1f);
    private static readonly Color SoftText = new Color(0.78f, 0.81f, 0.86f, 1f);
    private static readonly Color Accent = new Color(0.96f, 0.56f, 0.20f, 1f);
    private static readonly Color PanelFill = new Color(0.02f, 0.022f, 0.03f, 0.92f);
    private static readonly Color FieldFill = new Color(0.07f, 0.075f, 0.09f, 1f);

    private enum MenuMode
    {
        None,
        Main,
        Pause,
        Options
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        var go = new GameObject(nameof(GameMenuController));
        go.AddComponent<GameMenuController>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
        BuildUi();
        ShowMainMenu();
    }

    public static void RequestEnding()
    {
        EnsureExists();
        instance.BeginEnding();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!gameStarted || this.mode == MenuMode.Main)
        {
            SetPlayerControlsEnabled(false);
        }
    }

    private void Update()
    {
        AnimateBackdrop();

        if (endingPlaying)
        {
            if (endingAcceptsReturnClick && WasEndingReturnPressed())
            {
                endingReturnRequested = true;
            }

            return;
        }

        if (introPlaying)
        {
            if (WasSkipPressed())
            {
                introSkipRequested = true;
            }

            return;
        }

        if (!gameStarted)
        {
            return;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (mode == MenuMode.Options)
            {
                CloseOptions();
            }
            else if (paused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    // Slow drift and pulse so the menu backdrop feels alive instead of static.
    private void AnimateBackdrop()
    {
        if (surfaceGlow == null || menuBackdrop == null || !menuBackdrop.activeSelf)
        {
            return;
        }

        float t = Time.unscaledTime;
        float pulse = 0.15f + 0.055f * Mathf.Sin(t * 0.7f);
        surfaceGlow.color = new Color(0.98f, 0.62f, 0.28f, pulse);
        surfaceGlowRect.anchoredPosition = new Vector2(
            Mathf.Sin(t * 0.23f) * 36f,
            surfaceGlowBaseY + Mathf.Sin(t * 0.45f) * 26f);
    }

    private void ShowMainMenu()
    {
        gameStarted = false;
        introPlaying = false;
        endingPlaying = false;
        endingReturnRequested = false;
        endingAcceptsReturnClick = false;
        paused = false;
        HideEndingNarration();
        previousTimeScale = Time.timeScale <= 0f ? 1f : Time.timeScale;
        Time.timeScale = 0f;
        SetCursorForMenu(true);
        SetPlayerControlsEnabled(false);
        SetDimAlpha(1f, true);
        ShowPanel(MenuMode.Main);
    }

    private void StartGame()
    {
        if (introPlaying)
        {
            return;
        }

        gameStarted = true;
        StartCoroutine(StartGameRoutine());
    }

    private IEnumerator StartGameRoutine()
    {
        introPlaying = true;
        introSkipRequested = false;
        ShowPanel(MenuMode.None);
        SetCursorForMenu(false);
        SetPlayerControlsEnabled(false);
        SetDimAlpha(1f, true);
        Time.timeScale = 1f;

        introText.gameObject.SetActive(true);
        SetTextAlpha(introText, 0f);
        skipHint.gameObject.SetActive(true);
        SetTextAlpha(skipHint, 0.45f);

        yield return WaitRealtime(IntroBlackSeconds);

        for (int i = 0; i < IntroLines.Length && !introSkipRequested; i++)
        {
            yield return PlayIntroLine(IntroLines[i]);
        }

        SetTextAlpha(introText, 0f);
        introText.gameObject.SetActive(false);
        skipHint.gameObject.SetActive(false);

        GameStageManager.RequestDefaultStage();
        yield return FadeDimRoutine(0f, IntroRevealSeconds);

        introPlaying = false;
        SetPlayerControlsEnabled(true);
        SetDimAlpha(0f, false);
    }

    private IEnumerator PlayIntroLine(string line)
    {
        introText.text = line;
        yield return FadeTextRoutine(introText, 0f, 1f, IntroLineFadeIn);
        yield return WaitRealtime(IntroLineHold);
        yield return FadeTextRoutine(introText, 1f, 0f, IntroLineFadeOut);
    }

    private IEnumerator FadeTextRoutine(Text text, float from, float to, float duration)
    {
        if (text == null)
        {
            yield break;
        }

        float elapsed = 0f;
        SetTextAlpha(text, from);

        while (elapsed < duration)
        {
            if (introSkipRequested)
            {
                yield break;
            }

            elapsed = Timestep.AdvanceTimer(elapsed, duration, Timestep.UnscaledDelta);
            SetTextAlpha(text, Mathf.Lerp(from, to, Timestep.NormalizeTimer(elapsed, duration)));
            yield return null;
        }

        SetTextAlpha(text, to);
    }

    private IEnumerator WaitRealtime(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (introSkipRequested)
            {
                yield break;
            }

            elapsed = Timestep.AdvanceTimer(elapsed, seconds, Timestep.UnscaledDelta);
            yield return null;
        }
    }

    private static bool WasSkipPressed()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }

        if (Gamepad.current != null &&
            (Gamepad.current.startButton.wasPressedThisFrame || Gamepad.current.buttonSouth.wasPressedThisFrame))
        {
            return true;
        }

        return false;
    }

    private void PauseGame()
    {
        if (paused || introPlaying)
        {
            return;
        }

        paused = true;
        previousTimeScale = Time.timeScale <= 0f ? 1f : Time.timeScale;
        Time.timeScale = 0f;
        SetCursorForMenu(true);
        SetDimAlpha(PauseDimAlpha, true);
        ShowPanel(MenuMode.Pause);
    }

    private void ResumeGame()
    {
        if (!paused)
        {
            return;
        }

        paused = false;
        Time.timeScale = previousTimeScale;
        SetCursorForMenu(false);
        SetDimAlpha(0f, false);
        ShowPanel(MenuMode.None);
    }

    private void OpenOptions(MenuMode returnMode)
    {
        optionsReturnMode = returnMode;
        optionsTitle.text = returnMode == MenuMode.Main ? "Options" : "Paused \u2014 Options";
        CloseResolutionList();
        RefreshOptionsControls();
        ShowPanel(MenuMode.Options);
    }

    private void CloseOptions()
    {
        ShowPanel(optionsReturnMode);
    }

    private void QuitToMainMenu()
    {
        endingPlaying = false;
        endingReturnRequested = false;
        endingAcceptsReturnClick = false;
        HideEndingNarration();
        Time.timeScale = 1f;
        GameStageManager.ResetStages();
        ScreenFadeController.Instance?.SetAlpha(0f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        ShowMainMenu();
    }

    private static void EnsureExists()
    {
        if (instance != null)
        {
            return;
        }

        var go = new GameObject(nameof(GameMenuController));
        go.AddComponent<GameMenuController>();
    }

    private void BeginEnding()
    {
        if (endingPlaying)
        {
            return;
        }

        StartCoroutine(EndingRoutine());
    }

    private IEnumerator EndingRoutine()
    {
        endingPlaying = true;
        endingReturnRequested = false;
        endingAcceptsReturnClick = false;
        introSkipRequested = false;
        gameStarted = true;
        paused = false;
        Time.timeScale = 1f;
        SetCursorForMenu(true);
        SetPlayerControlsEnabled(false);
        ShowPanel(MenuMode.None);
        introText.gameObject.SetActive(false);
        skipHint.gameObject.SetActive(false);

        endingText.gameObject.SetActive(true);
        endingHint.gameObject.SetActive(false);
        SetTextAlpha(endingText, 0f);
        SetTextAlpha(endingHint, 0f);

        Player player = FindAnyObjectByType<Player>();
        Rigidbody2D playerBody = player != null ? player.GetComponent<Rigidbody2D>() : null;
        float elapsed = 0f;
        SetDimAlpha(0f, false);

        while (elapsed < EndingPushSeconds)
        {
            elapsed = Timestep.AdvanceTimer(elapsed, EndingPushSeconds, Timestep.UnscaledDelta);
            PushPlayerRight(playerBody);
            yield return null;
        }

        if (playerBody != null)
        {
            playerBody.linearVelocity = new Vector2(0f, playerBody.linearVelocity.y);
        }

        elapsed = 0f;
        while (elapsed < EndingFadeSeconds)
        {
            elapsed = Timestep.AdvanceTimer(elapsed, EndingFadeSeconds, Timestep.UnscaledDelta);
            float t = Timestep.NormalizeTimer(elapsed, EndingFadeSeconds);
            SetDimAlpha(t, true);
            yield return null;
        }

        SetDimAlpha(1f, true);
        yield return FadeTextRoutine(endingText, 0f, 1f, EndingTextFadeSeconds);

        endingHint.gameObject.SetActive(true);
        yield return FadeTextRoutine(endingHint, 0f, 0.55f, EndingTextFadeSeconds);

        endingReturnRequested = false;
        endingAcceptsReturnClick = true;
        while (!endingReturnRequested)
        {
            yield return null;
        }

        QuitToMainMenu();
    }

    private static void PushPlayerRight(Rigidbody2D playerBody)
    {
        if (playerBody == null)
        {
            return;
        }

        playerBody.linearVelocity = new Vector2(EndingPushSpeed, playerBody.linearVelocity.y);
    }

    private static bool WasEndingReturnPressed()
    {
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    private void QuitApplication()
    {
        Application.Quit();
    }

    private void ShowPanel(MenuMode nextMode)
    {
        mode = nextMode;
        mainPanel.SetActive(nextMode == MenuMode.Main);
        pausePanel.SetActive(nextMode == MenuMode.Pause);
        optionsPanel.SetActive(nextMode == MenuMode.Options);

        bool fullScreenMenu = nextMode == MenuMode.Main ||
                              (nextMode == MenuMode.Options && optionsReturnMode == MenuMode.Main);
        menuBackdrop.SetActive(fullScreenMenu);

        GameObject selected = null;
        if (nextMode == MenuMode.Main)
        {
            selected = mainPlayButton.gameObject;
        }
        else if (nextMode == MenuMode.Pause)
        {
            selected = pauseResumeButton.gameObject;
        }
        else if (nextMode == MenuMode.Options)
        {
            selected = optionsBackButton.gameObject;
        }

        EventSystem.current?.SetSelectedGameObject(selected);
    }

    private IEnumerator FadeDimRoutine(float targetAlpha, float duration)
    {
        float startAlpha = screenDim.color.a;
        float elapsed = 0f;
        screenDim.raycastTarget = true;

        while (elapsed < duration)
        {
            elapsed = Timestep.AdvanceTimer(elapsed, duration, Timestep.UnscaledDelta);
            float t = Timestep.NormalizeTimer(elapsed, duration);
            SetDimAlpha(Mathf.Lerp(startAlpha, targetAlpha, t), true);
            yield return null;
        }

        SetDimAlpha(targetAlpha, targetAlpha > 0.001f);
    }

    private void SetDimAlpha(float alpha, bool blocksRaycasts)
    {
        Color color = screenDim.color;
        color.a = Mathf.Clamp01(alpha);
        screenDim.color = color;
        screenDim.raycastTarget = blocksRaycasts;
    }

    private static void SetTextAlpha(Text text, float alpha)
    {
        if (text == null)
        {
            return;
        }

        Color color = text.color;
        color.a = Mathf.Clamp01(alpha);
        text.color = color;
    }

    private void SetPlayerControlsEnabled(bool enabled)
    {
        Player[] players = FindObjectsByType<Player>();
        for (int i = 0; i < players.Length; i++)
        {
            Player player = players[i];
            player.enabled = enabled;

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body == null)
            {
                continue;
            }

            body.simulated = enabled;
            if (!enabled)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        OilTileRefill[] throwers = FindObjectsByType<OilTileRefill>();
        for (int i = 0; i < throwers.Length; i++)
        {
            throwers[i].enabled = enabled;
        }
    }

    private static void SetCursorForMenu(bool menuVisible)
    {
        Cursor.visible = menuVisible;
        Cursor.lockState = CursorLockMode.None;
    }

    private void BuildUi()
    {
        EnsureEventSystem();

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        screenDim = CreateImage("Screen Dim", transform, new Color(0f, 0f, 0f, 1f));
        Stretch(screenDim.rectTransform);

        BuildMenuBackdrop();

        mainPanel = CreateCenteredPanel("Main Menu", 560f, out Transform mainContent);
        AddTitle(mainContent, "Echoes of the Void");
        AddSubtitle(mainContent, "A descent into the breathing dark.");
        AddDivider(mainContent);
        AddSpacer(mainContent, 8f);
        mainPlayButton = AddButton(mainContent, "Play", StartGame, primary: true);
        AddButton(mainContent, "Options", () => OpenOptions(MenuMode.Main));
        AddButton(mainContent, "Quit", QuitApplication);

        pausePanel = CreateCenteredPanel("Pause Menu", 500f, out Transform pauseContent);
        AddTitle(pauseContent, "Paused");
        AddDivider(pauseContent);
        AddSpacer(pauseContent, 8f);
        pauseResumeButton = AddButton(pauseContent, "Resume", ResumeGame, primary: true);
        AddButton(pauseContent, "Options", () => OpenOptions(MenuMode.Pause));
        AddButton(pauseContent, "Quit to Main Menu", QuitToMainMenu);

        optionsPanel = CreateCenteredPanel("Options Menu", 720f, out Transform optionsContent);
        optionsTitle = AddTitle(optionsContent, "Options");
        AddDivider(optionsContent);
        AddSpacer(optionsContent, 4f);
        AddSlider(optionsContent, "Master Volume", GameSettings.MasterVolume, value => GameSettings.MasterVolume = value);
        AddSlider(optionsContent, "Music Volume", GameSettings.MusicVolume, value => GameSettings.MusicVolume = value);
        // AddSlider(optionsContent, "Sound Effects Volume", GameSettings.SfxVolume, value => GameSettings.SfxVolume = value);
        AddToggle(optionsContent, "Assist Mode (infinite lantern timer — allows for bypassing game progression)", GameSettings.AssistMode, value => GameSettings.AssistMode = value);
        fullscreenToggle = AddToggle(optionsContent, "Fullscreen", Screen.fullScreen, SetFullscreen);
        BuildResolutionSelector(optionsContent);
        AddSpacer(optionsContent, 8f);
        optionsBackButton = AddButton(optionsContent, "Back", CloseOptions, primary: true);

        BuildIntroNarration();
        BuildEndingNarration();

        RefreshOptionsControls();
    }

    private void BuildMenuBackdrop()
    {
        menuBackdrop = CreateUiObject("Menu Backdrop");
        menuBackdrop.transform.SetParent(transform, false);
        Stretch((RectTransform)menuBackdrop.transform);

        // Darkened edges to draw the eye inward.
        Texture2D vignetteTex = CreateRadialTexture(256, 0f, 1f, 1.6f);
        RawImage vignette = CreateRawImage("Vignette", menuBackdrop.transform, vignetteTex, new Color(0f, 0f, 0f, 0.62f));
        Stretch(vignette.rectTransform);

        // A warm shaft of light from above, echoing the unreachable surface sunbeam.
        Texture2D glowTex = CreateRadialTexture(256, 1f, 0f, 1.9f);
        surfaceGlow = CreateRawImage("Surface Glow", menuBackdrop.transform, glowTex, new Color(0.98f, 0.62f, 0.28f, 0.15f));
        surfaceGlowRect = surfaceGlow.rectTransform;
        surfaceGlowRect.anchorMin = new Vector2(0.5f, 0.5f);
        surfaceGlowRect.anchorMax = new Vector2(0.5f, 0.5f);
        surfaceGlowRect.pivot = new Vector2(0.5f, 0.5f);
        surfaceGlowRect.sizeDelta = new Vector2(1900f, 1900f);
        surfaceGlowBaseY = 300f;
        surfaceGlowRect.anchoredPosition = new Vector2(0f, surfaceGlowBaseY);
    }

    private void BuildIntroNarration()
    {
        introText = CreateText("Intro Narration", transform, "", 40, FontStyle.Italic, Cream);
        introText.alignment = TextAnchor.MiddleCenter;
        introText.lineSpacing = 1.15f;
        RectTransform introRect = introText.rectTransform;
        introRect.anchorMin = new Vector2(0.5f, 0.5f);
        introRect.anchorMax = new Vector2(0.5f, 0.5f);
        introRect.pivot = new Vector2(0.5f, 0.5f);
        introRect.sizeDelta = new Vector2(1200f, 360f);
        introRect.anchoredPosition = Vector2.zero;
        AddTextShadow(introText, new Color(0f, 0f, 0f, 0.85f), new Vector2(2f, -2f));
        SetTextAlpha(introText, 0f);
        introText.gameObject.SetActive(false);

        skipHint = CreateText("Skip Hint", transform, "Press any key to skip", 22, FontStyle.Normal, SoftText);
        skipHint.alignment = TextAnchor.MiddleCenter;
        RectTransform skipRect = skipHint.rectTransform;
        skipRect.anchorMin = new Vector2(0.5f, 0f);
        skipRect.anchorMax = new Vector2(0.5f, 0f);
        skipRect.pivot = new Vector2(0.5f, 0f);
        skipRect.sizeDelta = new Vector2(600f, 40f);
        skipRect.anchoredPosition = new Vector2(0f, 64f);
        SetTextAlpha(skipHint, 0.45f);
        skipHint.gameObject.SetActive(false);
    }

    private void BuildEndingNarration()
    {
        endingText = CreateText("Ending Message", transform, "thanks for playing", 56, FontStyle.Italic, Cream);
        endingText.alignment = TextAnchor.MiddleCenter;
        endingText.lineSpacing = 1.15f;
        RectTransform endingRect = endingText.rectTransform;
        endingRect.anchorMin = new Vector2(0.5f, 0.5f);
        endingRect.anchorMax = new Vector2(0.5f, 0.5f);
        endingRect.pivot = new Vector2(0.5f, 0.5f);
        endingRect.sizeDelta = new Vector2(1200f, 260f);
        endingRect.anchoredPosition = Vector2.zero;
        AddTextShadow(endingText, new Color(0f, 0f, 0f, 0.85f), new Vector2(2f, -2f));
        SetTextAlpha(endingText, 0f);
        endingText.gameObject.SetActive(false);

        endingHint = CreateText("Ending Hint", transform, "click to return to the main menu", 22, FontStyle.Normal, SoftText);
        endingHint.alignment = TextAnchor.MiddleCenter;
        RectTransform hintRect = endingHint.rectTransform;
        hintRect.anchorMin = new Vector2(0.5f, 0f);
        hintRect.anchorMax = new Vector2(0.5f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.sizeDelta = new Vector2(720f, 40f);
        hintRect.anchoredPosition = new Vector2(0f, 64f);
        SetTextAlpha(endingHint, 0f);
        endingHint.gameObject.SetActive(false);
    }

    private void HideEndingNarration()
    {
        if (endingText != null)
        {
            SetTextAlpha(endingText, 0f);
            endingText.gameObject.SetActive(false);
        }

        if (endingHint != null)
        {
            SetTextAlpha(endingHint, 0f);
            endingHint.gameObject.SetActive(false);
        }
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        var eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
        DontDestroyOnLoad(eventSystemObject);
    }

    // A panel that hugs its content and stays centered. Used for the short menus.
    private GameObject CreateCenteredPanel(string name, float width, out Transform content)
    {
        GameObject panel = CreateUiObject(name);
        panel.transform.SetParent(transform, false);

        var image = panel.AddComponent<Image>();
        image.color = PanelFill;
        AddPanelOutline(panel);

        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, 0f);

        ConfigureVerticalLayout(panel, new RectOffset(48, 48, 44, 44), 16f);

        var fitter = panel.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        content = panel.transform;
        return panel;
    }

    // A panel that fills the screen height (minus margins) and scrolls when its
    // content is taller than the available space.
    private GameObject CreateScrollPanel(string name, float width, float verticalMargin, out Transform content)
    {
        GameObject panel = CreateUiObject(name);
        panel.transform.SetParent(transform, false);

        var image = panel.AddComponent<Image>();
        image.color = PanelFill;
        AddPanelOutline(panel);

        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, -2f * verticalMargin);

        GameObject viewport = CreateUiObject("Viewport");
        viewport.transform.SetParent(panel.transform, false);
        RectTransform viewportRect = (RectTransform)viewport.transform;
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewport.AddComponent<RectMask2D>();

        GameObject contentGo = CreateUiObject("Content");
        contentGo.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = (RectTransform)contentGo.transform;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        ConfigureVerticalLayout(contentGo, new RectOffset(42, 42, 40, 40), 14f);

        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var scroll = panel.AddComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        content = contentGo.transform;
        return panel;
    }

    private static void ConfigureVerticalLayout(GameObject go, RectOffset padding, float spacing)
    {
        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.padding = padding;
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    private static void AddPanelOutline(GameObject panel)
    {
        var outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.95f, 0.6f, 0.25f, 0.18f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);
    }

    private Text AddTitle(Transform parent, string text)
    {
        Text title = CreateText("Title", parent, text, 56, FontStyle.Bold, Cream);
        title.alignment = TextAnchor.MiddleCenter;
        AddTextShadow(title, new Color(0f, 0f, 0f, 0.7f), new Vector2(2f, -2f));
        return title;
    }

    private Text AddSubtitle(Transform parent, string text)
    {
        Text body = CreateText("Subtitle", parent, text, 22, FontStyle.Italic, SoftText);
        body.alignment = TextAnchor.MiddleCenter;
        body.lineSpacing = 1.1f;
        return body;
    }

    private void AddDivider(Transform parent)
    {
        Image line = CreateImage("Divider", parent, new Color(0.95f, 0.6f, 0.25f, 0.5f));
        SetFixedHeight(line.gameObject, 2f);
    }

    private void AddSpacer(Transform parent, float height)
    {
        GameObject spacer = CreateUiObject("Spacer");
        spacer.transform.SetParent(parent, false);
        SetFixedHeight(spacer, height);
    }

    private Button AddButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, bool primary = false)
    {
        GameObject buttonObject = CreateUiObject(label + " Button");
        buttonObject.transform.SetParent(parent, false);

        var image = buttonObject.AddComponent<Image>();
        image.color = primary
            ? new Color(0.16f, 0.12f, 0.08f, 0.95f)
            : new Color(0.09f, 0.095f, 0.115f, 0.92f);

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = BuildButtonColors(primary);
        button.onClick.AddListener(onClick);

        Text text = CreateText("Label", buttonObject.transform, label, 28, FontStyle.Normal, primary ? Cream : Color.white);
        text.alignment = TextAnchor.MiddleCenter;
        Stretch(text.rectTransform);

        SetFixedHeight(buttonObject, 60f);
        return button;
    }

    private Slider AddSlider(Transform parent, string label, float value, UnityEngine.Events.UnityAction<float> onChanged)
    {
        Text title = CreateText(label + " Label", parent, label, 22, FontStyle.Normal, SoftText);
        title.alignment = TextAnchor.MiddleLeft;

        GameObject sliderObject = CreateUiObject(label + " Slider");
        sliderObject.transform.SetParent(parent, false);
        SetFixedHeight(sliderObject, 30f);

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = value;
        slider.onValueChanged.AddListener(onChanged);

        Image background = CreateImage("Background", sliderObject.transform, FieldFill);
        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(1f, 0.5f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(0f, 10f);
        slider.targetGraphic = background;

        GameObject fillArea = CreateUiObject("Fill Area");
        fillArea.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = (RectTransform)fillArea.transform;
        fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
        fillAreaRect.pivot = new Vector2(0.5f, 0.5f);
        fillAreaRect.sizeDelta = new Vector2(0f, 10f);

        Image fill = CreateImage("Fill", fillArea.transform, Accent);
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        slider.fillRect = fillRect;

        Image handle = CreateImage("Handle", sliderObject.transform, new Color(1f, 0.84f, 0.5f, 1f));
        RectTransform handleRect = handle.rectTransform;
        handleRect.sizeDelta = new Vector2(22f, 30f);
        slider.handleRect = handleRect;

        return slider;
    }

    private Toggle AddToggle(Transform parent, string label, bool value, UnityEngine.Events.UnityAction<bool> onChanged)
    {
        GameObject toggleObject = CreateUiObject(label + " Toggle");
        toggleObject.transform.SetParent(parent, false);
        SetFixedHeight(toggleObject, 38f);

        var horizontal = toggleObject.AddComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 14f;
        horizontal.childAlignment = TextAnchor.MiddleLeft;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = false;

        Image box = CreateImage("Box", toggleObject.transform, FieldFill);
        var boxLayout = box.gameObject.AddComponent<LayoutElement>();
        boxLayout.preferredWidth = 30f;
        boxLayout.minWidth = 30f;
        boxLayout.preferredHeight = 30f;
        boxLayout.minHeight = 30f;

        Image checkmark = CreateImage("Checkmark", box.transform, Accent);
        checkmark.rectTransform.anchorMin = new Vector2(0.22f, 0.22f);
        checkmark.rectTransform.anchorMax = new Vector2(0.78f, 0.78f);
        checkmark.rectTransform.offsetMin = Vector2.zero;
        checkmark.rectTransform.offsetMax = Vector2.zero;

        Text text = CreateText("Label", toggleObject.transform, label, 22, FontStyle.Normal, Color.white);
        text.alignment = TextAnchor.MiddleLeft;
        var textLayout = text.gameObject.AddComponent<LayoutElement>();
        textLayout.flexibleWidth = 1f;

        var toggle = toggleObject.AddComponent<Toggle>();
        toggle.targetGraphic = box;
        toggle.graphic = checkmark;
        toggle.isOn = value;
        toggle.onValueChanged.AddListener(onChanged);

        return toggle;
    }

    private void BuildResolutionSelector(Transform parent)
    {
        Text title = CreateText("Resolution Label", parent, "Resolution", 22, FontStyle.Normal, SoftText);
        title.alignment = TextAnchor.MiddleLeft;

        GameObject row = CreateUiObject("Resolution Row");
        row.transform.SetParent(parent, false);
        SetFixedHeight(row, 52f);

        Image rowBackground = row.AddComponent<Image>();
        rowBackground.color = FieldFill;

        Button rowButton = row.AddComponent<Button>();
        rowButton.targetGraphic = rowBackground;
        rowButton.colors = BuildButtonColors(primary: false);
        rowButton.onClick.AddListener(ToggleResolutionList);

        resolutionValueText = CreateText("Value", row.transform, "", 22, FontStyle.Normal, Color.white);
        resolutionValueText.alignment = TextAnchor.MiddleLeft;
        Stretch(resolutionValueText.rectTransform, 16f, 0f, 46f, 0f);

        Text arrow = CreateText("Arrow", row.transform, "\u25be", 28, FontStyle.Normal, SoftText);
        arrow.alignment = TextAnchor.MiddleCenter;
        RectTransform arrowRect = arrow.rectTransform;
        arrowRect.anchorMin = new Vector2(1f, 0f);
        arrowRect.anchorMax = new Vector2(1f, 1f);
        arrowRect.pivot = new Vector2(1f, 0.5f);
        arrowRect.anchoredPosition = new Vector2(-12f, 0f);
        arrowRect.sizeDelta = new Vector2(32f, 0f);

        resolutionListPanel = CreateUiObject("Resolution List");
        resolutionListPanel.transform.SetParent(parent, false);

        var listLayout = resolutionListPanel.AddComponent<LayoutElement>();
        listLayout.preferredHeight = 220f;
        listLayout.minHeight = 220f;

        Image listBackground = resolutionListPanel.AddComponent<Image>();
        listBackground.color = new Color(0.04f, 0.04f, 0.05f, 0.98f);

        GameObject viewport = CreateUiObject("Viewport");
        viewport.transform.SetParent(resolutionListPanel.transform, false);
        RectTransform viewportRect = (RectTransform)viewport.transform;
        Stretch(viewportRect);
        viewport.AddComponent<RectMask2D>();

        GameObject content = CreateUiObject("Content");
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = (RectTransform)content.transform;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        ConfigureVerticalLayout(content, new RectOffset(6, 6, 6, 6), 4f);

        var contentFitter = content.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = resolutionListPanel.AddComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        resolutionListContent = content.transform;
        resolutionListPanel.SetActive(false);
    }

    private void ToggleResolutionList()
    {
        resolutionListOpen = !resolutionListOpen;
        resolutionListPanel.SetActive(resolutionListOpen);
    }

    private void CloseResolutionList()
    {
        resolutionListOpen = false;
        if (resolutionListPanel != null)
        {
            resolutionListPanel.SetActive(false);
        }
    }

    private Button AddResolutionOptionButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = CreateUiObject(label + " Button");
        buttonObject.transform.SetParent(parent, false);
        SetFixedHeight(buttonObject, 40f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = FieldFill;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = BuildButtonColors(primary: false);
        button.onClick.AddListener(onClick);

        Text text = CreateText("Label", buttonObject.transform, label, 20, FontStyle.Normal, Color.white);
        text.alignment = TextAnchor.MiddleLeft;
        Stretch(text.rectTransform, 12f, 0f, 12f, 0f);

        return button;
    }

    private void SelectResolution(int index)
    {
        SetResolution(index);
        if (resolutionValueText != null && index >= 0 && index < selectableResolutions.Count)
        {
            resolutionValueText.text = FormatResolution(selectableResolutions[index]);
        }

        CloseResolutionList();
    }

    private void RebuildResolutionList()
    {
        if (resolutionListContent == null)
        {
            return;
        }

        for (int i = resolutionListContent.childCount - 1; i >= 0; i--)
        {
            Destroy(resolutionListContent.GetChild(i).gameObject);
        }

        for (int i = 0; i < selectableResolutions.Count; i++)
        {
            int index = i;
            Resolution resolution = selectableResolutions[i];
            AddResolutionOptionButton(
                resolutionListContent,
                FormatResolution(resolution),
                () => SelectResolution(index));
        }

        if (resolutionValueText != null)
        {
            int currentIndex = FindResolutionIndex(Screen.width, Screen.height);
            currentIndex = Mathf.Clamp(currentIndex, 0, selectableResolutions.Count - 1);
            resolutionValueText.text = FormatResolution(selectableResolutions[currentIndex]);
        }
    }

    private void RefreshOptionsControls()
    {
        fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
        UpdateResolutionOptions();
    }

    private void UpdateResolutionOptions()
    {
        selectableResolutions.Clear();
        RefreshRate refreshRate = Screen.currentResolution.refreshRateRatio;

        Resolution[] resolutions = Screen.resolutions;
        if (resolutions != null && resolutions.Length > 0)
        {
            for (int i = 0; i < resolutions.Length; i++)
            {
                AddResolutionOption(resolutions[i]);
            }
        }
        else
        {
            AddResolutionOption(Screen.currentResolution);
        }

        if (Application.isEditor || selectableResolutions.Count <= 1)
        {
            for (int i = 0; i < CommonResolutions.Length; i++)
            {
                (int width, int height) = CommonResolutions[i];
                if (ContainsResolution(width, height))
                {
                    continue;
                }

                AddResolutionOption(new Resolution
                {
                    width = width,
                    height = height,
                    refreshRateRatio = refreshRate,
                });
            }
        }

        if (selectableResolutions.Count == 0)
        {
            AddResolutionOption(Screen.currentResolution);
        }

        RebuildResolutionList();
    }

    private void AddResolutionOption(Resolution resolution)
    {
        if (ContainsResolution(resolution.width, resolution.height))
        {
            return;
        }

        selectableResolutions.Add(resolution);
    }

    private void SetResolution(int index)
    {
        if (index < 0 || index >= selectableResolutions.Count)
        {
            return;
        }

        Resolution resolution = selectableResolutions[index];
        Screen.SetResolution(
            resolution.width,
            resolution.height,
            Screen.fullScreenMode,
            resolution.refreshRateRatio);
    }

    private static void SetFullscreen(bool fullscreen)
    {
        Screen.fullScreenMode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }

    private bool ContainsResolution(int width, int height)
    {
        for (int i = 0; i < selectableResolutions.Count; i++)
        {
            Resolution existing = selectableResolutions[i];
            if (existing.width == width && existing.height == height)
            {
                return true;
            }
        }

        return false;
    }

    private int FindResolutionIndex(int width, int height)
    {
        for (int i = 0; i < selectableResolutions.Count; i++)
        {
            Resolution resolution = selectableResolutions[i];
            if (resolution.width == width && resolution.height == height)
            {
                return i;
            }
        }

        return 0;
    }

    private static string FormatResolution(Resolution resolution)
    {
        return resolution.width + " x " + resolution.height;
    }

    private static Text CreateText(string name, Transform parent, string text, int size, FontStyle style, Color color)
    {
        GameObject textObject = CreateUiObject(name);
        textObject.transform.SetParent(parent, false);

        Text uiText = textObject.AddComponent<Text>();
        uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        uiText.text = text;
        uiText.fontSize = size;
        uiText.fontStyle = style;
        uiText.color = color;
        uiText.raycastTarget = false;
        uiText.horizontalOverflow = HorizontalWrapMode.Wrap;
        uiText.verticalOverflow = VerticalWrapMode.Overflow;

        return uiText;
    }

    private static void AddTextShadow(Text text, Color color, Vector2 distance)
    {
        var shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = distance;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject imageObject = CreateUiObject(name);
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static RawImage CreateRawImage(string name, Transform parent, Texture2D texture, Color color)
    {
        GameObject imageObject = CreateUiObject(name);
        imageObject.transform.SetParent(parent, false);

        RawImage image = imageObject.AddComponent<RawImage>();
        image.texture = texture;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // Builds a soft circular gradient used for the glow and vignette layers.
    private static Texture2D CreateRadialTexture(int size, float innerAlpha, float outerAlpha, float power)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        float half = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float distance = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float t = Mathf.Pow(distance, power);
                float alpha = Mathf.Lerp(innerAlpha, outerAlpha, t);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return texture;
    }

    private static GameObject CreateUiObject(string name)
    {
        return new GameObject(name, typeof(RectTransform));
    }

    private static void Stretch(RectTransform rect)
    {
        Stretch(rect, 0f, 0f, 0f, 0f);
    }

    private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    // Gives a layout-group child a fixed height (and a matching min so it is never
    // squeezed). Width is still controlled/expanded by the parent layout group.
    private static LayoutElement SetFixedHeight(GameObject go, float height)
    {
        var layout = go.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = go.AddComponent<LayoutElement>();
        }

        layout.minHeight = height;
        layout.preferredHeight = height;
        layout.flexibleHeight = 0f;
        return layout;
    }

    private static ColorBlock BuildButtonColors(bool primary)
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = primary
            ? new Color(0.16f, 0.12f, 0.08f, 0.95f)
            : new Color(0.09f, 0.095f, 0.115f, 0.92f);
        colors.highlightedColor = new Color(0.30f, 0.20f, 0.10f, 1f);
        colors.pressedColor = new Color(0.96f, 0.56f, 0.20f, 1f);
        colors.selectedColor = new Color(0.24f, 0.17f, 0.10f, 1f);
        colors.disabledColor = new Color(0.08f, 0.08f, 0.08f, 0.55f);
        colors.fadeDuration = 0.12f;
        return colors;
    }
}
