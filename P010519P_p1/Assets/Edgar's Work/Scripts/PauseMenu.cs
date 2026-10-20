using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PauseMenuWithSettings : MonoBehaviour
{
    [Header("Windows")]
    public GameObject pauseMenuUI;
    public GameObject settingsMenuUI;

    [Header("Controller Buttons")]
    public Button firstPauseButton;
    public Button firstSettingsButton;

    [Header("Audio Sources")]
    public AudioSource gameMusic;
    public AudioSource pauseMusic;
    public Slider musicVolumeSlider;

    [Header("The Visualizer")]
    public BassPostProcessPulse bassVisualizer;

    [Header("Effect Toggles")]
    public Toggle bloomToggle;
    public Toggle chromaticToggle;
    public Toggle lensToggle;
    public Toggle vignetteToggle;
    public Toggle filmGrainToggle;

    [Header("Player Scripts")]
    public MonoBehaviour[] scriptsToDisable;

    [Header("Mouse Logic")]
    public CustomCursor customCursor;

    bool isPaused = false;
    InputAction pauseAction;
    InputAction backAction;

    void OnEnable()
    {
        // start and escape for pausing
        pauseAction = new InputAction(type: InputActionType.Button);
        pauseAction.AddBinding("<Keyboard>/escape");
        pauseAction.AddBinding("<Gamepad>/start");
        pauseAction.performed += ctx => { if (isPaused) Resume(); else Pause(); };

        backAction = new InputAction(type: InputActionType.Button);
        backAction.AddBinding("<Gamepad>/buttonEast");
        backAction.performed += ctx => { if (settingsMenuUI.activeSelf) CloseSettings(); };

        pauseAction.Enable();
        backAction.Enable();
    }

    void OnDisable() { pauseAction.Disable(); backAction.Disable(); }

    void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        // make sure settings match the main menu
        LoadSettings();
        ApplySettings();

        // Listeners make sure the visuals change instantly when you click them
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(v => { SaveSettings(); ApplySettings(); });
        if (bloomToggle != null) bloomToggle.onValueChanged.AddListener(v => { SaveSettings(); ApplySettings(); });
        if (chromaticToggle != null) chromaticToggle.onValueChanged.AddListener(v => { SaveSettings(); ApplySettings(); });
        if (lensToggle != null) lensToggle.onValueChanged.AddListener(v => { SaveSettings(); ApplySettings(); });
        if (vignetteToggle != null) vignetteToggle.onValueChanged.AddListener(v => { SaveSettings(); ApplySettings(); });
        if (filmGrainToggle != null) filmGrainToggle.onValueChanged.AddListener(v => { SaveSettings(); ApplySettings(); });
    }

    void SaveSettings()
    {
        PlayerPrefs.SetFloat("Volume", musicVolumeSlider.value);
        PlayerPrefs.SetInt("Bloom", bloomToggle.isOn ? 1 : 0);
        PlayerPrefs.SetInt("Chromatic", chromaticToggle.isOn ? 1 : 0);
        PlayerPrefs.SetInt("Lens", lensToggle.isOn ? 1 : 0);
        PlayerPrefs.SetInt("Vignette", vignetteToggle.isOn ? 1 : 0);
        PlayerPrefs.SetInt("FilmGrain", filmGrainToggle.isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    void LoadSettings()
    {
        musicVolumeSlider.value = PlayerPrefs.GetFloat("Volume", 1f);
        bloomToggle.isOn = PlayerPrefs.GetInt("Bloom", 1) == 1;
        chromaticToggle.isOn = PlayerPrefs.GetInt("Chromatic", 1) == 1;
        lensToggle.isOn = PlayerPrefs.GetInt("Lens", 1) == 1;
        vignetteToggle.isOn = PlayerPrefs.GetInt("Vignette", 1) == 1;
        filmGrainToggle.isOn = PlayerPrefs.GetInt("FilmGrain", 1) == 1;
    }

    void ApplySettings()
    {
        if (gameMusic != null) gameMusic.volume = musicVolumeSlider.value;
        if (bassVisualizer != null)
        {
            bassVisualizer.bloomEnabled = bloomToggle.isOn;
            bassVisualizer.chromaticEnabled = chromaticToggle.isOn;
            bassVisualizer.lensDistortionEnabled = lensToggle.isOn;
            bassVisualizer.vignetteEnabled = vignetteToggle.isOn;
            bassVisualizer.filmGrainEnabled = filmGrainToggle.isOn;
        }
    }

    public void Resume()
    {
        pauseMenuUI.SetActive(false);
        settingsMenuUI.SetActive(false);
        Time.timeScale = 1f; // start time again
        isPaused = false;

        // turn the player controls back on
        foreach (var script in scriptsToDisable) script.enabled = true;

        if (gameMusic != null) gameMusic.UnPause();
        if (pauseMusic != null) pauseMusic.Stop();
        if (bassVisualizer != null) bassVisualizer.freezeEffects = false;

        Cursor.lockState = CursorLockMode.Locked;
        if (customCursor != null) customCursor.HideCursor();
    }

    public void Pause()
    {
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f; // stop time!
        isPaused = true;

        // disable movement scripts
        foreach (var script in scriptsToDisable) script.enabled = false;

        if (gameMusic != null) gameMusic.Pause();
        if (pauseMusic != null) pauseMusic.Play();
        if (bassVisualizer != null) bassVisualizer.freezeEffects = true;

        Cursor.lockState = CursorLockMode.None;
        if (customCursor != null) customCursor.ShowCursor();
        EventSystem.current.SetSelectedGameObject(firstPauseButton.gameObject);
    }

    public void OpenSettings()
    {
        pauseMenuUI.SetActive(false);
        settingsMenuUI.SetActive(true);
        EventSystem.current.SetSelectedGameObject(firstSettingsButton.gameObject);
    }

    public void CloseSettings()
    {
        settingsMenuUI.SetActive(false);
        pauseMenuUI.SetActive(true);
        EventSystem.current.SetSelectedGameObject(firstPauseButton.gameObject);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}