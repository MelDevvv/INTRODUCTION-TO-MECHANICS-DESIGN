using UnityEngine;

public class DestroyAfterTime : MonoBehaviour
{
    [Header("Timer")]
    public float destroyTime = 0.6f;

    void Start()
    {
        // kill this object after the timer runs out so we dont lag the game
        // good for things like explosion effects or temporary text
        Destroy(gameObject, destroyTime);
    }
}