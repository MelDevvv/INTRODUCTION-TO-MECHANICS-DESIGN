
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class LevelFinishTrigger : MonoBehaviour
{
    [Header("Player Detection")]
    [SerializeField] private Collider2D playerFinishCollider;

    private LevelCompletionController completionController;

    private void Awake()
    {
        Collider2D finishCollider = GetComponent<Collider2D>();
        finishCollider.isTrigger = true;

        completionController =
            FindFirstObjectByType<LevelCompletionController>();

        if (playerFinishCollider == null)
        {
            Debug.LogError(
                "LevelFinishTrigger: Assign the player's main collider.",
                this
            );
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (playerFinishCollider == null)
            return;

        if (other != playerFinishCollider)
            return;

        if (completionController == null)
        {
            completionController =
                FindFirstObjectByType<LevelCompletionController>();
        }

        if (completionController != null)
        {
            completionController.CompleteLevel();
        }
        else
        {
            Debug.LogError(
                "LevelFinishTrigger cannot find LevelCompletionController.",
                this
            );
        }
    }
}
