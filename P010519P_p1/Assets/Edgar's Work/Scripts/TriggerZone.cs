using UnityEngine;

public class TriggerZone : MonoBehaviour
{
    // This is the variable that other scripts (like SpikeTrap) check
    [Header("Status")]
    public bool activated = false;

    void OnEnable()
    {
        // If the player dies, we usually want the puzzle to reset
        PlayerController.OnPlayerRespawn += ResetTrigger;
    }

    void OnDisable()
    {
        // Unsubscribe to avoid memory leaks or errors
        PlayerController.OnPlayerRespawn -= ResetTrigger;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // If the player touches this invisible box, turn the switch 'on'
        if (other.CompareTag("Player"))
        {
            activated = true;
        }
    }

    void ResetTrigger()
    {
        // Flip the switch back to 'off'
        activated = false;
    }
}