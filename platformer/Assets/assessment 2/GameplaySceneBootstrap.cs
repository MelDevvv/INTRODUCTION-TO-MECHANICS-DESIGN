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

    private void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        Time.timeScale = 1f;
    }

    private void Start()
    {
        if (uiDocument == null)
        {
            Debug.LogError("GameplaySceneBootstrap requires a UIDocument.", this);
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
            Debug.LogError("GameplaySceneBootstrap cannot find PlayerController.", this);
            enabled = false;
            return;
        }

        // Register the pause menu buttons directly.
        if (resumeButton != null)
            resumeButton.clicked += ResumeGame;
        else
            Debug.LogError("Missing primary-button in PlatformerUI.uxml.", this);

        if (restartButton != null)
            restartButton.clicked += RestartLevel;
        else
            Debug.LogError("Missing secondary-button in PlatformerUI.uxml.", this);

        if (mainMenuButton != null)
            mainMenuButton.clicked += ReturnToMainMenu;
        else
            Debug.LogError("Missing tertiary-button in PlatformerUI.uxml.", this);

        // Keep player animation running, but disable gameplay controls.
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
            Debug.LogError("Missing start-prompt in PlatformerUI.uxml.", this);
        }

        // Any keyboard, mouse or gamepad button starts the level.
        InputSystem.onAnyButtonPress.CallOnce(HandleAnyButtonPressed);
    }

    private void Update()
    {
        if (isLoading)
            return;

        // Escape toggles pause/resume.
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

        // Gamepad Start toggles pause/resume.
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

        // Allow analogue stick movement to start the run.
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
    // ANY INPUT STARTS GAMEPLAY
    // ==================================================

    private void HandleAnyButtonPressed(InputControl control)
    {
        if (gameplayStarted || isLoading)
            return;

        // Escape is reserved for pausing.
        if (control.device is Keyboard &&
            control.path.EndsWith("/escape", StringComparison.OrdinalIgnoreCase))
        {
            InputSystem.onAnyButtonPress.CallOnce(HandleAnyButtonPressed);
            return;
        }

        StartGameplay();
    }

    private void StartGameplay()
    {
        if (gameplayStarted || isLoading)
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
    // PAUSE MENU
    // ==================================================

    private void PauseGame()
    {
        if (!gameplayStarted || isPaused || isLoading)
            return;

        isPaused = true;

        playerController.SetGameplayInputEnabled(false);

        if (levelTimer != null)
            levelTimer.PauseTimer();

        Time.timeScale = 0f;

        if (screenKicker != null)
            screenKicker.text = "TAKE A BREATHER";

        if (screenTitle != null)
            screenTitle.text = "PAUSED";

        if (screenDescription != null)
            screenDescription.text = "Your run is paused. What would you like to do?";

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

    private void ResumeGame()
    {
        if (!isPaused || isLoading)
            return;

        isPaused = false;

        Time.timeScale = 1f;

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
    // RESTART LEVEL
    // ==================================================

    private void RestartLevel()
    {
        if (isLoading)
            return;

        string currentScene = SceneManager.GetActiveScene().name;

        if (!Application.CanStreamedLevelBeLoaded(currentScene))
        {
            Debug.LogError(
                $"Cannot reload scene '{currentScene}'. Check the Build Profile Scene List.",
                this
            );

            return;
        }

        StartCoroutine(
            LoadSceneWithScreen(currentScene, "RESTARTING LEVEL")
        );
    }

    // ==================================================
    // RETURN TO MAIN MENU
    // ==================================================

    private void ReturnToMainMenu()
    {
        if (isLoading)
            return;

        if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Debug.LogError(
                $"Scene '{mainMenuSceneName}' is missing from the active Build Profile Scene List.",
                this
            );

            return;
        }

        StartCoroutine(
            LoadSceneWithScreen(mainMenuSceneName, "RETURNING TO MAIN MENU")
        );
    }

    // ==================================================
    // LOADING SCREEN
    // ==================================================

    private IEnumerator LoadSceneWithScreen(string sceneName, string loadingMessage)
    {
        if (isLoading)
            yield break;

        isLoading = true;
        isPaused = false;

        Time.timeScale = 1f;

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

        yield return new WaitForSecondsRealtime(loadingTransitionDuration);

        AsyncOperation operation = SceneManager.LoadSceneAsync(
            sceneName,
            LoadSceneMode.Single
        );

        if (operation == null)
        {
            Debug.LogError($"Could not begin loading scene '{sceneName}'.", this);

            isLoading = false;
            Time.timeScale = 1f;

            yield break;
        }

        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);

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
        // Remove button callbacks so they cannot be registered twice.
        if (resumeButton != null)
            resumeButton.clicked -= ResumeGame;

        if (restartButton != null)
            restartButton.clicked -= RestartLevel;

        if (mainMenuButton != null)
            mainMenuButton.clicked -= ReturnToMainMenu;

        Time.timeScale = 1f;
    }
}