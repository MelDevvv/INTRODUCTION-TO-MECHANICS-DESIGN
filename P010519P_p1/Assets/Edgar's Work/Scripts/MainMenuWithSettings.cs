using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class MainMenuWithSettings : MonoBehaviour
{
    [Header("UI Stuff")]
    public GameObject mainMenuUI;
    public GameObject settingsMenuUI;

    [Header("Select these first")]
    public Button firstMainButton;
    public Button firstSettingsButton;

    [Header("Audio")]
    public AudioSource menuMusic;
    public Slider musicVolumeSlider;

    [Header("Visual Effects")]
    public BassPostProcessPulse bassVisualizer;

    [Header("Checkboxes")]
    public Toggle bloomToggle;
    public Toggle chromaticToggle;
    public Toggle lensToggle;
    public Toggle vignetteToggle;
    public Toggle filmGrainToggle;

    [Header("Mouse")]
    public CustomCursor customCursor;

    InputAction backAction;

    void OnEnable()
    {
        // go back to main menu when pressing B on controller
        backAction = new InputAction(type: InputActionType.Button);
        backAction.AddBinding("<Gamepad>/buttonEast");
        backAction.performed += ctx => { if (settingsMenuUI != null && settingsMenuUI.activeSelf) CloseSettings(); };
        backAction.Enable();
    }

    void OnDisable() { backAction.Disable(); }

    void Start()
    {
        Time.timeScale = 1f; // make sure time is running
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;

        if (customCursor != null)
        {
            customCursor.gameObject.SetActive(true);
            customCursor.ShowCursor();
        }

        if (mainMenuUI != null) mainMenuUI.SetActive(true);
        if (settingsMenuUI != null) settingsMenuUI.SetActive(false);

        // load previous settings so they dont reset every time
        LoadSettings();
        ApplySettings();

        // setup the listeners so the game updates as soon as you click stuff
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.AddListener(v => { SaveSettings(); ApplySettings(); });

        if (bloomToggle != null)
            bloomToggle.onValueChanged.AddListener(v => { SaveSettings(); ApplySettings(); });

        if (chromaticToggle != null)
            chromaticToggle.onValueChanged.AddListener(v => { SaveSettings(); ApplySettings(); });

        if (lensToggle != null)
            lensToggle.onValueChanged.AddListener(v => { SaveSettings(); ApplySettings(); });

        if (vignetteToggle != null)
            vignetteToggle.onValueChanged.AddListener(v => { SaveSettings(); ApplySettings(); });

        if (filmGrainToggle != null)
            filmGrainToggle.onValueChanged.AddListener(v => { SaveSettings(); ApplySettings(); });

        // highlight the first button for the controller
        if (EventSystem.current != null && firstMainButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstMainButton.gameObject);
        }
    }

    public void SaveSettings()
    {
        // write everything to playerprefs
        if (musicVolumeSlider != null) PlayerPrefs.SetFloat("Volume", musicVolumeSlider.value);
        if (bloomToggle != null) PlayerPrefs.SetInt("Bloom", bloomToggle.isOn ? 1 : 0);
        if (chromaticToggle != null) PlayerPrefs.SetInt("Chromatic", chromaticToggle.isOn ? 1 : 0);
        if (lensToggle != null) PlayerPrefs.SetInt("Lens", lensToggle.isOn ? 1 : 0);
        if (vignetteToggle != null) PlayerPrefs.SetInt("Vignette", vignetteToggle.isOn ? 1 : 0);
        if (filmGrainToggle != null) PlayerPrefs.SetInt("FilmGrain", filmGrainToggle.isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    void LoadSettings()
    {
        // get the saved values or use 1 as default
        if (musicVolumeSlider != null) musicVolumeSlider.value = PlayerPrefs.GetFloat("Volume", 1f);
        if (bloomToggle != null) bloomToggle.isOn = PlayerPrefs.GetInt("Bloom", 1) == 1;
        if (chromaticToggle != null) chromaticToggle.isOn = PlayerPrefs.GetInt("Chromatic", 1) == 1;
        if (lensToggle != null) lensToggle.isOn = PlayerPrefs.GetInt("Lens", 1) == 1;
        if (vignetteToggle != null) vignetteToggle.isOn = PlayerPrefs.GetInt("Vignette", 1) == 1;
        if (filmGrainToggle != null) filmGrainToggle.isOn = PlayerPrefs.GetInt("FilmGrain", 1) == 1;
    }

    void ApplySettings()
    {
        // actually push the values to the music and visualizer
        if (menuMusic != null && musicVolumeSlider != null) menuMusic.volume = musicVolumeSlider.value;
        if (bassVisualizer != null)
        {
            if (bloomToggle != null) bassVisualizer.bloomEnabled = bloomToggle.isOn;
            if (chromaticToggle != null) bassVisualizer.chromaticEnabled = chromaticToggle.isOn;
            if (lensToggle != null) bassVisualizer.lensDistortionEnabled = lensToggle.isOn;
            if (vignetteToggle != null) bassVisualizer.vignetteEnabled = vignetteToggle.isOn;
            if (filmGrainToggle != null) bassVisualizer.filmGrainEnabled = filmGrainToggle.isOn;
        }
    }

    public void PlayGame() { SceneManager.LoadScene("Level1"); }

    public void OpenSettings()
    {
        mainMenuUI.SetActive(false);
        settingsMenuUI.SetActive(true);
        EventSystem.current.SetSelectedGameObject(firstSettingsButton.gameObject);
    }

    public void CloseSettings()
    {
        settingsMenuUI.SetActive(false);
        mainMenuUI.SetActive(true);
        EventSystem.current.SetSelectedGameObject(firstMainButton.gameObject);
    }

    public void QuitGame() { Application.Quit(); }
}