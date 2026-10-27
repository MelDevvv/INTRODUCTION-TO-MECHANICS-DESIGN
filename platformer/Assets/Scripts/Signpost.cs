using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Shows a signpost message with a typewriter effect when the player enters
/// its trigger, then deletes the text when the player leaves.
/// If the player re-enters while the text is deleting, typing resumes from
/// the current visible character instead of restarting from the beginning.
/// Put the actual message in the assigned TextMeshPro component in the Inspector.
/// </summary>
public class SignpostMessage : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject popup;
    [SerializeField] private TMP_Text messageText;

    [Header("Typing Animation")]
    [Min(1f)]
    [SerializeField] private float charactersPerSecond = 35f;

    private Coroutine animationCoroutine;
    private int visibleCharacters;
    private int totalCharacters;
    private bool playerInside;
    private bool initialized;

    private void Awake()
    {
        if (messageText == null)
        {
            Debug.LogError($"{nameof(SignpostMessage)} on '{name}' needs a Message Text TMP component assigned.", this);
            enabled = false;
            return;
        }

        // Read the message from the TMP component, not from a hardcoded string.
        messageText.ForceMeshUpdate();
        totalCharacters = messageText.textInfo.characterCount;
        visibleCharacters = 0;
        messageText.maxVisibleCharacters = 0;
        initialized = true;

        if (popup != null)
            popup.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || !initialized)
            return;

        playerInside = true;

        if (popup != null && !popup.activeSelf)
            popup.SetActive(true);

        // Stop deletion/typing without resetting the current character count.
        StopCurrentAnimation();
        animationCoroutine = StartCoroutine(TypeMessage());
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || !initialized)
            return;

        playerInside = false;
        StopCurrentAnimation();
        animationCoroutine = StartCoroutine(DeleteMessage());
    }

    private IEnumerator TypeMessage()
    {
        messageText.ForceMeshUpdate();
        totalCharacters = messageText.textInfo.characterCount;
        visibleCharacters = Mathf.Clamp(visibleCharacters, 0, totalCharacters);
        messageText.maxVisibleCharacters = visibleCharacters;

        float delay = 1f / Mathf.Max(1f, charactersPerSecond);
        while (playerInside && visibleCharacters < totalCharacters)
        {
            visibleCharacters++;
            messageText.maxVisibleCharacters = visibleCharacters;
            yield return new WaitForSecondsRealtime(delay);
        }

        animationCoroutine = null;
    }

    private IEnumerator DeleteMessage()
    {
        float delay = 1f / Mathf.Max(1f, charactersPerSecond);
        while (!playerInside && visibleCharacters > 0)
        {
            visibleCharacters--;
            messageText.maxVisibleCharacters = visibleCharacters;
            yield return new WaitForSecondsRealtime(delay);
        }

        if (!playerInside)
        {
            messageText.maxVisibleCharacters = 0;
            visibleCharacters = 0;
            if (popup != null)
                popup.SetActive(false);
        }

        animationCoroutine = null;
    }

    private void StopCurrentAnimation()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }
    }
}