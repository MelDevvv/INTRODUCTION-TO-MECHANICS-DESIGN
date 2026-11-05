
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BoxCollider2D))]
public class SignpostMessage : MonoBehaviour
{
    private static SignpostMessage activeSignpost;

    [Header("UI Reference")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Message")]
    [TextArea(3, 6)]
    [SerializeField] private string message = "Add your signpost message here.";

    [Header("Interaction")]
    [SerializeField] private Key interactionKey = Key.E;
    [SerializeField] private string playerTag = "Player";

    [Header("Animation")]
    [Min(1f)]
    [SerializeField] private float charactersPerSecond = 35f;

    [Min(0.05f)]
    [SerializeField] private float fadeDuration = 0.2f;

    [Header("Prompt Pulse")]
    [SerializeField] private bool pulsePrompt = true;

    [Min(0.2f)]
    [SerializeField] private float pulseInterval = 0.75f;

    private VisualElement signpostPrompt;
    private VisualElement signpostPanel;
    private Label messageLabel;

    private Coroutine animationCoroutine;
    private Coroutine promptPulseCoroutine;

    private readonly HashSet<Collider2D> playerCollidersInside =
        new HashSet<Collider2D>();

    private int visibleCharacters;
    private bool playerInside;
    private bool messageOpen;

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;

        if (uiDocument == null)
        {
            Debug.LogError(
                "SignpostMessage: Assign the shared RuntimeUI UIDocument.",
                this
            );

            enabled = false;
            return;
        }

        VisualElement root = uiDocument.rootVisualElement;

        signpostPrompt = root.Q<VisualElement>("signpost-prompt");
        signpostPanel = root.Q<VisualElement>("signpost-panel");
        messageLabel = root.Q<Label>("signpost-message");

        if (signpostPrompt == null ||
            signpostPanel == null ||
            messageLabel == null)
        {
            Debug.LogError(
                "SignpostMessage: Missing signpost-prompt, signpost-panel or signpost-message in PlatformerUI.uxml.",
                this
            );

            enabled = false;
            return;
        }

        HideAllUIImmediately();
    }

    private void Update()
    {
        if (!playerInside ||
            activeSignpost != this ||
            Keyboard.current == null)
            return;

        if (Keyboard.current[interactionKey].wasPressedThisFrame)
        {
            if (messageOpen)
                CloseMessage();
            else
                OpenMessage();
        }
    }

