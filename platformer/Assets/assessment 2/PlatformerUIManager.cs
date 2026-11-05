
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(UIDocument))]
public class PlatformerUIManager : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("Leave empty to reload the current scene.")]
    [SerializeField] private string gameplaySceneName = "";

    [SerializeField] private string levelDisplayName = "01";

    private enum ScreenState
    {
        MainMenu,
        Playing,
        Paused,
        Dead,
        Complete
    }

    private ScreenState state;

    private static int sessionDeaths;
    private static bool reloadIntoGameplayAfterLoad;

    private UIDocument uiDocument;
    private VisualElement root;
    private VisualElement overlay;
    private VisualElement toast;
    private VisualElement controlsHint;

    private Label deathCountLabel;
    private Label levelLabel;
    private Label titleLabel;
    private Label kickerLabel;
    private Label descriptionLabel;
    private Label footerLabel;
    private Label toastLabel;

    private Button primaryButton;
    private Button secondaryButton;
    private Button tertiaryButton;

    private bool mainMenuPlayReloadsScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSessionValues()
    {
        sessionDeaths = 0;
        reloadIntoGameplayAfterLoad = false;
    }

    private void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();
        root = uiDocument.rootVisualElement;

        if (root == null)
        {
            Debug.LogError(
                "PlatformerUIManager: UIDocument has no visual tree. Assign PlatformerUI.uxml.",
                this
            );

            enabled = false;
            return;
        }

        // Find UI elements from UXML
        overlay = root.Q<VisualElement>("screen-overlay");
        toast = root.Q<VisualElement>("toast");
        controlsHint = root.Q<VisualElement>("controls-hint");

        deathCountLabel = root.Q<Label>("death-count");
        levelLabel = root.Q<Label>("level-name");

        titleLabel = root.Q<Label>("screen-title");
        kickerLabel = root.Q<Label>("screen-kicker");
        descriptionLabel = root.Q<Label>("screen-description");
        footerLabel = root.Q<Label>("screen-footer");
        toastLabel = root.Q<Label>("toast-label");

        primaryButton = root.Q<Button>("primary-button");
        secondaryButton = root.Q<Button>("secondary-button");
        tertiaryButton = root.Q<Button>("tertiary-button");

        if (!ValidateUIReferences())
        {
            enabled = false;
            return;
        }

        // Connect buttons
        primaryButton.clicked += OnPrimaryPressed;
        secondaryButton.clicked += OnSecondaryPressed;
        tertiaryButton.clicked += OnTertiaryPressed;

        levelLabel.text = levelDisplayName;

        RefreshDeathCounter();

        if (reloadIntoGameplayAfterLoad)
        {
            reloadIntoGameplayAfterLoad = false;
            StartGameplay();
        }
        else
        {
            ShowMainMenu(false);
        }
    }

    private void OnDisable()
    {
        if (primaryButton != null)
            primaryButton.clicked -= OnPrimaryPressed;

        if (secondaryButton != null)
            secondaryButton.clicked -= OnSecondaryPressed;

        if (tertiaryButton != null)
            tertiaryButton.clicked -= OnTertiaryPressed;
    }

    private bool ValidateUIReferences()
    {
        bool valid = true;

        valid &= CheckReference(overlay, "screen-overlay");
        valid &= CheckReference(deathCountLabel, "death-count");
        valid &= CheckReference(levelLabel, "level-name");
        valid &= CheckReference(titleLabel, "screen-title");
        valid &= CheckReference(kickerLabel, "screen-kicker");
        valid &= CheckReference(descriptionLabel, "screen-description");
        valid &= CheckReference(footerLabel, "screen-footer");
        valid &= CheckReference(primaryButton, "primary-button");
        valid &= CheckReference(secondaryButton, "secondary-button");
        valid &= CheckReference(tertiaryButton, "tertiary-button");

        return valid;
    }

    // Correct type for UI Toolkit elements
    private bool CheckReference(VisualElement reference, string elementName)
    {
        if (reference != null)
            return true;

        Debug.LogError(
            "PlatformerUIManager: Could not find '" +
            elementName +
            "' in PlatformerUI.uxml. Check that you are using the matching UXML file.",
            this
        );

        return false;
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (!Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (state == ScreenState.Playing)
        {
            PauseGame();
        }
        else if (state == ScreenState.Paused)
        {
            ResumeGame();
        }
    }

    private void SetScreen(
        ScreenState newState,
        string kicker,
        string title,
        string description,
        string primaryText,
        string secondaryText,
        string tertiaryText,
        string footer)
    {
        state = newState;

        kickerLabel.text = kicker;
        titleLabel.text = title;
        descriptionLabel.text = description;
        footerLabel.text = footer;

        SetButtonText(primaryButton, primaryText);
        SetButtonText(secondaryButton, secondaryText);
        SetButtonText(tertiaryButton, tertiaryText);

        tertiaryButton.EnableInClassList(
            "hidden",
            string.IsNullOrEmpty(tertiaryText)
        );

        secondaryButton.SetEnabled(!string.IsNullOrEmpty(secondaryText));
        tertiaryButton.SetEnabled(!string.IsNullOrEmpty(tertiaryText));

        overlay.RemoveFromClassList("hidden");
        overlay.style.display = DisplayStyle.Flex;
        overlay.style.opacity = 0f;

        overlay.schedule.Execute(() =>
        {
            if (overlay != null)
                overlay.style.opacity = 1f;

        }).StartingIn(20);

        if (controlsHint != null)
        {
            controlsHint.style.display =
                newState == ScreenState.Playing
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }

    private void SetButtonText(Button button, string text)
    {
        button.text = text;

        button.SetEnabled(!string.IsNullOrEmpty(text));

        button.style.color =
            new StyleColor(new Color32(255, 248, 224, 255));

        button.style.unityTextAlign = TextAnchor.MiddleCenter;
        button.style.fontSize = 14;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
    }

    private void HideScreen()
    {
        if (overlay == null)
            return;

        overlay.style.opacity = 0f;

        overlay.schedule.Execute(() =>
        {
            if (overlay != null && state == ScreenState.Playing)
            {
                overlay.AddToClassList("hidden");
            }

        }).StartingIn(230);

        if (controlsHint != null)
            controlsHint.style.display = DisplayStyle.Flex;
    }

    // MAIN MENU
    private void ShowMainMenu(bool playShouldRestart)
    {
        mainMenuPlayReloadsScene = playShouldRestart;

        Time.timeScale = 0f;

        SetScreen(
            ScreenState.MainMenu,
            "A LITTLE PIXEL ADVENTURE",
            "CUBE//RUN",
            playShouldRestart
                ? "Start a fresh run and jump back into the level."
                : "Run, jump and find your way across the world.",
            playShouldRestart ? "START NEW RUN" : "START GAME",
            "QUIT GAME",
            "",
            "A / D OR ARROWS TO MOVE     SPACE TO JUMP"
        );
    }

    // START GAME
    private void StartGameplay()
    {
        state = ScreenState.Playing;

        Time.timeScale = 1f;

        HideScreen();

        ShowToast("LET'S GO!");
    }

    // PAUSE
    private void PauseGame()
    {
        Time.timeScale = 0f;

        SetScreen(
            ScreenState.Paused,
            "TAKE A BREATHER",
            "PAUSED",
            "Your run is waiting. What would you like to do?",
            "RESUME GAME",
            "RESTART LEVEL",
            "MAIN MENU",
            "PRESS ESC TO RESUME"
        );
    }

    private void ResumeGame()
    {
        StartGameplay();
    }

    // DEATH SCREEN
    // Call this once when the player dies.
    public void ShowDeathScreen()
    {
        if (state != ScreenState.Playing)
            return;

        sessionDeaths++;

        RefreshDeathCounter();

        Time.timeScale = 0f;

        SetScreen(
            ScreenState.Dead,
            "THAT WAS A CLOSE ONE...",
            "YOU DIED",
            "Death number " + sessionDeaths + ". Have another go?",
            "TRY AGAIN",
            "MAIN MENU",
            "",
            "YOUR DEATH TOTAL IS KEPT FOR THIS PLAY SESSION"
        );
    }

    // COMPLETION SCREEN
    public void ShowCompletionScreen()
    {
        if (state != ScreenState.Playing)
            return;

        Time.timeScale = 0f;

        SetScreen(
            ScreenState.Complete,
            "STAGE CLEARED",
            "LEVEL COMPLETE!",
            "You made it through. Nice work!",
            "PLAY AGAIN",
            "MAIN MENU",
            "",
            "READY FOR ANOTHER RUN?"
        );
    }

    // DEATH COUNTER
    private void RefreshDeathCounter()
    {
        if (deathCountLabel != null)
        {
            deathCountLabel.text = sessionDeaths.ToString("00");
        }
    }

    // PRIMARY BUTTON
    private void OnPrimaryPressed()
    {
        switch (state)
        {
            case ScreenState.MainMenu:

                if (mainMenuPlayReloadsScene)
                    RestartIntoGameplay();
                else
                    StartGameplay();

                break;

            case ScreenState.Paused:

                ResumeGame();

                break;

            case ScreenState.Dead:
            case ScreenState.Complete:

                RestartIntoGameplay();

                break;
        }
    }

    // SECONDARY BUTTON
    private void OnSecondaryPressed()
    {
        switch (state)
        {
            case ScreenState.MainMenu:

                QuitGame();

                break;

            case ScreenState.Paused:

                RestartIntoGameplay();

                break;

            case ScreenState.Dead:
            case ScreenState.Complete:

                ShowMainMenu(true);

                break;
        }
    }

    // THIRD BUTTON
    private void OnTertiaryPressed()
    {
        if (state == ScreenState.Paused)
        {
            ShowMainMenu(true);
        }
    }

    // RESTART LEVEL
    public void RestartIntoGameplay()
    {
        reloadIntoGameplayAfterLoad = true;

        Time.timeScale = 1f;

        string targetScene =
            string.IsNullOrWhiteSpace(gameplaySceneName)
            ? SceneManager.GetActiveScene().name
            : gameplaySceneName;

        SceneManager.LoadScene(targetScene);
    }

    // QUIT GAME
    private void QuitGame()
    {
        Time.timeScale = 1f;

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // TOAST NOTIFICATION
    public void ShowToast(string message)
    {
        if (toast == null || toastLabel == null)
            return;

        toastLabel.text = message;

        toast.RemoveFromClassList("hidden");
        toast.AddToClassList("visible");

        toast.schedule.Execute(() =>
        {
            if (toast != null)
                toast.RemoveFromClassList("visible");

        }).StartingIn(1800);
    }

    private void OnApplicationQuit()
    {
        Time.timeScale = 1f;
    }
}
