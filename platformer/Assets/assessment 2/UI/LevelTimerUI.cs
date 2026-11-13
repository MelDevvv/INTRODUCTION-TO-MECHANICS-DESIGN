using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Displays an elapsed run timer in the existing UI Toolkit gameplay HUD.
/// Uses Time.deltaTime, so the timer pauses when Time.timeScale is set to zero.
/// Call CompleteLevel() from your level finish trigger to save a best time.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class LevelTimerUI : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Level Display")]
    [SerializeField] private string levelDisplayName = "LEVEL 01";

    [Header("Timer")]
    [SerializeField] private bool startAutomatically = true;
    [SerializeField] private bool saveBestTime = true;
    [Min(0.02f)]
    [SerializeField] private float displayRefreshInterval = 0.05f;

    private Label timerLabel;
    private Label bestTimeLabel;
    private Label levelLabel;

    private float elapsedTime;
    private float refreshTimer;
    private bool isRunning;
    private bool isCompleted;

    private string BestTimeKey =>
        "NARP_BEST_" + SceneManagerSafeName();

    private void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();
    }

    private void Start()
    {
        if (uiDocument == null)
        {
            Debug.LogError("LevelTimerUI needs a UIDocument reference.", this);
            enabled = false;
            return;
        }

        VisualElement root = uiDocument.rootVisualElement;

        timerLabel = root.Q<Label>("run-timer");
        bestTimeLabel = root.Q<Label>("best-time");
        levelLabel = root.Q<Label>("level-name");

        if (timerLabel == null)
        {
            Debug.LogError(
                "LevelTimerUI could not find 'run-timer'. Use the supplied PlatformerUI.uxml.",
                this
            );
            enabled = false;
            return;
        }

        if (levelLabel != null)
            levelLabel.text = levelDisplayName;

        elapsedTime = 0f;
        refreshTimer = 0f;
        isCompleted = false;

        UpdateTimerDisplay();
        UpdateBestTimeDisplay();

        isRunning = startAutomatically;
    }

    private void Update()
    {
        if (!isRunning || isCompleted)
            return;

        // Time.deltaTime respects normal Unity pause behaviour.
        elapsedTime += Time.deltaTime;
        refreshTimer += Time.deltaTime;

        if (refreshTimer >= displayRefreshInterval)
        {
            refreshTimer = 0f;
            UpdateTimerDisplay();
        }
    }

    public float GetElapsedTime()
    {
        return elapsedTime;
    }

    public void PauseTimer()
    {
        isRunning = false;
        UpdateTimerDisplay();
    }

    public void ResumeTimer()
    {
        if (!isCompleted)
            isRunning = true;
    }

    public void ResetTimer()
    {
        elapsedTime = 0f;
        refreshTimer = 0f;
        isCompleted = false;
        isRunning = true;

        UpdateTimerDisplay();
    }

    /// <summary>
    /// Call this from your finish trigger when the player completes the level.
    /// Stops the timer and saves a new personal best when applicable.
    /// </summary>
    public void CompleteLevel()
    {
        if (isCompleted)
            return;

        isRunning = false;
        isCompleted = true;

        if (saveBestTime)
        {
            float previousBest = PlayerPrefs.GetFloat(BestTimeKey, -1f);

            if (previousBest < 0f || elapsedTime < previousBest)
            {
                PlayerPrefs.SetFloat(BestTimeKey, elapsedTime);
                PlayerPrefs.Save();
            }
        }

        UpdateTimerDisplay();
        UpdateBestTimeDisplay();
    }

    private void UpdateTimerDisplay()
    {
        if (timerLabel == null)
            return;

        timerLabel.text = FormatTime(elapsedTime);
    }

    private void UpdateBestTimeDisplay()
    {
        if (bestTimeLabel == null)
            return;

        float best = PlayerPrefs.GetFloat(BestTimeKey, -1f);

        bestTimeLabel.text = best < 0f
            ? "BEST  --:--.--"
            : "BEST  " + FormatTime(best);
    }

    private string FormatTime(float time)
    {
        int totalCentiseconds = Mathf.FloorToInt(time * 100f);
        int minutes = totalCentiseconds / 6000;
        int seconds = (totalCentiseconds / 100) % 60;
        int centiseconds = totalCentiseconds % 100;

        if (minutes >= 60)
        {
            int hours = minutes / 60;
            minutes %= 60;
            return $"{hours:00}:{minutes:00}:{seconds:00}.{centiseconds:00}";
        }

        return $"{minutes:00}:{seconds:00}.{centiseconds:00}";
    }

    private string SceneManagerSafeName()
    {
        return UnityEngine.SceneManagement.SceneManager
            .GetActiveScene().name;
    }
}