    // PLAYER ENTERS SIGNPOST

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other) || !playerCollidersInside.Add(other))
            return;

        if (playerCollidersInside.Count > 1)
            return;

        playerInside = true;

        if (activeSignpost != null && activeSignpost != this)
            activeSignpost.HideAllUIImmediately();

        activeSignpost = this;

        messageOpen = false;
        visibleCharacters = 0;
        messageLabel.text = string.Empty;

        signpostPanel.RemoveFromClassList("is-visible");
        signpostPanel.style.display = DisplayStyle.None;

        ShowPrompt();
    }

    // PLAYER EXITS SIGNPOST

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other) || !playerCollidersInside.Remove(other))
            return;

        if (playerCollidersInside.Count > 0)
            return;

        playerInside = false;
        StopPromptPulse();

        if (activeSignpost != this)
            return;

        signpostPrompt.RemoveFromClassList("is-visible");
        signpostPrompt.style.display = DisplayStyle.None;

        StopAnimation();

        if (messageOpen)
        {
            messageOpen = false;

            signpostPanel.RemoveFromClassList("is-visible");

            animationCoroutine = StartCoroutine(
                FadeOutMessage(false)
            );
        }
        else
        {
            signpostPanel.RemoveFromClassList("is-visible");
            signpostPanel.style.display = DisplayStyle.None;

            messageLabel.text = string.Empty;

            activeSignpost = null;
        }
    }

    // SHOW INTERACTION PROMPT

    private void ShowPrompt()
    {
        signpostPrompt.style.display = DisplayStyle.Flex;

        signpostPrompt.RemoveFromClassList("is-visible");
        signpostPrompt.RemoveFromClassList("pulse");

        signpostPrompt.schedule.Execute(() =>
        {
            if (playerInside && activeSignpost == this)
            {
                signpostPrompt.AddToClassList("is-visible");
                StartPromptPulse();
            }
        }).StartingIn(20);
    }

    // OPEN MESSAGE

    private void OpenMessage()
    {
        if (!playerInside || activeSignpost != this)
            return;

        StopPromptPulse();
        StopAnimation();

        messageOpen = true;
        visibleCharacters = 0;
        messageLabel.text = string.Empty;

        signpostPrompt.RemoveFromClassList("is-visible");
        signpostPrompt.style.display = DisplayStyle.None;

        signpostPanel.style.display = DisplayStyle.Flex;
        signpostPanel.RemoveFromClassList("is-visible");

        signpostPanel.schedule.Execute(() =>
        {
            if (playerInside &&
                messageOpen &&
                activeSignpost == this)
            {
                signpostPanel.AddToClassList("is-visible");
            }
        }).StartingIn(20);

        animationCoroutine = StartCoroutine(TypeMessage());
    }

    // CLOSE MESSAGE

    private void CloseMessage()
    {
        if (!messageOpen)
            return;

        messageOpen = false;

        StopAnimation();

        signpostPanel.RemoveFromClassList("is-visible");

        animationCoroutine = StartCoroutine(
            FadeOutMessage(true)
        );
    }

    // TYPEWRITER EFFECT

    private IEnumerator TypeMessage()
    {
        float delay = 1f / Mathf.Max(1f, charactersPerSecond);

        while (
            playerInside &&
            messageOpen &&
            activeSignpost == this &&
            visibleCharacters < message.Length
        )
        {
            visibleCharacters++;

            messageLabel.text = message.Substring(
                0,
                visibleCharacters
            );

            yield return new WaitForSecondsRealtime(delay);
        }

        animationCoroutine = null;
    }

    // FADE OUT MESSAGE

    private IEnumerator FadeOutMessage(bool returnToPrompt)
    {
        yield return new WaitForSecondsRealtime(fadeDuration);

        if (activeSignpost == this && !messageOpen)
        {
            signpostPanel.style.display = DisplayStyle.None;

            messageLabel.text = string.Empty;
            visibleCharacters = 0;

            if (returnToPrompt && playerInside)
            {
                ShowPrompt();
            }
            else if (!playerInside)
            {
                activeSignpost = null;
            }
        }

        animationCoroutine = null;
    }

    // PROMPT PULSE

    private void StartPromptPulse()
    {
        StopPromptPulse();

        if (pulsePrompt)
            promptPulseCoroutine = StartCoroutine(PromptPulse());
    }

    private IEnumerator PromptPulse()
    {
        while (
            playerInside &&
            !messageOpen &&
            activeSignpost == this
        )
        {
            signpostPrompt.AddToClassList("pulse");

            yield return new WaitForSecondsRealtime(pulseInterval);

            signpostPrompt.RemoveFromClassList("pulse");

            yield return new WaitForSecondsRealtime(pulseInterval);
        }

        signpostPrompt.RemoveFromClassList("pulse");

        promptPulseCoroutine = null;
    }

    private void StopPromptPulse()
    {
        if (promptPulseCoroutine != null)
        {
            StopCoroutine(promptPulseCoroutine);
            promptPulseCoroutine = null;
        }

        if (signpostPrompt != null)
            signpostPrompt.RemoveFromClassList("pulse");
    }

    // STOP ANIMATIONS

    private void StopAnimation()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }
    }

    // HIDE ALL UI

    private void HideAllUIImmediately()
    {
        StopAnimation();
        StopPromptPulse();

        playerInside = false;
        messageOpen = false;
        visibleCharacters = 0;

        playerCollidersInside.Clear();

        if (signpostPrompt != null)
        {
            signpostPrompt.RemoveFromClassList("is-visible");
            signpostPrompt.style.display = DisplayStyle.None;
        }

        if (signpostPanel != null)
        {
            signpostPanel.RemoveFromClassList("is-visible");
            signpostPanel.style.display = DisplayStyle.None;
        }

        if (messageLabel != null)
            messageLabel.text = string.Empty;

        if (activeSignpost == this)
            activeSignpost = null;
    }

    // PLAYER DETECTION

    private bool IsPlayer(Collider2D other)
    {
        if (other == null)
            return false;

        Rigidbody2D body = other.attachedRigidbody;

        GameObject playerObject = body != null
            ? body.gameObject
            : other.gameObject;

        return playerObject.CompareTag(playerTag);
    }

    private void OnDisable()
    {
        HideAllUIImmediately();
    }
}
