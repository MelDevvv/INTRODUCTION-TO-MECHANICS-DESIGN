using UnityEngine;
using TMPro;
using System.Collections;

[System.Serializable]
public class DeathTaunt
{
    // the audio clip of someone making fun of you
    public AudioClip voiceLine;
    [TextArea(2, 4)]
    public string screenText;
}

public class DeathTauntSystem : MonoBehaviour
{
    [Header("All the taunts")]
    public DeathTaunt[] taunts;

    [Header("Sounds")]
    public AudioSource tauntAudioSource;
    public AudioSource explosionAudioSource;
    public AudioClip deathExplosionSound;

    [Header("UI Stuff")]
    public TextMeshProUGUI tauntText;

    [Header("Timing")]
    [Range(0.01f, 0.2f)]
    public float typingSpeed = 0.05f;
    [Range(1f, 10f)]
    public float displayDuration = 3f;

    [Header("The Player")]
    public PlayerController player;

    void Start()
    {
        ClearText();
        // listen for when the player dies
        if (player != null)
            player.OnPlayerDeath += HandleDeath;
        else
            Debug.LogError("forgot to drag the player into the taunt system!");

        // listen for when they respawn to play the mean message
        PlayerController.OnPlayerRespawn += PlayRandomTaunt;
    }

    void OnDestroy()
    {
        // gotta clean up the events or unity gets mad
        if (player != null)
            player.OnPlayerDeath -= HandleDeath;

        PlayerController.OnPlayerRespawn -= PlayRandomTaunt;
    }

    void HandleDeath()
    {
        // play the explosion sound when we die
        if (explosionAudioSource != null && deathExplosionSound != null)
            explosionAudioSource.PlayOneShot(deathExplosionSound, 1f);
    }

    public void PlayRandomTaunt()
    {
        if (taunts.Length == 0) return;

        // pick a random roast from the list
        int index = Random.Range(0, taunts.Length);
        DeathTaunt chosen = taunts[index];

        if (tauntAudioSource != null && chosen.voiceLine != null)
        {
            // change the pitch slightly so it doesnt sound exactly the same every time
            tauntAudioSource.pitch = Random.Range(0.95f, 1.05f);
            tauntAudioSource.PlayOneShot(chosen.voiceLine, 3f);
        }

        // stop any old typing and start the new one
        StopAllCoroutines();
        StartCoroutine(TypeText(chosen.screenText));
    }

    IEnumerator TypeText(string message)
    {
        if (tauntText == null) yield break;

        tauntText.gameObject.SetActive(true);
        tauntText.text = "";

        // type the message out letter by letter for effect
        foreach (char letter in message)
        {
            tauntText.text += letter;
            yield return new WaitForSeconds(typingSpeed);
        }

        yield return new WaitForSeconds(displayDuration);
        ClearText();
    }

    void ClearText()
    {
        if (tauntText != null)
        {
            tauntText.text = "";
            tauntText.gameObject.SetActive(false);
        }
    }
}