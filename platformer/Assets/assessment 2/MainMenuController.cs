
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Runtime UI controller for the NOT A REAL PLATFORMER main menu.
/// Includes floating cube animation and mouse-following eyes.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MainMenuController : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField]
    private string[] levelSceneNames =
    {
        "Level_01",
        "Level_02",
        "Level_03",
        "Level_04",
        "Level_05"
    };

    [SerializeField] private string practiceSceneName = "Practice";

    [Header("Transitions")]
    [Min(0f)]
    [SerializeField] private float transitionDuration = 0.35f;

    [Header("Menu Animation")]
    [Min(0.1f)]
    [SerializeField] private float cubeFloatInterval = 1.1f;

    [Header("Cube Eye Tracking")]
    [SerializeField] private float eyeMoveX = 14f;
    [SerializeField] private float eyeMoveY = 12f;

    [Min(0.01f)]
    [SerializeField] private float eyeSmoothTime = 0.05f;

    private UIDocument uiDocument;
    private VisualElement root;
    private VisualElement menuRoot;
    private VisualElement mainScreen;
    private VisualElement levelSelectScreen;
    private VisualElement transitionOverlay;
    private VisualElement heroCube;

    private VisualElement leftEye;
    private VisualElement rightEye;

    private Label mainStatus;
    private Label levelStatus;

    private Button playButton;
    private Button levelSelectButton;
    private Button practiceButton;
    private Button quitButton;
    private Button backButton;

    private Button[] levelButtons;
    private Action[] levelButtonCallbacks;

    private bool isLoading;
    private bool cubeFloatingHigh;
    private float nextCubeFloatTime;

    private Vector2 targetEyeOffset;
    private Vector2 currentEyeOffset;
    private Vector2 eyeVelocity;

    private void Awake()
    {
        Time.timeScale = 1f;
    }

    private void Start()
    {
        uiDocument = GetComponent<UIDocument>();
        root = uiDocument.rootVisualElement;

        menuRoot = root.Q<VisualElement>("menu-root");

        mainScreen = root.Q<VisualElement>("main-screen");
        levelSelectScreen = root.Q<VisualElement>("level-select-screen");
        transitionOverlay = root.Q<VisualElement>("transition-overlay");
        heroCube = root.Q<VisualElement>("hero-cube");

        // Find the two existing eyes inside the cube.
        if (heroCube != null)
        {
            leftEye = heroCube.Q<VisualElement>(
                className: "eye-left"
            );

            rightEye = heroCube.Q<VisualElement>(
                className: "eye-right"
            );
        }

        mainStatus = root.Q<Label>("menu-status");
        levelStatus = root.Q<Label>("level-status");

        playButton = root.Q<Button>("play-button");
        levelSelectButton = root.Q<Button>("level-select-button");
        practiceButton = root.Q<Button>("practice-button");
        quitButton = root.Q<Button>("quit-button");
        backButton = root.Q<Button>("back-button");

        levelButtons = new Button[5];
        levelButtonCallbacks = new Action[5];

        if (!ValidateUI())
        {
            enabled = false;
            return;
        }

        // Main menu buttons.
        playButton.clicked += PlayFirstLevel;
        levelSelectButton.clicked += ShowLevelSelect;
        practiceButton.clicked += PlayPractice;
        quitButton.clicked += QuitGame;
        backButton.clicked += ShowMainScreen;

        // Level selection buttons.
        for (int i = 0; i < levelButtons.Length; i++)
        {
            int levelIndex = i;

            levelButtons[i] = root.Q<Button>(
                $"level-{i + 1}-button"
            );

            if (levelButtons[i] == null)
            {
                Debug.LogError(
                    $"MainMenuController: Missing level-{i + 1}-button in MainMenu.uxml.",
                    this
                );

                continue;
            }

            levelButtonCallbacks[i] = () => PlayLevel(levelIndex);

            levelButtons[i].clicked += levelButtonCallbacks[i];
        }

        Button practiceLevelButton =
            root.Q<Button>("practice-level-button");

        if (practiceLevelButton != null)
            practiceLevelButton.clicked += PlayPractice;

        ShowMainScreen();

        if (menuRoot != null)
            menuRoot.AddToClassList("intro-visible");

        mainScreen.style.display = DisplayStyle.Flex;
        levelSelectScreen.style.display = DisplayStyle.None;

        // Listen for mouse movement.
        root.RegisterCallback<PointerMoveEvent>(HandlePointerMove);
        root.RegisterCallback<PointerLeaveEvent>(HandlePointerLeave);

        nextCubeFloatTime =
            Time.unscaledTime + cubeFloatInterval;
    }

    private void Update()
    {
        if (isLoading || heroCube == null)
            return;

        // Floating cube animation.
        if (Time.unscaledTime >= nextCubeFloatTime)
        {
            cubeFloatingHigh = !cubeFloatingHigh;

            if (cubeFloatingHigh)
                heroCube.AddToClassList("float-high");
            else
                heroCube.RemoveFromClassList("float-high");

            nextCubeFloatTime =
                Time.unscaledTime + cubeFloatInterval;
        }

        // Smoothly move both eyes towards the mouse.
        UpdateEyeTracking();
    }

    // ==================================================
    // EYE TRACKING
    // ==================================================

    private void HandlePointerMove(PointerMoveEvent evt)
    {
        if (heroCube == null || leftEye == null || rightEye == null)
            return;

        Rect cubeBounds = heroCube.worldBound;

        if (cubeBounds.width <= 0f || cubeBounds.height <= 0f)
            return;

        Vector2 cubeCentre = cubeBounds.center;

        Vector2 mousePosition = evt.position;

        float horizontal = Mathf.Clamp(
            (mousePosition.x - cubeCentre.x) / (cubeBounds.width * 0.5f),
            -1f,
            1f
        );

        float vertical = Mathf.Clamp(
            (mousePosition.y - cubeCentre.y) / (cubeBounds.height * 0.5f),
            -1f,
            1f
        );

        targetEyeOffset = new Vector2(
            horizontal * eyeMoveX,
            vertical * eyeMoveY
        );
    }

    private void HandlePointerLeave(PointerLeaveEvent evt)
    {
        targetEyeOffset = Vector2.zero;
    }

    private void UpdateEyeTracking()
    {
        if (leftEye == null || rightEye == null)
            return;

        currentEyeOffset = Vector2.SmoothDamp(
            currentEyeOffset,
            targetEyeOffset,
            ref eyeVelocity,
            eyeSmoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime
        );

        Translate eyeTranslation = new Translate(
            new Length(currentEyeOffset.x),
            new Length(currentEyeOffset.y),
            0f
        );

        leftEye.style.translate = eyeTranslation;
        rightEye.style.translate = eyeTranslation;
    }

    // ==================================================
    // UI VALIDATION
    // ==================================================

    private bool ValidateUI()
    {
        if (menuRoot == null ||
            mainScreen == null ||
            levelSelectScreen == null ||
            transitionOverlay == null ||
            playButton == null ||
            levelSelectButton == null ||
            practiceButton == null ||
            backButton == null)
        {
            Debug.LogError(
                "MainMenuController: MainMenu.uxml is missing one or more required elements. Check that the correct UXML is assigned.",
                this
            );

            return false;
        }

        if (levelSceneNames == null || levelSceneNames.Length < 5)
        {
            Debug.LogError(
                "MainMenuController needs five level scene names.",
                this
            );

            return false;
        }

        if (leftEye == null || rightEye == null)
        {
            Debug.LogWarning(
                "MainMenuController: Could not find both cube eyes. Check eye-left and eye-right in the UXML.",
                this
            );
        }

        return true;
    }

    // ==================================================
    // SCREEN NAVIGATION
    // ==================================================

    private void ShowMainScreen()
    {
        if (isLoading)
            return;

        mainScreen.style.display = DisplayStyle.Flex;
        levelSelectScreen.style.display = DisplayStyle.None;

        if (mainStatus != null)
            mainStatus.text = string.Empty;

        if (levelStatus != null)
            levelStatus.text = string.Empty;
    }

    private void ShowLevelSelect()
    {
        if (isLoading)
            return;

        mainScreen.style.display = DisplayStyle.None;
        levelSelectScreen.style.display = DisplayStyle.Flex;

        if (levelStatus != null)
            levelStatus.text = string.Empty;
    }

    // ==================================================
    // LEVEL BUTTONS
    // ==================================================

    private void PlayFirstLevel()
    {
        PlayLevel(0);
    }

    private void PlayLevel(int index)
    {
        if (index < 0 || index >= levelSceneNames.Length)
            return;

        LoadScene(levelSceneNames[index]);
    }

    private void PlayPractice()
    {
        LoadScene(practiceSceneName);
    }

    // ==================================================
    // SCENE LOADING
    // ==================================================

    private void LoadScene(string sceneName)
    {
        if (isLoading)
            return;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            ShowStatus("No scene name has been assigned.", true);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            ShowStatus(
                $"Scene '{sceneName}' was not found. Check its name and add it to the Build Profile Scene List.",
                true
            );

            Debug.LogError(
                $"MainMenuController: Scene '{sceneName}' cannot be loaded. Check the scene name and Build Profiles > Scene List.",
                this
            );

            return;
        }

        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        isLoading = true;
        Time.timeScale = 1f;

        transitionOverlay.style.display = DisplayStyle.Flex;
        transitionOverlay.RemoveFromClassList("active");

        yield return null;

        transitionOverlay.AddToClassList("active");

        yield return new WaitForSecondsRealtime(
            transitionDuration
        );

        AsyncOperation operation =
            SceneManager.LoadSceneAsync(sceneName);

        if (operation == null)
        {
            isLoading = false;

            ShowStatus(
                $"Could not start loading '{sceneName}'.",
                true
            );

            yield break;
        }

        while (!operation.isDone)
            yield return null;
    }

    // ==================================================
    // STATUS / QUIT
    // ==================================================

    private void ShowStatus(string message, bool error)
    {
        Label target =
            levelSelectScreen.resolvedStyle.display != DisplayStyle.None
                ? levelStatus
                : mainStatus;

        if (target != null)
            target.text = message;

        Debug.LogWarning(message, this);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        Debug.Log(
            "Quit button pressed. Application.Quit works in a built game."
        );
#else
        Application.Quit();
#endif
    }

    // ==================================================
    // CLEANUP
    // ==================================================

    private void OnDestroy()
    {
        if (root != null)
        {
            root.UnregisterCallback<PointerMoveEvent>(
                HandlePointerMove
            );

            root.UnregisterCallback<PointerLeaveEvent>(
                HandlePointerLeave
            );
        }

        if (playButton != null)
            playButton.clicked -= PlayFirstLevel;

        if (levelSelectButton != null)
            levelSelectButton.clicked -= ShowLevelSelect;

        if (practiceButton != null)
            practiceButton.clicked -= PlayPractice;

        if (quitButton != null)
            quitButton.clicked -= QuitGame;

        if (backButton != null)
            backButton.clicked -= ShowMainScreen;

        if (levelButtons != null && levelButtonCallbacks != null)
        {
            for (int i = 0; i < levelButtons.Length; i++)
            {
                if (levelButtons[i] != null &&
                    levelButtonCallbacks[i] != null)
                {
                    levelButtons[i].clicked -= levelButtonCallbacks[i];
                }
            }
        }

        Button practiceLevelButton =
            root != null
                ? root.Q<Button>("practice-level-button")
                : null;

        if (practiceLevelButton != null)
            practiceLevelButton.clicked -= PlayPractice;
    }
}
