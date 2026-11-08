using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerGameJuice : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Collider2D groundSensor;
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private CameraFollow cameraFollow;

    [Header("Landing Dust Particles")]
    [SerializeField] private ParticleSystem landingDustPrefab;
    [Min(0.01f)]
    [SerializeField] private float landingDustLifetime = 0.4f;
    [Min(0.01f)]
    [SerializeField] private float landingDustSize = 1f;
    [SerializeField] private float landingDustOffsetX = 0f;
    [SerializeField] private float landingDustOffsetY = 0.05f;

    [Header("Fall-Based Particle Scaling")]
    [Min(0.1f)]
    [SerializeField] private float maximumFallDistanceForEffects = 8f;

    [Min(1f)]
    [SerializeField] private float maximumParticleSizeMultiplier = 2.5f;

    [Min(1f)]
    [SerializeField] private float maximumParticleLifetimeMultiplier = 2.2f;

    [Header("Landing Detection")]
    [Min(0f)]
    [SerializeField] private float minimumLandingSpeed = 2f;

    [Min(0f)]
    [SerializeField] private float minimumFallDistance = 0.3f;

    [Min(0f)]
    [SerializeField] private float minimumDownwardSpeed = 1f;

    [Header("Landing Audio")]
    [SerializeField] private AudioSource landingAudioSource;
    [SerializeField] private AudioClip smallLandingSound;
    [SerializeField] private AudioClip bigLandingSound;

    [Tooltip("Falls shorter than this use the small landing sound. Falls at or above it use the big landing sound.")]
    [Min(0.1f)]
    [SerializeField] private float bigLandingThreshold = 3f;

    [Range(0f, 1f)]
    [SerializeField] private float smallLandingVolume = 0.65f;

    [Range(0f, 1f)]
    [SerializeField] private float bigLandingVolume = 1f;

    [Header("Camera Fall Shake")]
    [Min(0f)]
    [SerializeField] private float minimumCameraShakeFallDistance = 1.5f;

    [Min(0f)]
    [SerializeField] private float minimumCameraShakeImpactSpeed = 5f;

    [Tooltip("A jump must finish this much lower than its take-off height before it counts as jumping off a ledge.")]
    [Min(0f)]
    [SerializeField] private float minimumJumpOffDropDistance = 1.5f;

    [Header("Movement Dust")]
    [SerializeField] private ParticleSystem movementDustPrefab;
    [SerializeField] private float minimumTrailSpeed = 2f;
    [SerializeField] private float trailOffsetY = -0.35f;

    [Header("Squash and Stretch")]
    [SerializeField] private float landingSquashX = 1.25f;
    [SerializeField] private float landingSquashY = 0.72f;
    [SerializeField] private float jumpStretchX = 0.85f;
    [SerializeField] private float jumpStretchY = 1.18f;
    [SerializeField] private float scaleRecoverySpeed = 12f;

    private Rigidbody2D rb;
    private ParticleSystem movementDust;

    private Vector3 originalScale;
    private Vector3 targetScale;

    private bool wasGrounded;
    private float previousVerticalVelocity;

    private float fallStartHeight;
    private float trackedFallDistance;

    private float lastGroundedHeight;
    private float airborneStartHeight;

    private bool isTrackingFall;
    private bool hasFallenEnough;
    private bool jumpedThisAirborne;
    private bool hasAirborneStartHeight;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (visualRoot == null)
        {
            Debug.LogError(
                "PlayerGameJuice: Assign your sprite/visual object to Visual Root.",
                this
            );

            enabled = false;
            return;
        }

        if (cameraFollow == null && Camera.main != null)
            cameraFollow = Camera.main.GetComponent<CameraFollow>();

        // Automatically create an audio source if one isn't assigned.
        if (landingAudioSource == null)
            landingAudioSource = GetComponent<AudioSource>();

        if (landingAudioSource == null)
            landingAudioSource = gameObject.AddComponent<AudioSource>();

        landingAudioSource.playOnAwake = false;
        landingAudioSource.loop = false;
        landingAudioSource.spatialBlend = 0f;

        originalScale = visualRoot.localScale;
        targetScale = originalScale;

        if (movementDustPrefab != null)
        {
            movementDust = Instantiate(movementDustPrefab, transform);

            movementDust.transform.localPosition =
                new Vector3(0f, trailOffsetY, 0f);

            movementDust.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }
    }

    private void Start()
    {
        wasGrounded = IsGrounded();

        previousVerticalVelocity = rb.linearVelocity.y;

        fallStartHeight = transform.position.y;
        lastGroundedHeight = transform.position.y;
        airborneStartHeight = transform.position.y;

        hasAirborneStartHeight = !wasGrounded;

        isTrackingFall = false;
        hasFallenEnough = false;
        jumpedThisAirborne = false;
    }

    private void Update()
    {
        bool grounded = IsGrounded();

        // Check landing before resetting the fall measurements.
        HandleLanding(grounded);

        UpdateFallTracking(grounded);

        HandleJump(grounded);
        HandleMovementDust(grounded);
        RecoverScale();

        wasGrounded = grounded;
        previousVerticalVelocity = rb.linearVelocity.y;
    }

    private bool IsGrounded()
    {
        if (groundSensor == null)
            return false;

        return groundSensor.IsTouchingLayers(groundLayers);
    }

    // --------------------------------------------------
    // FALL TRACKING
    // --------------------------------------------------

    private void UpdateFallTracking(bool grounded)
    {
        if (grounded)
        {
            isTrackingFall = false;
            hasFallenEnough = false;

            fallStartHeight = transform.position.y;
            trackedFallDistance = 0f;

            lastGroundedHeight = transform.position.y;

            jumpedThisAirborne = false;
            hasAirborneStartHeight = false;

            return;
        }

        // Record the height where the player left the ground.
        if (wasGrounded && !hasAirborneStartHeight)
        {
            airborneStartHeight = lastGroundedHeight;
            hasAirborneStartHeight = true;

            jumpedThisAirborne = false;
        }

        float verticalSpeed = rb.linearVelocity.y;

        // Keep updating the peak while the player is rising.
        if (verticalSpeed > 0.1f)
        {
            fallStartHeight = transform.position.y;
            isTrackingFall = true;
        }

        // Measure the actual downward distance.
        if (verticalSpeed < -0.1f)
        {
            if (!isTrackingFall)
            {
                fallStartHeight = transform.position.y;
                isTrackingFall = true;
            }

            float currentFallDistance =
                fallStartHeight - transform.position.y;

            trackedFallDistance = Mathf.Max(
                trackedFallDistance,
                currentFallDistance
            );

            if (trackedFallDistance >= minimumFallDistance)
                hasFallenEnough = true;
        }
    }

    // --------------------------------------------------
    // LANDING DETECTION
    // --------------------------------------------------

    private void HandleLanding(bool grounded)
    {
        if (!grounded || wasGrounded)
            return;

        float impactSpeed = Mathf.Abs(previousVerticalVelocity);

        bool landedHardEnough =
            previousVerticalVelocity <= -minimumDownwardSpeed &&
            impactSpeed >= minimumLandingSpeed;

        float fallDistance = Mathf.Max(
            trackedFallDistance,
            Mathf.Max(
                0f,
                fallStartHeight - transform.position.y
            )
        );

        if (!hasFallenEnough || !landedHardEnough)
            return;

        // Play the appropriate landing sound.
        PlayLandingSound(fallDistance);

        // Landing dust and squash still work for ordinary jumps.
        SpawnLandingDust(fallDistance);
        Squash();

        // How much lower is the landing position than take-off?
        float dropFromTakeoff = hasAirborneStartHeight
            ? airborneStartHeight - transform.position.y
            : fallDistance;

        // Walking off a ledge always counts as a fall.
        // Jumping only counts if the player lands significantly lower
        // than the height from which the jump started.
        bool genuineFall =
            !jumpedThisAirborne ||
            dropFromTakeoff >= minimumJumpOffDropDistance;

        bool strongEnoughForCamera =
            fallDistance >= minimumCameraShakeFallDistance &&
            impactSpeed >= minimumCameraShakeImpactSpeed;

        if (
            genuineFall &&
            strongEnoughForCamera &&
            cameraFollow != null
        )
        {
            cameraFollow.TriggerFallImpact(
                fallDistance,
                impactSpeed
            );
        }
    }

    // --------------------------------------------------
    // LANDING AUDIO
    // --------------------------------------------------

    private void PlayLandingSound(float fallDistance)
    {
        if (landingAudioSource == null)
            return;

        bool isBigLanding = fallDistance >= bigLandingThreshold;

        AudioClip selectedSound = isBigLanding
            ? bigLandingSound
            : smallLandingSound;

        float selectedVolume = isBigLanding
            ? bigLandingVolume
            : smallLandingVolume;

        if (selectedSound == null)
            return;

        landingAudioSource.PlayOneShot(
            selectedSound,
            selectedVolume
        );
    }

    // --------------------------------------------------
    // JUMP EFFECT
    // --------------------------------------------------

    private void HandleJump(bool grounded)
    {
        bool hasStartedJump =
            wasGrounded &&
            !grounded &&
            rb.linearVelocity.y > 0.5f;

        if (hasStartedJump)
        {
            jumpedThisAirborne = true;
            Stretch();
        }
    }

    // --------------------------------------------------
    // MOVEMENT DUST
    // --------------------------------------------------

    private void HandleMovementDust(bool grounded)
    {
        if (movementDust == null)
            return;

        bool movingOnGround =
            grounded &&
            Mathf.Abs(rb.linearVelocity.x) >= minimumTrailSpeed;

        if (movingOnGround)
        {
            if (!movementDust.isPlaying)
                movementDust.Play();

            float direction = Mathf.Sign(rb.linearVelocity.x);

            movementDust.transform.localPosition =
                new Vector3(
                    -direction * 0.3f,
                    trailOffsetY,
                    0f
                );
        }
        else
        {
            if (movementDust.isPlaying)
            {
                movementDust.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmitting
                );
            }
        }
    }

    // --------------------------------------------------
    // LANDING DUST PARTICLES
    // --------------------------------------------------

    private void SpawnLandingDust(float fallDistance)
    {
        if (landingDustPrefab == null)
            return;

        Vector3 spawnPosition;

        if (groundSensor != null)
        {
            spawnPosition = new Vector3(
                transform.position.x + landingDustOffsetX,
                groundSensor.bounds.min.y + landingDustOffsetY,
                transform.position.z
            );
        }
        else
        {
            spawnPosition = transform.position;
        }

        ParticleSystem particles = Instantiate(
            landingDustPrefab,
            spawnPosition,
            Quaternion.identity
        );

        float fallStrength = Mathf.InverseLerp(
            minimumFallDistance,
            Mathf.Max(
                minimumFallDistance + 0.1f,
                maximumFallDistanceForEffects
            ),
            fallDistance
        );

        fallStrength = Mathf.SmoothStep(
            0f,
            1f,
            fallStrength
        );

        // Increase particle size with fall distance.
        float sizeMultiplier = Mathf.Lerp(
            1f,
            maximumParticleSizeMultiplier,
            fallStrength
        );

        // Increase particle lifetime with fall distance.
        float lifetimeMultiplier = Mathf.Lerp(
            1f,
            maximumParticleLifetimeMultiplier,
            fallStrength
        );

        particles.transform.localScale =
            landingDustPrefab.transform.localScale * landingDustSize;

        ParticleSystem.MainModule main = particles.main;

        main.startLifetime = landingDustLifetime * lifetimeMultiplier;
        main.startSizeMultiplier *= sizeMultiplier;

        particles.Play();

        float actualLifetime =
            landingDustLifetime * lifetimeMultiplier;

        float destroyDelay =
            main.duration + actualLifetime + 0.5f;

        Destroy(particles.gameObject, destroyDelay);
    }

    // --------------------------------------------------
    // SQUASH
    // --------------------------------------------------

    private void Squash()
    {
        targetScale = new Vector3(
            originalScale.x * landingSquashX,
            originalScale.y * landingSquashY,
            originalScale.z
        );
    }

    // --------------------------------------------------
    // STRETCH
    // --------------------------------------------------

    private void Stretch()
    {
        targetScale = new Vector3(
            originalScale.x * jumpStretchX,
            originalScale.y * jumpStretchY,
            originalScale.z
        );
    }

    // --------------------------------------------------
    // SCALE RECOVERY
    // --------------------------------------------------

    private void RecoverScale()
    {
        visualRoot.localScale = Vector3.Lerp(
            visualRoot.localScale,
            targetScale,
            scaleRecoverySpeed * Time.deltaTime
        );

        if (Vector3.Distance(
            visualRoot.localScale,
            targetScale
        ) < 0.005f)
        {
            targetScale = originalScale;
        }
    }

    private void OnDisable()
    {
        if (movementDust != null)
        {
            movementDust.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        if (visualRoot != null)
            visualRoot.localScale = originalScale;
    }
}