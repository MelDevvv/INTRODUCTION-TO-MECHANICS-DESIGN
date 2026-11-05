
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Adds entrance animations and subtle pulsing effects
/// to the existing Platformer UI Toolkit interface.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class PlatformerUIEffects : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Menu Animation")]
    [Min(0f)]
    [SerializeField] private float menuEntryDelay = 0.02f;

    [Header("HUD Entrance")]
    [Min(0f)]
    [SerializeField] private float hudStaggerDelay = 0.07f;

    [Header("Status Pixel")]
    [SerializeField] private bool pulseMenuStatusPixel = true;

    [Min(0.1f)]
    [SerializeField] private float statusPulseInterval = 0.8f;

    private VisualElement screenOverlay;
    private VisualElement menuFrame;
    private VisualElement brandBlock;
    private VisualElement hudRight;
    private VisualElement controlsHint;
    private VisualElement statusPixel;

    private bool previousMenuVisible;
    private bool menuStateInitialised;

    private Coroutine hudIntroCoroutine;
    private Coroutine statusPulseCoroutine;

    private void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError(
                "PlatformerUIEffects needs a UIDocument reference.",
                this
            );

            enabled = false;
            return;
        }

        VisualElement root = uiDocument.rootVisualElement;

        screenOverlay = root.Q<VisualElement>("screen-overlay");
        menuFrame = root.Q<VisualElement>("menu-frame");

        brandBlock = root.Q<VisualElement>(
            className: "brand-block"
        );

        hudRight = root.Q<VisualElement>(
            className: "hud-right"
        );

        controlsHint = root.Q<VisualElement>(
            "controls-hint"
        );

        statusPixel = root.Q<VisualElement>(
            className: "status-pixel"
        );

        if (screenOverlay == null || menuFrame == null)
        {
            Debug.LogError(
                "PlatformerUIEffects could not find screen-overlay or menu-frame in the UXML.",
                this
            );

            enabled = false;
            return;
        }
    }

    private void Start()
    {
        hudIntroCoroutine = StartCoroutine(PlayHudIntro());

        if (pulseMenuStatusPixel && statusPixel != null)
        {
            statusPulseCoroutine = StartCoroutine(
                PulseStatusPixel()
            );
        }
    }

    private void Update()
    {
        if (screenOverlay == null || menuFrame == null)
            return;

        bool menuVisible =
            screenOverlay.resolvedStyle.display != DisplayStyle.None;

        if (
            !menuStateInitialised ||
            menuVisible != previousMenuVisible
        )
        {
            menuStateInitialised = true;
            previousMenuVisible = menuVisible;

            if (menuVisible)
                RevealMenuFrame();
            else
                menuFrame.RemoveFromClassList("menu-entered");
        }
    }

    // MENU ENTRANCE

    private void RevealMenuFrame()
    {
        menuFrame.RemoveFromClassList("menu-entered");

        menuFrame.schedule.Execute(() =>
        {
            if (
                screenOverlay != null &&
                screenOverlay.resolvedStyle.display != DisplayStyle.None
            )
            {
                menuFrame.AddToClassList("menu-entered");
            }
        }).StartingIn(
            Mathf.RoundToInt(menuEntryDelay * 1000f)
        );
    }

    // HUD ENTRANCE

    private IEnumerator PlayHudIntro()
    {
        yield return null;

        if (brandBlock != null)
            brandBlock.AddToClassList("ui-entered");

        yield return new WaitForSecondsRealtime(
            hudStaggerDelay
        );

        if (hudRight != null)
            hudRight.AddToClassList("ui-entered");

        yield return new WaitForSecondsRealtime(
            hudStaggerDelay
        );

        if (controlsHint != null)
            controlsHint.AddToClassList("ui-entered");

        hudIntroCoroutine = null;
    }

    // STATUS PIXEL PULSE

    private IEnumerator PulseStatusPixel()
    {
        while (statusPixel != null)
        {
            statusPixel.AddToClassList("blink");

            yield return new WaitForSecondsRealtime(
                statusPulseInterval * 0.5f
            );

            statusPixel.RemoveFromClassList("blink");

            yield return new WaitForSecondsRealtime(
                statusPulseInterval * 0.5f
            );
        }
    }

    private void OnDisable()
    {
        if (hudIntroCoroutine != null)
            StopCoroutine(hudIntroCoroutine);

        if (statusPulseCoroutine != null)
            StopCoroutine(statusPulseCoroutine);

        if (menuFrame != null)
            menuFrame.RemoveFromClassList("menu-entered");
    }
}
