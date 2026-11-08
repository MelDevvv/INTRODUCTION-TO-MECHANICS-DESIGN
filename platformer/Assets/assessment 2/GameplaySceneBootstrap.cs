
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

[DefaultExecutionOrder(10000)]
[RequireComponent(typeof(UIDocument))]
public class GameplaySceneBootstrap : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private LevelTimerUI levelTimer;

    [Header("Scene Navigation")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Transitions")]
    [SerializeField] private float loadingTransitionDuration = 0.3f;

    [Header("UI Sounds")]
    [SerializeField] private AudioSource uiAudioSource;
    [SerializeField] private AudioClip buttonHoverSound;
    [SerializeField] private AudioClip buttonClickSound;

    [Range(0f, 1f)]
    [SerializeField] private float hoverVolume = 0.2f;

    [Range(0f, 1f)]
    [SerializeField] private float clickVolume = 0.4f;

    [Header("Pause Music")]
    [SerializeField] private AudioSource gameplayMusicSource;
    [SerializeField] private AudioClip pauseMusicClip;
    [SerializeField] private AudioSource pauseMusicSource;

    [Range(0f, 1f)]
    [SerializeField] private float pauseMusicVolume = 1f;

    [Header("Death")]
    [SerializeField] private float deathRestartDelay = 1f;

    private VisualElement root;
    private VisualElement startPrompt;
    private VisualElement screenOverlay;
    private VisualElement menuFrame;
    private VisualElement loadingOverlay;
    private VisualElement loadingBarFill;

    private Label screenKicker;
    private Label screenTitle;
    private Label screenDescription;
    private Label screenFooter;
    private Label loadingTitle;

    private Button resumeButton;
    private Button restartButton;
    private Button mainMenuButton;

    private bool gameplayStarted;
    private bool isPaused;
    private bool isLoading;
    private bool isDying;
    private bool gameplayMusicWasPlaying;

    private void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        Time.timeScale = 1f;

        SetupAudioSources();
    }

    // ==================================================
    // AUDIO SETUP
    // ==================================================

    private void SetupAudioSources()
    {
        if (uiAudioSource == null)
            uiAudioSource = gameObject.AddComponent<AudioSource>();

        uiAudioSource.playOnAwake = false;
        uiAudioSource.loop = false;
        uiAudioSource.spatialBlend = 0f;
        uiAudioSource.ignoreListenerPause = true;
        uiAudioSource.volume = 1f;

        if (pauseMusicSource == null ||
            pauseMusicSource == uiAudioSource ||
            pauseMusicSource == gameplayMusicSource)
        {
            pauseMusicSource = gameObject.AddComponent<AudioSource>();
        }

        pauseMusicSource.playOnAwake = false;
        pauseMusicSource.loop = true;
        pauseMusicSource.spatialBlend = 0f;
        pauseMusicSource.ignoreListenerPause = true;
        pauseMusicSource.volume = pauseMusicVolume;
        pauseMusicSource.clip = pauseMusicClip;
    }

    private void Start()
    {
        if (uiDocument == null)
        {
            Debug.LogError(
                "GameplaySceneBootstrap requires a UIDocument.",
                this
            );

            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement;

        startPrompt = root.Q<VisualElement>("start-prompt");
        screenOverlay = root.Q<VisualElement>("screen-overlay");
        menuFrame = root.Q<VisualElement>("menu-frame");

        loadingOverlay = root.Q<VisualElement>("scene-loading-overlay");
        loadingBarFill = root.Q<VisualElement>("scene-loading-bar-fill");

        screenKicker = root.Q<Label>("screen-kicker");
        screenTitle = root.Q<Label>("screen-title");
        screenDescription = root.Q<Label>("screen-description");
        screenFooter = root.Q<Label>("screen-footer");
        loadingTitle = root.Q<Label>("scene-loading-title");

        resumeButton = root.Q<Button>("primary-button");
        restartButton = root.Q<Button>("secondary-button");
        mainMenuButton = root.Q<Button>("tertiary-button");

        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();

        if (levelTimer == null)
            levelTimer = FindFirstObjectByType<LevelTimerUI>();

        if (playerController == null)
        {
            Debug.LogError(
                "GameplaySceneBootstrap cannot find PlayerController.",
                this
            );

            enabled = false;
            return;
        }

        // Register hover sounds for all buttons.
        root.Query<Button>().ForEach(RegisterButtonAudio);

        // Register pause menu actions.
        if (resumeButton != null)
            resumeButton.clicked += OnResumeClicked;
        else
            Debug.LogError("Missing primary-button.", this);

        if (restartButton != null)
            restartButton.clicked += OnRestartClicked;
        else
            Debug.LogError("Missing secondary-button.", this);

        if (mainMenuButton != null)
            mainMenuButton.clicked += OnMainMenuClicked;
        else
            Debug.LogError("Missing tertiary-button.", this);

        playerController.SetGameplayInputEnabled(false);
        playerController.SetStartPreviewActive(true);

        if (levelTimer != null)
            levelTimer.PauseTimer();

        if (screenOverlay != null)
        {
            screenOverlay.style.display = DisplayStyle.None;
            screenOverlay.style.opacity = 0f;
        }

        if (loadingOverlay != null)
            loadingOverlay.style.display = DisplayStyle.None;

        if (startPrompt != null)
        {
            startPrompt.style.display = DisplayStyle.Flex;
            startPrompt.RemoveFromClassList("is-visible");

            startPrompt.schedule.Execute(() =>
            {
                if (!gameplayStarted && startPrompt != null)
                    startPrompt.AddToClassList("is-visible");

            }).StartingIn(30);
        }
        else
        {
            Debug.LogError("Missing start-prompt.", this);
        }

        InputSystem.onAnyButtonPress.CallOnce(
            HandleAnyButtonPressed
        );
    }

    // ==================================================
    // UI BUTTON AUDIO
    // ==================================================

    private void RegisterButtonAudio(Button button)
    {
        if (button == null)
            return;

        button.RegisterCallback<PointerEnterEvent>(
            OnButtonPointerEnter
        );
    }

    private void OnButtonPointerEnter(PointerEnterEvent evt)
    {
        if (isLoading)
            return;

        PlayHoverSound();
    }

    private void PlayHoverSound()
    {
        if (buttonHoverSound == null || uiAudioSource == null)
            return;

        uiAudioSource.PlayOneShot(
            buttonHoverSound,
            hoverVolume
        );
    }

    private void PlayClickSound()
    {
        if (buttonClickSound == null || uiAudioSource == null)
            return;

        uiAudioSource.PlayOneShot(
            buttonClickSound,
            clickVolume
        );
    }

    // ==================================================
    // BUTTON ACTIONS
    // ==================================================

    private void OnResumeClicked()
    {
        if (isLoading || isDying)
            return;

        PlayClickSound();
        ResumeGame();
    }

    private void OnRestartClicked()
    {
        if (isLoading || isDying)
            return;

        PlayClickSound();

        StartCoroutine(RestartAfterClickSound());
    }

    private void OnMainMenuClicked()
    {
        if (isLoading || isDying)
            return;

        PlayClickSound();

        StartCoroutine(MainMenuAfterClickSound());
    }

    private IEnumerator RestartAfterClickSound()
    {
        yield return WaitForClickSound();

        RestartLevel();
    }

    private IEnumerator MainMenuAfterClickSound()
    {
        yield return WaitForClickSound();

        ReturnToMainMenu();
    }

    private IEnumerator WaitForClickSound()
    {
        if (buttonClickSound != null)
        {
            yield return new WaitForSecondsRealtime(
                buttonClickSound.length
            );
        }
    }

    // ==================================================
    // UPDATE / INPUT
    // ==================================================

    private void Update()
    {
        if (isLoading || isDying)
            return;

        if (gameplayStarted &&
            Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();

            return;
        }

        if (gameplayStarted &&
            Gamepad.current != null &&
            Gamepad.current.startButton.wasPressedThisFrame)
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();

            return;
        }

        if (!gameplayStarted)
        {
            foreach (Gamepad gamepad in Gamepad.all)
            {
                if (gamepad.leftStick.ReadValue().sqrMagnitude > 0.20f ||
                    gamepad.rightStick.ReadValue().sqrMagnitude > 0.20f)
                {
                    StartGameplay();
                    return;
                }
            }
        }
    }

    // ==================================================
    // START GAMEPLAY
    // ==================================================

    private void HandleAnyButtonPressed(InputControl control)
    {
        if (gameplayStarted || isLoading || isDying)
            return;

        if (control.device is Keyboard &&
            control.path.EndsWith(
                "/escape",
                StringComparison.OrdinalIgnoreCase))
        {
            InputSystem.onAnyButtonPress.CallOnce(
                HandleAnyButtonPressed
            );

            return;
        }

        StartGameplay();
    }

    private void StartGameplay()
    {
        if (gameplayStarted || isLoading || isDying)
            return;

        gameplayStarted = true;

        playerController.SetStartPreviewActive(false);
        playerController.SetGameplayInputEnabled(true);

        if (levelTimer != null)
            levelTimer.ResetTimer();

        if (startPrompt != null)
        {
            startPrompt.RemoveFromClassList("is-visible");

            startPrompt.schedule.Execute(() =>
            {
                if (startPrompt != null)
                    startPrompt.style.display = DisplayStyle.None;

            }).StartingIn(240);
        }
    }

    // ==================================================
    // PAUSE MUSIC
    // ==================================================

    private void StartPauseMusic()
    {
        gameplayMusicWasPlaying = false;

        if (gameplayMusicSource != null &&
            gameplayMusicSource.isPlaying)
        {
            gameplayMusicWasPlaying = true;
            gameplayMusicSource.Pause();
        }

        if (pauseMusicSource != null &&
            pauseMusicClip != null)
        {
            pauseMusicSource.clip = pauseMusicClip;
            pauseMusicSource.volume = pauseMusicVolume;
            pauseMusicSource.loop = true;

            pauseMusicSource.Play();
        }
    }

    private void StopPauseMusic()
    {
        if (pauseMusicSource != null)
            pauseMusicSource.Stop();

        if (gameplayMusicSource != null &&
            gameplayMusicWasPlaying)
        {
            gameplayMusicSource.UnPause();
        }

        gameplayMusicWasPlaying = false;
    }

    // ==================================================
    // PAUSE GAME
    // ==================================================

    private void PauseGame()
    {
        if (!gameplayStarted || isPaused || isLoading || isDying)
            return;

        isPaused = true;

        playerController.SetGameplayInputEnabled(false);

        if (levelTimer != null)
            levelTimer.PauseTimer();

        StartPauseMusic();

        Time.timeScale = 0f;

        if (screenKicker != null)
            screenKicker.text = "TAKE A BREATHER";

        if (screenTitle != null)
            screenTitle.text = "PAUSED";

        if (screenDescription != null)
            screenDescription.text =
                "Your run is paused. What would you like to do?";

        if (screenFooter != null)
            screenFooter.text = "PRESS ESC TO RESUME";

        if (resumeButton != null)
            resumeButton.text = "RESUME GAME";

        if (restartButton != null)
            restartButton.text = "RESTART LEVEL";

        if (mainMenuButton != null)
            mainMenuButton.text = "MAIN MENU";

        if (screenOverlay != null)
        {
            screenOverlay.style.display = DisplayStyle.Flex;
            screenOverlay.style.opacity = 1f;
        }

        if (menuFrame != null)
        {
            menuFrame.RemoveFromClassList("menu-entered");

            menuFrame.schedule.Execute(() =>
            {
                if (isPaused && menuFrame != null)
                    menuFrame.AddToClassList("menu-entered");

            }).StartingIn(20);
        }
    }

    // ==================================================
    // RESUME GAME
    // ==================================================

    private void ResumeGame()
    {
        if (!isPaused || isLoading || isDying)
            return;

        isPaused = false;

        Time.timeScale = 1f;

        StopPauseMusic();

        if (screenOverlay != null)
        {
            screenOverlay.style.display = DisplayStyle.None;
            screenOverlay.style.opacity = 0f;
        }

        if (menuFrame != null)
            menuFrame.RemoveFromClassList("menu-entered");

        playerController.SetGameplayInputEnabled(true);

        if (levelTimer != null)
            levelTimer.ResumeTimer();
    }

    // ==================================================
    // PLAYER DEATH
    // ==================================================

    public void PlayerDied(float restartDelay)
    {
        if (isLoading || isDying)
            return;

        isDying = true;
        isPaused = false;

        if (playerController != null)
            playerController.SetGameplayInputEnabled(false);

        if (levelTimer != null)
            levelTimer.PauseTimer();

        StopPauseMusic();

        StartCoroutine(DeathRestartRoutine(restartDelay));
    }

    private IEnumerator DeathRestartRoutine(float restartDelay)
    {
        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, restartDelay)
        );

        string currentScene = SceneManager.GetActiveScene().name;

        if (!Application.CanStreamedLevelBeLoaded(currentScene))
        {
            Debug.LogError(
                $"Cannot reload scene '{currentScene}'. Check the Build Profile Scene List.",
                this
            );

            isDying = false;
            yield break;
        }

        isDying = false;

        StartCoroutine(
            LoadSceneWithScreen(
                currentScene,
                "RESTARTING AFTER DEATH"
            )
        );
    }

    // ==================================================
    // RESTART LEVEL
    // ==================================================

    private void RestartLevel()
    {
        if (isLoading || isDying)
            return;

        string currentScene =
            SceneManager.GetActiveScene().name;

        if (!Application.CanStreamedLevelBeLoaded(currentScene))
        {
            Debug.LogError(
                $"Cannot reload scene '{currentScene}'. " +
                "Check the Build Profile Scene List.",
                this
            );

            return;
        }

        StartCoroutine(
            LoadSceneWithScreen(
                currentScene,
                "RESTARTING LEVEL"
            )
        );
    }

    // ==================================================
    // RETURN TO MAIN MENU
    // ==================================================

    private void ReturnToMainMenu()
    {
        if (isLoading || isDying)
            return;

        if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Debug.LogError(
                $"Scene '{mainMenuSceneName}' is missing " +
                "from the active Build Profile Scene List.",
                this
            );

            return;
        }

        StartCoroutine(
            LoadSceneWithScreen(
                mainMenuSceneName,
                "RETURNING TO MAIN MENU"
            )
        );
    }

    // ==================================================
    // LOADING SCREEN
    // ==================================================

    private IEnumerator LoadSceneWithScreen(
        string sceneName,
        string loadingMessage)
    {
        if (isLoading)
            yield break;

        isLoading = true;
        isPaused = false;
        isDying = false;

        Time.timeScale = 1f;

        StopPauseMusic();

        if (playerController != null)
            playerController.SetGameplayInputEnabled(false);

        if (levelTimer != null)
            levelTimer.PauseTimer();

        if (screenOverlay != null)
            screenOverlay.style.display = DisplayStyle.None;

        if (startPrompt != null)
            startPrompt.style.display = DisplayStyle.None;

        if (loadingTitle != null)
            loadingTitle.text = loadingMessage;

        if (loadingBarFill != null)
            loadingBarFill.style.width = Length.Percent(6f);

        if (loadingOverlay != null)
        {
            loadingOverlay.style.display = DisplayStyle.Flex;
            loadingOverlay.RemoveFromClassList("is-visible");

            yield return null;

            loadingOverlay.AddToClassList("is-visible");
        }

        yield return new WaitForSecondsRealtime(
            loadingTransitionDuration
        );

        AsyncOperation operation =
            SceneManager.LoadSceneAsync(
                sceneName,
                LoadSceneMode.Single
            );

        if (operation == null)
        {
            Debug.LogError(
                $"Could not begin loading scene '{sceneName}'.",
                this
            );

            isLoading = false;
            Time.timeScale = 1f;

            yield break;
        }

        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f)
        {
            float progress = Mathf.Clamp01(
                operation.progress / 0.9f
            );

            if (loadingBarFill != null)
            {
                loadingBarFill.style.width = Length.Percent(
                    Mathf.Lerp(6f, 92f, progress)
                );
            }

            yield return null;
        }

        if (loadingBarFill != null)
            loadingBarFill.style.width = Length.Percent(100f);

        yield return new WaitForSecondsRealtime(0.2f);

        operation.allowSceneActivation = true;
    }

    // ==================================================
    // CLEANUP
    // ==================================================

    private void OnDestroy()
    {
        if (resumeButton != null)
            resumeButton.clicked -= OnResumeClicked;

        if (restartButton != null)
            restartButton.clicked -= OnRestartClicked;

        if (mainMenuButton != null)
            mainMenuButton.clicked -= OnMainMenuClicked;

        if (root != null)
        {
            root.Query<Button>().ForEach(button =>
            {
                button.UnregisterCallback<PointerEnterEvent>(
                    OnButtonPointerEnter
                );
            });
        }

        if (pauseMusicSource != null)
            pauseMusicSource.Stop();

        Time.timeScale = 1f;
    }
}
