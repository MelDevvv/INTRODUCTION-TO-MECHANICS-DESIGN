using UnityEngine;
using System.Collections;

public class MovingPlatformTrigger : MonoBehaviour
{
    [Header("The Object")]
    public Transform platform;

    [Header("Movement")]
    public Vector3 targetPosition;
    public float moveSpeed = 3f;

    [Header("Show/Hide")]
    public bool appearBeforeMove = true;
    public bool disappearAfterMove = true;

    [Header("Wait Timers")]
    public float appearDelay = 0f;
    public float moveDelay = 0f;
    public float disappearDelay = 0f;

    private bool triggered = false;
    private bool isMoving = false;

    Vector3 startPosition;
    Coroutine platformCoroutine;

    void Start()
    {
        // save the start spot so we can reset it later
        if (platform == null) return;

        startPosition = platform.position;

        // hide the platform if it's supposed to pop in later
        if (appearBeforeMove)
            platform.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        // listen for the player dying so we can reset the trap
        PlayerController.OnPlayerRespawn += ResetPlatform;
    }

    void OnDisable()
    {
        // stop listening if the script is disabled
        PlayerController.OnPlayerRespawn -= ResetPlatform;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // start the whole sequence when the player hits the trigger
        if (!triggered && other.CompareTag("Player"))
        {
            triggered = true;
            platformCoroutine = StartCoroutine(PlatformSequence());
        }
    }

    private IEnumerator PlatformSequence()
    {
        if (platform == null) yield break;

        // 1. Wait then show the platform
        if (appearBeforeMove)
        {
            yield return new WaitForSeconds(appearDelay);
            platform.gameObject.SetActive(true);
        }

        // 2. Short pause before it starts moving
        if (moveDelay > 0f)
            yield return new WaitForSeconds(moveDelay);

        // 3. The actual movement logic
        isMoving = true;

        while (Vector3.Distance(platform.position, targetPosition) > 0.01f)
        {
            platform.position = Vector3.MoveTowards(
                platform.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );

            yield return null; // wait for the next frame
        }

        platform.position = targetPosition;
        isMoving = false;

        // 4. Wait then hide it again if needed
        if (disappearAfterMove)
        {
            yield return new WaitForSeconds(disappearDelay);
            platform.gameObject.SetActive(false);
        }
    }

    void ResetPlatform()
    {
        // kill the current movement so it doesnt keep sliding after respawn
        if (platformCoroutine != null)
            StopCoroutine(platformCoroutine);

        triggered = false;
        isMoving = false;

        if (platform != null)
        {
            platform.position = startPosition;

            // put it back to its default visible state
            if (appearBeforeMove)
                platform.gameObject.SetActive(false);
            else
                platform.gameObject.SetActive(true);
        }
    }
}