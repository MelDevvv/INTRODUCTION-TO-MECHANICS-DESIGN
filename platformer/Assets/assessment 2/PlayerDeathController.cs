
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerDeathController : MonoBehaviour
{
    [Header("Death Settings")]
    [SerializeField] private float deathRestartDelay = 1f;

    [Header("Death Particles")]
    [SerializeField] private ParticleSystem deathParticles;

    [Header("Death Sound")]
    [SerializeField] private AudioSource deathAudioSource;
    [SerializeField] private AudioClip deathSound;

    [Range(0f, 1f)]
    [SerializeField] private float deathSoundVolume = 0.8f;

    private static int totalDeaths;

    private PlayerController playerController;
    private GameplaySceneBootstrap gameplayBootstrap;
    private Rigidbody2D rb;
    private SpriteRenderer[] characterSprites;
    private Label deathCountLabel;

    private bool isDead;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetDeathCounter()
    {
        totalDeaths = 0;
    }

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        rb = GetComponent<Rigidbody2D>();

        if (playerController == null)
        {
            Debug.LogError(
                "PlayerDeathController requires PlayerController on the same GameObject.",
                this
            );

            enabled = false;
            return;
        }

        gameplayBootstrap =
            FindFirstObjectByType<GameplaySceneBootstrap>();

        characterSprites =
            GetComponentsInChildren<SpriteRenderer>(true);

        if (deathParticles == null)
            deathParticles = GetComponentInChildren<ParticleSystem>(true);

        if (deathAudioSource == null)
            deathAudioSource = GetComponent<AudioSource>();

        if (deathAudioSource == null)
            deathAudioSource = gameObject.AddComponent<AudioSource>();

        deathAudioSource.playOnAwake = false;
        deathAudioSource.loop = false;
        deathAudioSource.spatialBlend = 0f;
    }

    private void Start()
    {
        RefreshDeathCounter();
    }

    public void Die()
    {
        if (isDead)
            return;

        isDead = true;

        totalDeaths++;
        RefreshDeathCounter();

        // Freeze player physics.
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        // Spawn independent death particles.
        if (deathParticles != null)
        {
            ParticleSystem explosion = Instantiate(
                deathParticles,
                transform.position,
                Quaternion.identity
            );

            explosion.gameObject.SetActive(true);

            var main = explosion.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            explosion.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );

            explosion.Clear(true);
            explosion.Play(true);

            float particleDuration =
                main.duration +
                main.startDelay.constantMax +
                main.startLifetime.constantMax;

            Destroy(explosion.gameObject, particleDuration + 0.5f);
        }
        else
        {
            Debug.LogWarning(
                "Death Particles is not assigned.",
                this
            );
        }

        // Play death sound without extending restart delay.
        if (deathSound != null && deathAudioSource != null)
        {
            deathAudioSource.PlayOneShot(
                deathSound,
                deathSoundVolume
            );
        }

        // Hide player graphics.
        foreach (SpriteRenderer sprite in characterSprites)
        {
            if (sprite != null)
                sprite.enabled = false;
        }

        // Start existing restart system using ONLY the Inspector delay.
        if (gameplayBootstrap == null)
        {
            gameplayBootstrap =
                FindFirstObjectByType<GameplaySceneBootstrap>();
        }

        if (gameplayBootstrap != null)
        {
            gameplayBootstrap.PlayerDied(
                Mathf.Max(0f, deathRestartDelay)
            );
        }
        else
        {
            playerController.SetGameplayInputEnabled(false);

            Debug.LogError(
                "PlayerDeathController cannot find GameplaySceneBootstrap.",
                this
            );
        }

        // Stop movement and spin updates.
        playerController.enabled = false;
    }

    private void RefreshDeathCounter()
    {
        if (deathCountLabel == null)
        {
            UIDocument document =
                FindFirstObjectByType<UIDocument>();

            if (document != null)
            {
                deathCountLabel =
                    document.rootVisualElement.Q<Label>("death-count");
            }
        }

        if (deathCountLabel != null)
            deathCountLabel.text = totalDeaths.ToString("00");
    }
}
