
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class LevelCompletionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private LevelTimerUI levelTimer;

    [Header("Gameplay Controls")]
    [Tooltip("Drag your pause menu controller component here.")]
    [SerializeField] private MonoBehaviour pauseController;

    [Header("Level Progression")]
    [SerializeField]
    private string[] levelScenes =
    {
        "Level_01",
        "Level_02",
        "Level_03",
        "Level_04",
        "Level_05"
    };

    [SerializeField] private string mainMenuScene = "MainMenu";

    private VisualElement root;
    private VisualElement completionOverlay;

    private Label completionTitle;
    private Label runTimeLabel;
    private Label bestTimeLabel;
    private Label statusLabel;
    private Label bestTimeHUD;

    private Button nextButton;
    private Button restartButton;
    private Button mainMenuButton;

    private bool isCompleted;
    private bool isLoading;
    private bool isLastLevel;

    private string currentScene;
    private string nextScene;

    private string BestTimeKey => "NARP_BEST_" + currentScene;

    private void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();

        if (levelTimer == null)
            levelTimer = FindFirstObjectByType<LevelTimerUI>();
    }

    private void Start()
    {
        if (uiDocument == null)
        {
            Debug.LogError(
                "LevelCompletionController requires a UIDocument.",
                this
            );

            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement;
        currentScene = SceneManager.GetActiveScene().name;

        // Find completion UI elements from UXML.
        completionOverlay = root.Q<VisualElement>("completion-overlay");
        completionTitle = root.Q<Label>("completion-title");
        runTimeLabel = root.Q<Label>("completion-run-time");
        bestTimeLabel = root.Q<Label>("completion-best-time");
        statusLabel = root.Q<Label>("completion-status");

        nextButton = root.Q<Button>("completion-next-button");
        restartButton = root.Q<Button>("completion-restart-button");
        mainMenuButton = root.Q<Button>("completion-menu-button");

        CreateBestTimeHUD();

        // Connect buttons.
        if (nextButton != null)
            nextButton.clicked += OnNextLevelClicked;

        if (restartButton != null)
            restartButton.clicked += OnRestartClicked;

        if (mainMenuButton != null)
            mainMenuButton.clicked += OnMainMenuClicked;

        // Validate UI.
        if (completionOverlay == null ||
            completionTitle == null ||
            runTimeLabel == null ||
            bestTimeLabel == null ||
            statusLabel == null ||
            nextButton == null ||
            restartButton == null ||
            mainMenuButton == null)
        {
            Debug.LogError(
                "Completion UI elements are missing. Check PlatformerUI.uxml.",
                this
            );

            enabled = false;
            return;
        }

        completionOverlay.style.display = DisplayStyle.None;

        if (playerController == null)
            Debug.LogError("Cannot find PlayerController.", this);

        if (levelTimer == null)
            Debug.LogError("Cannot find LevelTimerUI.", this);
    }

    private void Update()
    {
        UpdateBestTimeHUD();
    }

    // ==========================================
    // COMPLETE LEVEL
    // ==========================================

    public void CompleteLevel()
    {
        if (isCompleted || isLoading)
            return;

        if (completionOverlay == null)
        {
            Debug.LogError("Completion overlay is missing.", this);
            return;
        }

        isCompleted = true;

        // Disable player input.
        if (playerController != null)
        {
            playerController.SetGameplayInputEnabled(false);

            Rigidbody2D rb =
                playerController.GetComponent<Rigidbody2D>();

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
        }

        // Disable pause menu input.
        if (pauseController != null)
        {
            pauseController.enabled = false;
        }

        // Stop timer and save personal best.
        if (levelTimer != null)
        {
            levelTimer.CompleteLevel();
        }

        // Update completion screen information.
        UpdateCompletionInformation();

        // Show completion screen.
        completionOverlay.style.display = DisplayStyle.Flex;
        completionOverlay.style.opacity = 1f;

        // Apply the visible animation class immediately.
        completionOverlay.AddToClassList("is-visible");

        // Freeze the entire gameplay world.
        // UI Toolkit remains interactive.
        Time.timeScale = 0f;

        Debug.Log("LEVEL COMPLETED - GAMEPLAY FROZEN");
    }

    // ==========================================
    // COMPLETION INFORMATION
    // ==========================================

    private void UpdateCompletionInformation()
    {
        currentScene = SceneManager.GetActiveScene().name;

        float runTime = levelTimer != null
            ? levelTimer.GetElapsedTime()
            : 0f;

        float bestTime = PlayerPrefs.GetFloat(
            BestTimeKey,
            -1f
        );

        runTimeLabel.text = FormatTime(runTime);

        bestTimeLabel.text = bestTime < 0f
            ? "--:--.--"
            : FormatTime(bestTime);

        int currentIndex = Array.IndexOf(
            levelScenes,
            currentScene
        );

        isLastLevel =
            currentIndex < 0 ||
            currentIndex >= levelScenes.Length - 1;

        nextScene = isLastLevel
            ? null
            : levelScenes[currentIndex + 1];

        if (isLastLevel)
        {
            completionTitle.text = "ALL LEVELS COMPLETE!";

            nextButton.text = "ALL LEVELS COMPLETE";
            nextButton.SetEnabled(false);

            statusLabel.text =
                "Congratulations! You have completed every level.";
        }
        else
        {
            completionTitle.text = "LEVEL COMPLETE!";

            nextButton.text = "NEXT LEVEL  >";
            nextButton.SetEnabled(true);

            statusLabel.text =
                "Get ready for " + nextScene.Replace("_", " ") + ".";
        }

        UpdateBestTimeHUD();
    }

    private string FormatTime(float time)
    {
        int centiseconds = Mathf.FloorToInt(time * 100f);

        int minutes = centiseconds / 6000;
        int seconds = (centiseconds / 100) % 60;
        int fraction = centiseconds % 100;

        if (minutes >= 60)
        {
            int hours = minutes / 60;
            minutes %= 60;

            return $"{hours:00}:{minutes:00}:{seconds:00}.{fraction:00}";
        }

        return $"{minutes:00}:{seconds:00}.{fraction:00}";
    }

    // ==========================================
    // BEST TIME HUD
    // ==========================================

    private void CreateBestTimeHUD()
    {
        bestTimeHUD = root.Q<Label>("best-time");

        if (bestTimeHUD == null)
        {
            Label timerHUD = root.Q<Label>("run-timer");

            if (timerHUD != null && timerHUD.parent != null)
            {
                bestTimeHUD = new Label();
                bestTimeHUD.name = "best-time";
                bestTimeHUD.AddToClassList("timer-best");

                int index = timerHUD.parent.IndexOf(timerHUD);

                timerHUD.parent.Insert(
                    index + 1,
                    bestTimeHUD
                );
            }
        }

        UpdateBestTimeHUD();
    }

    private void UpdateBestTimeHUD()
    {
        if (bestTimeHUD == null)
            return;

        string sceneName = SceneManager.GetActiveScene().name;

        float best = PlayerPrefs.GetFloat(
            "NARP_BEST_" + sceneName,
            -1f
        );

        bestTimeHUD.text = best < 0f
            ? "BEST  --:--.--"
            : "BEST  " + FormatTime(best);
    }

    // ==========================================
    // BUTTON ACTIONS
    // ==========================================

    private void OnNextLevelClicked()
    {
        if (isLoading || isLastLevel ||
            string.IsNullOrEmpty(nextScene))
            return;

        StartCoroutine(LoadSceneRoutine(nextScene));
    }

    private void OnRestartClicked()
    {
        if (isLoading)
            return;

        StartCoroutine(
            LoadSceneRoutine(
                SceneManager.GetActiveScene().name
            )
        );
    }

    private void OnMainMenuClicked()
    {
        if (isLoading)
            return;

        StartCoroutine(LoadSceneRoutine(mainMenuScene));
    }

    // ==========================================
    // SCENE LOADING
    // ==========================================

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        if (isLoading)
            yield break;

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                "Cannot load scene '" + sceneName +
                "'. Add it to the active Build Profile Scene List.",
                this
            );

            yield break;
        }

        isLoading = true;

        if (nextButton != null)
            nextButton.SetEnabled(false);

        if (restartButton != null)
            restartButton.SetEnabled(false);

        if (mainMenuButton != null)
            mainMenuButton.SetEnabled(false);

        if (statusLabel != null)
            statusLabel.text = "LOADING...";

        // Restore time before loading.
        Time.timeScale = 1f;

        AsyncOperation operation =
            SceneManager.LoadSceneAsync(
                sceneName,
                LoadSceneMode.Single
            );

        if (operation == null)
        {
            Debug.LogError(
                "Scene loading could not start.",
                this
            );

            isLoading = false;
            yield break;
        }

        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f)
        {
            yield return null;
        }

        operation.allowSceneActivation = true;
    }

    // ==========================================
    // CLEANUP
    // ==========================================

    private void OnDestroy()
    {
        if (nextButton != null)
            nextButton.clicked -= OnNextLevelClicked;

        if (restartButton != null)
            restartButton.clicked -= OnRestartClicked;

        if (mainMenuButton != null)
            mainMenuButton.clicked -= OnMainMenuClicked;
    }
}
