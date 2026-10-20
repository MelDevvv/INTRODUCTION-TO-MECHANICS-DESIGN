using UnityEngine;
using System.Collections;

public class PopUpSpike : MonoBehaviour
{
    [Header("The Hazard")]
    public GameObject spikeObject;

    [Header("Logic Puzzle")]
    public bool requirePreviousTrigger = false;
    public TriggerZone requiredTrigger;

    [Header("Timing")]
    [Tooltip("How long to wait before the spike pops up.")]
    public float spawnDelay = 0f;

    private bool hasTriggered = false;
    Coroutine spawnCoroutine;

    void OnEnable()
    {
        // make sure the trap resets when the player respawns
        PlayerController.OnPlayerRespawn += ResetTrap;
    }

    void OnDisable()
    {
        // clean up the event
        PlayerController.OnPlayerRespawn -= ResetTrap;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // if it already popped up or it's not the player, ignore it
        if (hasTriggered) return;
        if (!other.CompareTag("Player")) return;

        // this part is cool: the trap only works if another button was pressed first
        if (requirePreviousTrigger)
        {
            if (requiredTrigger == null) return;
            if (!requiredTrigger.activated) return;
        }

        // start the pop-up timer
        spawnCoroutine = StartCoroutine(ActivateSpikeWithDelay());
    }

    IEnumerator ActivateSpikeWithDelay()
    {
        hasTriggered = true;

        if (spawnDelay > 0f)
            yield return new WaitForSeconds(spawnDelay);

        if (spikeObject != null)
            spikeObject.SetActive(true);
    }

    void ResetTrap()
    {
        // kill the timer if it's still running so spikes don't pop up after death
        if (spawnCoroutine != null)
            StopCoroutine(spawnCoroutine);

        hasTriggered = false;

        // hide the spike again
        if (spikeObject != null)
            spikeObject.SetActive(false);
    }
}