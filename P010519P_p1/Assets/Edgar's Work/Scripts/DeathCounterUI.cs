using UnityEngine;
using TMPro;

public class DeathCounterUI : MonoBehaviour
{
    // link the text from the canvas here
    public TextMeshProUGUI deathText;
    private int deathCount;

    void Start()
    {
        // try to load the score so it doesnt reset to 0 every time we play
        deathCount = PlayerPrefs.GetInt("DeathCount", 0);
        UpdateDeathText();
    }

    void Update()
    {
        // look for the player script to see if he died
        PlayerController player = FindObjectOfType<PlayerController>();

        if (player != null && player.deathCount != deathCount)
        {
            // update our local number to match the players real death count
            deathCount = player.deathCount;
            UpdateDeathText();

            // save it to the registry so we dont lose progress
            PlayerPrefs.SetInt("DeathCount", deathCount);
        }
    }

    void UpdateDeathText()
    {
        // just update the string on screen
        if (deathText != null)
        {
            deathText.text = $"Deaths: {deathCount}";
        }
    }

    // useful for a "new game" button or something
    public void ResetDeathCount()
    {
        deathCount = 0;
        PlayerPrefs.SetInt("DeathCount", deathCount);
        UpdateDeathText();
    }
}