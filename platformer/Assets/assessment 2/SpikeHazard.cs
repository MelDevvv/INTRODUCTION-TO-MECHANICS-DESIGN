
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SpikeHazard : MonoBehaviour
{
    [Header("Player Detection")]
    [Tooltip("Only this specific player collider will trigger death.")]
    [SerializeField] private Collider2D playerDeathCollider;

    private void Awake()
    {
        Collider2D spikeCollider = GetComponent<Collider2D>();
        spikeCollider.isTrigger = true;

        if (playerDeathCollider == null)
        {
            Debug.LogError(
                "SpikeHazard: Assign the player's main collider in the Inspector.",
                this
            );
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Ignore every collider except the one assigned in the Inspector.
        if (playerDeathCollider == null || other != playerDeathCollider)
            return;

        PlayerDeathController deathController =
            playerDeathCollider.GetComponentInParent<PlayerDeathController>();

        if (deathController != null)
        {
            deathController.Die();
        }
    }
}
