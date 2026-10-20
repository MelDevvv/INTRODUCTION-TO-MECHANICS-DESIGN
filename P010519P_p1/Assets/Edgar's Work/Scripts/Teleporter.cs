using UnityEngine;

public class Teleporter : MonoBehaviour
{
    // link the other door/portal here
    public Teleporter exitTeleporter;

    // the empty object where the player actually spawns
    public Transform exitPoint;

    private bool playerInside = false;
    private bool canTeleport = true;

    void Update()
    {
        // the player has to be standing in the portal AND press jump to use it
        if (playerInside && canTeleport && Input.GetKeyDown(KeyCode.Space))
        {
            Teleport();
        }
    }

    void Teleport()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null) return;

        // stop both portals from working for a split second 
        // so you don't instantly teleport back to where you started
        canTeleport = false;
        exitTeleporter.canTeleport = false;

        // move the player to the destination
        player.transform.position = exitPoint.position;

        // wait a tiny bit then turn the portals back on
        Invoke(nameof(ResetTeleport), 0.3f);
        exitTeleporter.Invoke(nameof(ResetTeleport), 0.3f);
    }

    void ResetTeleport()
    {
        canTeleport = true;
    }

    // check if the player is standing in the trigger zone
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = true;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = false;
        }
    }
}