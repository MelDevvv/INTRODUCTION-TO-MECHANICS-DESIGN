using UnityEngine;
using System.Collections;

public class SpikeTrap : MonoBehaviour
{
    [Header("Detection")]
    public Transform player;
    public float detectionRadius = 3f;

    [Header("Movement 1 (Attack)")]
    public Vector3 targetPosition;
    public float moveSpeed = 5f;

    [Header("Combo Trigger")]
    public bool requireTriggerZone = false;
    public TriggerZone triggerZone;

    [Header("Movement 2 (Retract/Follow up)")]
    public bool enableSecondMovement = false;
    public Vector3 secondTargetPosition;
    public float secondMoveDelay = 1f;
    public float secondMoveSpeed = 5f;

    private bool hasTriggered = false;
    private int movementStage = 0; // 0 = idle, 1 = waiting, 2 = moving again

    Vector3 startPosition;
    Coroutine secondMoveCoroutine;

    void Start()
    {
        startPosition = transform.position;
    }

    void OnEnable()
    {
        // keep everything in sync with the player's life
        PlayerController.OnPlayerRespawn += ResetTrap;
    }

    void OnDisable()
    {
        PlayerController.OnPlayerRespawn -= ResetTrap;
    }

    void Update()
    {
        if (player == null) return;

        // check if a button needs to be pressed before this trap even turns on
        if (requireTriggerZone)
        {
            if (triggerZone == null || !triggerZone.activated) return;
        }

        // if the player gets too close, fire the trap
        if (!hasTriggered)
        {
            float distance = Vector3.Distance(transform.position, player.position);
            if (distance <= detectionRadius)
            {
                hasTriggered = true;
            }
            return;
        }

        // STAGE 0: The initial attack
        if (movementStage == 0)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );

            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                transform.position = targetPosition;

                if (enableSecondMovement)
                {
                    movementStage = 1; // move to the waiting phase
                    secondMoveCoroutine = StartCoroutine(StartSecondMove());
                }
            }
        }

        // STAGE 2: The follow-up move (like retracting spikes)
        else if (movementStage == 2)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                secondTargetPosition,
                secondMoveSpeed * Time.deltaTime
            );

            if (Vector3.Distance(transform.position, secondTargetPosition) < 0.01f)
            {
                transform.position = secondTargetPosition;
                movementStage = 3; // Finished!
            }
        }
    }

    IEnumerator StartSecondMove()
    {
        yield return new WaitForSeconds(secondMoveDelay);
        movementStage = 2; // kick off the second movement in Update
    }

    void ResetTrap()
    {
        // stop any waiting timers so it doesn't move after you've already died
        if (secondMoveCoroutine != null) StopCoroutine(secondMoveCoroutine);

        transform.position = startPosition;
        hasTriggered = false;
        movementStage = 0;
    }

    // This makes it SO much easier to set up levels
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(targetPosition, 0.15f);

        if (enableSecondMovement)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(secondTargetPosition, 0.15f);
        }
    }
}