using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelDoor : MonoBehaviour
{
    [Header("Level Exit")]
    public bool loadNextScene = true;
    public string nextSceneName;

    [Header("Door Teleport Trick")]
    public bool doorMoves = false;
    public Transform[] teleportLocations;
    public float detectionRadius = 2f;
    public int maxTeleports = 1;

    private int teleportCount = 0;
    private Transform player;

    Vector3 startPosition;

    void Start()
    {
        // find the player so we know how far away they are
        player = GameObject.FindGameObjectWithTag("Player").transform;
        startPosition = transform.position;
    }

    void OnEnable()
    {
        // if the player dies and resets, put the door back where it started
        PlayerController.OnPlayerRespawn += ResetDoor;
    }

    void OnDisable()
    {
        // clean up the event listener
        PlayerController.OnPlayerRespawn -= ResetDoor;
    }

    void Update()
    {
        // if this isnt a "trick" door or we already moved it, dont do anything
        if (!doorMoves || player == null) return;
        if (teleportCount >= maxTeleports) return;

        // check how close the player is
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= detectionRadius)
        {
            TeleportDoor();
        }
    }

    void TeleportDoor()
    {
        if (teleportLocations.Length == 0) return;

        // move the door to one of the empty markers we set in the inspector
        int randomIndex = Random.Range(0, teleportLocations.Length);
        transform.position = teleportLocations[randomIndex].position;

        teleportCount++;
    }

    // this is the normal door logic to finish the level
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!loadNextScene) return;

        if (other.CompareTag("Player"))
        {
            // load the next level string from the inspector
            SceneManager.LoadScene(nextSceneName);
        }
    }

    void ResetDoor()
    {
        // put everything back to normal
        teleportCount = 0;
        transform.position = startPosition;
    }

    // draw a circle in the editor so I can see how big the trigger is
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}