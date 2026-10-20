using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public static Action OnPlayerRespawn;
    public event Action OnPlayerDeath;

    [Header("Movement Tweaks")]
    public float moveSpeed = 5f;
    public float jumpForce = 10f;
    public float doubleJumpForce = 12f;
    public float airControlSpeed = 3f;

    [Header("Juicy Spin")]
    public float jumpSpinForce = 700f;
    public float doubleJumpSpinForce = 1100f;
    public float spinDrag = 6f;

    [Header("Squash and Stretch")]
    public float jumpStretch = 1.2f;
    public float jumpSquash = 0.8f;
    public float landSquash = 0.7f;
    public float squashSpeed = 12f;
    public float squashMultiplier = 0.5f;

    [Header("Ground Detection")]
    public Transform groundCheck;
    public Vector2 groundCheckSize = new Vector2(0.5f, 0.1f);
    public LayerMask groundLayer;
    public float groundCheckOffset = 0.55f;

    [SerializeField, Min(0.01f)]
    private float groundCastDistance = 0.12f;

    [Header("Camera Shake Link")]
    public CameraFollow camFollow;
    public float maxShakeMagnitude = 0.8f;

    [Header("Stats & Respawn")]
    public int deathCount = 0;
    public Transform respawnPoint;
    public GameObject deathEffectPrefab;
    public float respawnDelay = 0.9f;

    [Header("Visuals")]
    public TrailRenderer trail;


    // =========================================================
    // PLATFORMING FORGIVENESS
    // =========================================================

    [Header("Coyote Time and Jump Buffer")]
    [SerializeField, Range(0f, 0.25f)]
    private float coyoteTime = 0.12f;

    [SerializeField, Range(0f, 0.25f)]
    private float jumpBufferTime = 0.12f;

    [Header("Variable Jump Height")]
    [SerializeField, Range(0.1f, 0.9f)]
    private float jumpCutMultiplier = 0.45f;

    [SerializeField, Range(0f, 0.25f)]
    private float runUpJumpBonus = 0.10f;

    [Header("Jump Apex")]
    [SerializeField, Min(0f)]
    private float apexSlowdownAcceleration = 3f;

    [SerializeField, Min(0.05f)]
    private float apexVelocityThreshold = 0.8f;

    [SerializeField, Min(1f)]
    private float apexAirControlMultiplier = 1.5f;

    [Header("Falling")]
    [SerializeField, Min(1f)]
    private float maximumFallSpeed = 18f;

    [Header("Sticky Feet on Landing")]
    [SerializeField, Min(0f)]
    private float stickyLandingDuration = 0.12f;

    [SerializeField, Min(0f)]
    private float stickyLandingDeceleration = 55f;

    [Header("Missed Landing Assistance")]
    [SerializeField]
    private bool enableMissedJumpAssistance = true;

    [SerializeField, Range(0f, 0.25f)]
    private float missedJumpLift = 0.20f;

    [SerializeField, Range(0f, 0.20f)]
    private float missedJumpDrop = 0.14f;

    [SerializeField, Range(0f, 0.20f)]
    private float missedJumpForwardDistance = 0.12f;

    [Tooltip("Brief lockout after a catch to prevent repeated position corrections causing visible vibration.")]
    [SerializeField, Range(0.03f, 0.25f)]
    private float missedJumpAssistCooldown = 0.12f;

    private float nextMissedJumpAssistTime;

    [Header("Head Bump Correction")]
    [SerializeField]
    private bool enableHeadBumpCorrection = true;

    [Tooltip("Minimum upward speed needed before a ceiling hit triggers the assist.")]
    [SerializeField, Min(0f)]
    private float headBumpMinimumUpwardSpeed = 0.05f;

    [Tooltip("How far below the player's top edge a contact can be and still count as a head hit.")]
    [SerializeField, Range(0.01f, 0.3f)]
    private float headBumpDetectionTolerance = 0.10f;

    [Tooltip("Stop all upward velocity when the head hits a ceiling.")]
    [SerializeField]
    private bool headBumpStopUpwardVelocity = true;

    [Tooltip("Optional small downward push after a head hit. Set to 0 for no extra push.")]
    [SerializeField, Range(0f, 5f)]
    private float headBumpDownwardAssist = 0.0f;

    [Header("Corner Assistance")]
    [SerializeField]
    private bool enableCornerClip = true;

    [SerializeField, Range(0f, 0.2f)]
    private float cornerCorrectionDistance = 0.16f;

    [SerializeField, Range(0.005f, 0.04f)]
    private float cornerCorrectionStep = 0.01f;

    [Header("Ledge Crouch")]
    [SerializeField]
    private bool enableLedgeCrouch = true;

    [SerializeField, Range(0.1f, 1f)]
    private float crouchSpeedMultiplier = 0.5f;

    [SerializeField, Range(0.01f, 0.2f)]
    private float ledgeLookAhead = 0.08f;



    // =========================================================
    // REFERENCES AND STATE
    // =========================================================

    private Rigidbody2D rb;
    private Collider2D playerCollider;

    private SpriteRenderer spriteRenderer;
    private SpriteRenderer sourceSpriteRenderer;
    private Transform visualRoot;

    private bool isGrounded;
    private bool hasUsedDoubleJump;
    private bool isDead;

    private Vector3 originalScale;
    private Vector3 targetScale;

    private float spinVelocity;
    private float previousYVelocity;
    private float pendingShakeMagnitude;

    // New Input System.
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction crouchAction;

    private float moveInput;
    private bool jumpHeld;

    // Forgiveness state.
    private float coyoteCounter;
    private float jumpBufferCounter;
    private float stickyLandingCounter;
    private float landingMomentumX;

    private bool jumpCutApplied;
    private bool playerJumpInProgress;
    private bool jumpWasBufferedInAir;
    private bool apexAssistLogged;
    private bool fallCapLogged;
    private bool ledgeCrouchWasBlocking;

    private int runtimeGroundMask;

    private ContactFilter2D groundFilter;
    private readonly RaycastHit2D[] groundHits =
        new RaycastHit2D[16];

    // Reusable non-allocating physics-query buffers. Reusing these avoids
    // creating arrays every frame while keeping the same assist checks.
    private readonly RaycastHit2D[] missedLandingHits =
        new RaycastHit2D[32];

    // Reusable lists use the current Physics2D query APIs without allocating
    // a new result array for each check.
    private readonly List<Collider2D> assistOverlapHits =
        new List<Collider2D>(64);

    private readonly List<RaycastHit2D> groundBoxCastHits =
        new List<RaycastHit2D>(16);

    // Cached candidate offsets: same order and values as the original checks.
    private readonly float[] missedLandingProbeOffsets = new float[3];
    private readonly float[] missedLandingForwardOffsets = new float[4];
    private readonly float[] missedLandingExtraLifts =
        { 0f, 0.025f, 0.05f, 0.075f, 0.10f };



    // =========================================================
    // NEW INPUT SYSTEM
    // =========================================================

    private void OnEnable()
    {
        moveAction = new InputAction(
            type: InputActionType.Value);

        moveAction.AddCompositeBinding("1DAxis")
            .With("Negative", "<Keyboard>/a")
            .With("Positive", "<Keyboard>/d");

        moveAction.AddBinding("<Keyboard>/leftArrow");
        moveAction.AddBinding("<Keyboard>/rightArrow");
        moveAction.AddBinding("<Gamepad>/leftStick/x");

        moveAction.performed += HandleMovePerformed;
        moveAction.canceled += HandleMoveCancelled;


        jumpAction = new InputAction(
            type: InputActionType.Button);

        jumpAction.AddBinding("<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");

        jumpAction.performed += HandleJumpPressed;
        jumpAction.canceled += HandleJumpReleased;


        crouchAction = new InputAction(
            "Crouch",
            InputActionType.Button);

        crouchAction.AddBinding("<Keyboard>/leftCtrl");
        crouchAction.AddBinding("<Keyboard>/s");
        crouchAction.AddBinding("<Keyboard>/downArrow");
        crouchAction.AddBinding("<Gamepad>/dpad/down");
        crouchAction.AddBinding("<Gamepad>/leftStick/down");

        moveAction.Enable();
        jumpAction.Enable();
        crouchAction.Enable();
    }


    private void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.performed -= HandleMovePerformed;
            moveAction.canceled -= HandleMoveCancelled;

            moveAction.Disable();
            moveAction.Dispose();
            moveAction = null;
        }

        if (jumpAction != null)
        {
            jumpAction.performed -= HandleJumpPressed;
            jumpAction.canceled -= HandleJumpReleased;

            jumpAction.Disable();
            jumpAction.Dispose();
            jumpAction = null;
        }

        if (crouchAction != null)
        {
            crouchAction.Disable();
            crouchAction.Dispose();
            crouchAction = null;
        }

    }


    private void HandleMovePerformed(
        InputAction.CallbackContext context)
    {
        moveInput = Mathf.Clamp(
            context.ReadValue<float>(),
            -1f,
            1f);
    }


    private void HandleMoveCancelled(
        InputAction.CallbackContext context)
    {
        moveInput = 0f;
    }


    private void HandleJumpPressed(
        InputAction.CallbackContext context)
    {
        jumpHeld = true;

        // Buffer every jump press. If pressed while airborne, this is a
        // genuine landing buffer and will be logged when it is consumed.
        jumpBufferCounter = jumpBufferTime;
        jumpWasBufferedInAir = !isGrounded && coyoteCounter <= 0f;

    }


    private void HandleJumpReleased(
        InputAction.CallbackContext context)
    {
        jumpHeld = false;
    }


    private bool IsCrouchHeld()
    {
        return crouchAction != null &&
               crouchAction.IsPressed();
    }


    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();

        if (rb == null || playerCollider == null)
        {

            enabled = false;
            return;
        }

        // Render between physics steps instead of displaying each fixed-step
        // position directly. This removes much of the visible floor jitter.
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        SetupVisuals();
        ResolveRuntimeGroundMask();

        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(runtimeGroundMask);
        groundFilter.useTriggers = false;

        originalScale = visualRoot != null
            ? visualRoot.localScale
            : Vector3.one;

        targetScale = originalScale;
    }


    // =========================================================
    // SEPARATE VISUALS FROM PHYSICS
    // =========================================================

    private void SetupVisuals()
    {
        SpriteRenderer existingRenderer =
            GetComponent<SpriteRenderer>();

        if (existingRenderer == null)
        {
            existingRenderer =
                GetComponentInChildren<SpriteRenderer>(true);
        }

        if (existingRenderer == null)
        {

            return;
        }

        bool rendererSharesPhysics =
            existingRenderer.transform == transform ||
            existingRenderer.GetComponent<Collider2D>() != null ||
            existingRenderer.GetComponentInChildren<Collider2D>(true) != null;

        if (!rendererSharesPhysics)
        {
            // Already on a separate visual transform.
            spriteRenderer = existingRenderer;
            visualRoot = existingRenderer.transform;
            return;
        }

        sourceSpriteRenderer = existingRenderer;

        Transform sourceTransform = existingRenderer.transform;

        Vector3 localPosition =
            transform.InverseTransformPoint(sourceTransform.position);

        Quaternion localRotation =
            Quaternion.Inverse(transform.rotation) *
            sourceTransform.rotation;

        Vector3 localScale = DivideScale(
            sourceTransform.lossyScale,
            transform.lossyScale);

        GameObject visualObject = new GameObject("RuntimeVisuals");
        visualObject.layer = existingRenderer.gameObject.layer;

        visualRoot = visualObject.transform;
        visualRoot.SetParent(transform, false);

        visualRoot.localPosition = localPosition;
        visualRoot.localRotation = localRotation;
        visualRoot.localScale = localScale;

        spriteRenderer =
            visualObject.AddComponent<SpriteRenderer>();

        CopySpriteAppearance(
            sourceSpriteRenderer,
            spriteRenderer);

        spriteRenderer.enabled = sourceSpriteRenderer.enabled;

        // Hide only the original rendering component.
        // Its collider and the physics root remain unchanged.
        sourceSpriteRenderer.enabled = false;
    }


    private Vector3 DivideScale(
        Vector3 numerator,
        Vector3 denominator)
    {
        return new Vector3(
            Mathf.Abs(denominator.x) > 0.0001f
                ? numerator.x / denominator.x
                : numerator.x,

            Mathf.Abs(denominator.y) > 0.0001f
                ? numerator.y / denominator.y
                : numerator.y,

            Mathf.Abs(denominator.z) > 0.0001f
                ? numerator.z / denominator.z
                : numerator.z);
    }


    private void CopySpriteAppearance(
        SpriteRenderer source,
        SpriteRenderer destination)
    {
        if (source == null || destination == null)
            return;

        destination.sprite = source.sprite;
        destination.color = source.color;
        destination.flipX = source.flipX;
        destination.flipY = source.flipY;

        destination.sharedMaterial = source.sharedMaterial;
        destination.maskInteraction = source.maskInteraction;

        destination.drawMode = source.drawMode;
        destination.size = source.size;
        destination.tileMode = source.tileMode;
        destination.adaptiveModeThreshold =
            source.adaptiveModeThreshold;

        destination.spriteSortPoint = source.spriteSortPoint;
        destination.sortingLayerID = source.sortingLayerID;
        destination.sortingOrder = source.sortingOrder;

        destination.shadowCastingMode = source.shadowCastingMode;
        destination.receiveShadows = source.receiveShadows;
    }


    private void SyncRuntimeVisual()
    {
        if (sourceSpriteRenderer == null ||
            spriteRenderer == null)
        {
            return;
        }

        // Keep animated sprite properties synchronized.
        CopySpriteAppearance(
            sourceSpriteRenderer,
            spriteRenderer);

        sourceSpriteRenderer.enabled = false;
    }


    // =========================================================
    // RUNTIME GROUND MASK
    // =========================================================

    private void ResolveRuntimeGroundMask()
    {
        runtimeGroundMask = groundLayer.value;

        if (runtimeGroundMask == 0)
        {
            GameObject groundObject = GameObject.Find("Ground");

            if (groundObject != null)
            {
                runtimeGroundMask =
                    1 << groundObject.layer;
            }
            else
            {
                int layerIndex =
                    LayerMask.NameToLayer("Ground");

                if (layerIndex >= 0)
                {
                    runtimeGroundMask = 1 << layerIndex;
                }
                else
                {
                    runtimeGroundMask =
                        ~(1 << gameObject.layer);

                }
            }
        }

    }


    // =========================================================
    // MAIN UPDATE
    // =========================================================

    private void Update()
    {
        if (isDead || rb == null || playerCollider == null)
            return;

        ForceGroundCheckPosition();
        CheckGround();

        if (enableMissedJumpAssistance)
            TryCatchMissedJump();

        HandleMovement();
        HandleJump();

        ApplyEarlyJumpRelease();
        ApplyApexAndFallSpeed();


        HandleSpin();
        HandleJellySquash();

        previousYVelocity = rb.linearVelocity.y;
    }


    // =========================================================
    // GROUND DETECTION: ONLY UPWARD-FACING SURFACES COUNT
    // =========================================================

    private bool QueryGround()
    {
        // Ignore real upward movement, but tolerate tiny contact-solver
        // impulses while standing on the floor so grounded state won't flicker.
        if (rb.linearVelocity.y > 0.5f)
            return false;

        int hitCount = playerCollider.Cast(
            Vector2.down,
            groundFilter,
            groundHits,
            groundCastDistance);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = groundHits[i];

            if (hit.collider == null)
                continue;

            if (hit.rigidbody == rb ||
                hit.collider.isTrigger)
            {
                continue;
            }

            // A vertical wall does not have an upward normal.
            if (hit.normal.y >= 0.65f)
                return true;
        }

        // Fallback around the original GroundCheck position.
        if (groundCheck != null)
        {
            ContactFilter2D fallbackFilter = new ContactFilter2D();
            fallbackFilter.SetLayerMask(runtimeGroundMask);
            fallbackFilter.useTriggers = true;

            groundBoxCastHits.Clear();
            Physics2D.BoxCast(
                groundCheck.position,
                groundCheckSize,
                0f,
                Vector2.down,
                fallbackFilter,
                groundBoxCastHits,
                0.03f);
            int fallbackCount = groundBoxCastHits.Count;

            for (int i = 0; i < fallbackCount; i++)
            {
                RaycastHit2D hit = groundBoxCastHits[i];

                if (hit.collider == null ||
                    hit.rigidbody == rb ||
                    hit.collider.isTrigger)
                {
                    continue;
                }

                if (hit.normal.y >= 0.65f)
                    return true;
            }
        }

        return false;
    }


    private void CheckGround()
    {
        bool wasGrounded = isGrounded;

        isGrounded = QueryGround();

        if (isGrounded)
        {
            coyoteCounter = coyoteTime;

            // Only remove tiny vertical solver movement when the collider is
            // actually touching a ground layer. This avoids both floor buzz
            // and hovering above the floor from the longer ground cast.
            if (rb.linearVelocity.y <= 0.15f &&
                playerCollider.IsTouchingLayers(runtimeGroundMask))
            {
                Vector2 stableVelocity = rb.linearVelocity;
                stableVelocity.y = 0f;
                rb.linearVelocity = stableVelocity;
            }
        }
        else
        {
            coyoteCounter = Mathf.Max(
                0f,
                coyoteCounter - Time.deltaTime);
        }

        if (isGrounded && !wasGrounded)
            RegisterLanding();
    }


    private void RegisterLanding()
    {
        hasUsedDoubleJump = false;

        landingMomentumX = rb.linearVelocity.x;
        stickyLandingCounter = stickyLandingDuration;

        playerJumpInProgress = false;

        if (visualRoot != null)
        {
            // Snap the sprite, not the physics root.
            float snapped = Mathf.Round(
                visualRoot.localEulerAngles.z / 90f) * 90f;

            visualRoot.localRotation =
                Quaternion.Euler(0f, 0f, snapped);
        }

        spinVelocity = 0f;

        targetScale = new Vector3(
            originalScale.x * 1.2f,
            originalScale.y * landSquash,
            originalScale.z);

        CancelInvoke(nameof(ResetScale));
        Invoke(nameof(ResetScale), 0.08f);

        if (camFollow != null)
        {
            float landingSpeed =
                Mathf.Abs(previousYVelocity);

            pendingShakeMagnitude = Mathf.Clamp(
                landingSpeed * 0.02f,
                0f,
                maxShakeMagnitude);

            Invoke(nameof(TriggerCameraShake), 0.05f);
        }
    }


    // =========================================================
    // MOVEMENT
    // =========================================================

    private void HandleMovement()
    {
        float effectiveMoveInput = moveInput;
        bool crouching = IsCrouchHeld();

        bool ledgeBlocking = enableLedgeCrouch &&
            crouching &&
            isGrounded &&
            Mathf.Abs(effectiveMoveInput) > 0.01f &&
            !HasGroundAhead(effectiveMoveInput);

        ledgeCrouchWasBlocking = ledgeBlocking;

        if (ledgeBlocking)
            effectiveMoveInput = 0f;

        float dt = Time.deltaTime;

        if (isGrounded)
        {
            float effectiveSpeed = moveSpeed;

            if (enableLedgeCrouch && crouching)
                effectiveSpeed *= crouchSpeedMultiplier;

            float targetX = effectiveMoveInput * effectiveSpeed;

            bool reversingOnLanding =
                stickyLandingCounter > 0f &&
                Mathf.Abs(effectiveMoveInput) > 0.01f &&
                landingMomentumX * effectiveMoveInput < 0f;

            if (reversingOnLanding)
            {
                rb.linearVelocity = new Vector2(
                    Mathf.MoveTowards(
                        rb.linearVelocity.x,
                        targetX,
                        stickyLandingDeceleration * dt),
                    rb.linearVelocity.y);
            }
            else
            {
                // Original instant ground movement is retained.
                rb.linearVelocity = new Vector2(
                    effectiveMoveInput * effectiveSpeed,
                    rb.linearVelocity.y);
            }
        }
        else
        {
            float effectiveAirControl = airControlSpeed;

            if (Mathf.Abs(rb.linearVelocity.y) <=
                apexVelocityThreshold)
            {
                effectiveAirControl *= apexAirControlMultiplier;
            }

            // Original Lerp-based air control is retained.
            rb.linearVelocity = new Vector2(
                Mathf.Lerp(
                    rb.linearVelocity.x,
                    moveInput * moveSpeed,
                    effectiveAirControl * dt),
                rb.linearVelocity.y);
        }

        stickyLandingCounter = Mathf.Max(
            0f,
            stickyLandingCounter - dt);
    }


    // =========================================================
    // JUMP BUFFER, COYOTE TIME AND DOUBLE JUMP
    // =========================================================

    private void HandleJump()
    {
        if (jumpBufferCounter > 0f)
        {
            if (isGrounded || coyoteCounter > 0f)
            {
                bool usedCoyoteTime = !isGrounded && coyoteCounter > 0f;
                bool usedJumpBuffer = jumpWasBufferedInAir;
                float runUp = Mathf.Clamp01(
                    Mathf.Abs(rb.linearVelocity.x) /
                    Mathf.Max(0.01f, moveSpeed));

                float variableJumpForce =
                    jumpForce * (1f + runUpJumpBonus * runUp);

                PerformJump(variableJumpForce);

                jumpWasBufferedInAir = false;
                apexAssistLogged = false;
                fallCapLogged = false;
                hasUsedDoubleJump = false;
                spinVelocity += jumpSpinForce;

                targetScale = new Vector3(
                    originalScale.x * jumpSquash,
                    originalScale.y * jumpStretch,
                    originalScale.z);

                coyoteCounter = 0f;
                isGrounded = false;
                jumpBufferCounter = 0f;
            }
            else if (!hasUsedDoubleJump)
            {
                // Preserve the original double jump.
                PerformJump(doubleJumpForce);
                apexAssistLogged = false;
                fallCapLogged = false;

                hasUsedDoubleJump = true;
                spinVelocity += doubleJumpSpinForce;

                targetScale = new Vector3(
                    originalScale.x * 0.75f,
                    originalScale.y * 1.3f,
                    originalScale.z);

                jumpBufferCounter = 0f;
            }
        }

        jumpBufferCounter = Mathf.Max(
            0f,
            jumpBufferCounter - Time.deltaTime);
        if (jumpBufferCounter <= 0f)
            jumpWasBufferedInAir = false;
    }


    private void PerformJump(float force)
    {
        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x,
            0f);

        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x,
            force);

        coyoteCounter = 0f;
        playerJumpInProgress = true;
        jumpCutApplied = false;
    }


    // =========================================================
    // VARIABLE JUMP HEIGHT
    // =========================================================

    private void ApplyEarlyJumpRelease()
    {
        if (!playerJumpInProgress ||
            jumpHeld ||
            jumpCutApplied ||
            rb.linearVelocity.y <= 0f)
        {
            return;
        }

        Vector2 velocity = rb.linearVelocity;
        velocity.y *= jumpCutMultiplier;

        rb.linearVelocity = velocity;
        jumpCutApplied = true;
    }


    // =========================================================
    // APEX CONTROL AND FALL-SPEED CLAMP
    // =========================================================

    private void ApplyApexAndFallSpeed()
    {
        Vector2 velocity = rb.linearVelocity;

        // Counteract a small amount of gravity near the apex.
        // Unlike repeatedly moving velocity toward zero, this does
        // not hold the character at zero vertical speed.
        if (!isGrounded &&
            Mathf.Abs(velocity.y) < apexVelocityThreshold)
        {
            if (!apexAssistLogged)
            {
                apexAssistLogged = true;
            }
            float correction =
                apexSlowdownAcceleration * Time.deltaTime;

            if (velocity.y >= 0f)
            {
                velocity.y += correction;
            }
            else
            {
                velocity.y = Mathf.Min(
                    0f,
                    velocity.y + correction);
            }
        }

        if (velocity.y < -maximumFallSpeed)
        {
            velocity.y = -maximumFallSpeed;
            if (!fallCapLogged)
            {
                fallCapLogged = true;
            }
        }

        rb.linearVelocity = velocity;
    }


    // =========================================================
    // MISSED-LANDING ASSISTANCE
    // =========================================================

    private void TryCatchMissedJump()
    {
        if (rb == null || playerCollider == null || isGrounded ||
            Time.time < nextMissedJumpAssistTime ||
            rb.linearVelocity.y > 0.05f ||
            (missedJumpLift <= 0f && missedJumpDrop <= 0f))
        {
            return;
        }

        float direction = Mathf.Abs(moveInput) > 0.01f
            ? Mathf.Sign(moveInput)
            : Mathf.Abs(rb.linearVelocity.x) > 0.1f
                ? Mathf.Sign(rb.linearVelocity.x)
                : 0f;

        if (direction == 0f)
            return;

        Bounds bounds = playerCollider.bounds;
        float inset = Mathf.Min(0.02f, bounds.extents.x * 0.15f);
        float leadX = bounds.center.x + direction *
            Mathf.Max(0.005f, bounds.extents.x - inset + missedJumpForwardDistance);
        float innerX = bounds.center.x + direction *
            Mathf.Max(0f, bounds.extents.x * 0.35f);
        float centerX = bounds.center.x;

        float rayLength = missedJumpLift + missedJumpDrop + 0.08f;
        float rayStartY = bounds.min.y + missedJumpLift + 0.04f;
        missedLandingProbeOffsets[0] = leadX;
        missedLandingProbeOffsets[1] = innerX;
        missedLandingProbeOffsets[2] = centerX;

        RaycastHit2D bestHit = default;
        float bestCorrectionMagnitude = float.MaxValue;
        bool foundHit = false;

        // NonAlloc keeps the three probe checks without allocating a new array
        // on every Update. The buffer is intentionally larger than typical
        // tilemap contacts; process only the number returned by Unity.
        for (int probeIndex = 0; probeIndex < missedLandingProbeOffsets.Length; probeIndex++)
        {
            Vector2 origin = new Vector2(missedLandingProbeOffsets[probeIndex], rayStartY);
            int hitCount = Physics2D.RaycastNonAlloc(
                origin, Vector2.down, missedLandingHits, rayLength, runtimeGroundMask);

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                RaycastHit2D hit = missedLandingHits[hitIndex];
                if (hit.collider == null || hit.rigidbody == rb ||
                    hit.collider.isTrigger || hit.normal.y < 0.65f)
                    continue;

                float correction = hit.point.y - bounds.min.y;
                if (correction > missedJumpLift || correction < -missedJumpDrop)
                    continue;

                // Avoid catching a surface far below the feet: snapping down to it
                // repeatedly can fight the next ground check and look like vibration.
                if (correction < -0.025f)
                    continue;

                // Prefer a surface at or just above the feet over a lower
                // surface, so the assist does not snap the player downward.
                float score = Mathf.Abs(correction);
                if (!foundHit || score < bestCorrectionMagnitude)
                {
                    bestHit = hit;
                    bestCorrectionMagnitude = score;
                    foundHit = true;
                }
            }
        }

        if (!foundHit)
            return;

        float correctionY = bestHit.point.y - bounds.min.y + 0.012f;
        float forwardPush = Mathf.Clamp(missedJumpForwardDistance + 0.04f, 0.06f, 0.16f);
        Vector2 originalPosition = rb.position;
        Vector2 originalCenter = (Vector2)bounds.center;
        Vector2 testSize = new Vector2(
            Mathf.Max(0.01f, bounds.size.x * 0.90f),
            Mathf.Max(0.01f, bounds.size.y * 0.90f));

        // Try a small push in the direction of travel as well as progressively
        // higher positions. This escapes the common state where the player's
        // side is wedged against the vertical face of the step.
        missedLandingForwardOffsets[0] = forwardPush;
        missedLandingForwardOffsets[1] = forwardPush * 0.65f;
        missedLandingForwardOffsets[2] = 0f;
        missedLandingForwardOffsets[3] = -0.02f;
        bool foundSafePosition = false;
        Vector2 safePosition = originalPosition;
        float appliedLift = correctionY;
        float appliedForward = 0f;

        for (int liftIndex = 0; liftIndex < missedLandingExtraLifts.Length; liftIndex++)
        {
            float extraLift = missedLandingExtraLifts[liftIndex];
            for (int offsetIndex = 0; offsetIndex < missedLandingForwardOffsets.Length; offsetIndex++)
            {
                float forwardOffset = missedLandingForwardOffsets[offsetIndex];
                float xOffset = direction * forwardOffset;
                Vector2 candidateCenter = originalCenter +
                    new Vector2(xOffset, correctionY + extraLift);

                ContactFilter2D overlapFilter = new ContactFilter2D();
                overlapFilter.SetLayerMask(~0);
                overlapFilter.useTriggers = true;
                assistOverlapHits.Clear();
                Physics2D.OverlapBox(
                    candidateCenter, testSize, rb.rotation,
                    overlapFilter, assistOverlapHits);
                int overlapCount = assistOverlapHits.Count;

                bool blocked = false;
                for (int overlapIndex = 0; overlapIndex < overlapCount; overlapIndex++)
                {
                    Collider2D overlap = assistOverlapHits[overlapIndex];
                    if (overlap != null && overlap.attachedRigidbody != rb &&
                        !overlap.isTrigger && overlap != bestHit.collider)
                    {
                        blocked = true;
                        break;
                    }
                }

                if (blocked)
                    continue;

                safePosition = originalPosition + new Vector2(xOffset, correctionY + extraLift);
                appliedLift = correctionY + extraLift;
                appliedForward = xOffset;
                foundSafePosition = true;
                break;
            }

            if (foundSafePosition)
                break;
        }

        if (!foundSafePosition)
            return;

        // Apply one correction, then give the physics/ground detector time to settle.
        // Do not repeatedly teleport the body on consecutive Update frames.
        rb.position = safePosition;
        nextMissedJumpAssistTime = Time.time + missedJumpAssistCooldown;

        Vector2 velocity = rb.linearVelocity;
        velocity.y = 0f;
        // Preserve momentum in the direction the player was trying to move.
        if (Mathf.Abs(moveInput) > 0.01f && velocity.x * direction < 0f)
            velocity.x = 0f;
        rb.linearVelocity = velocity;

        bool wasGrounded = isGrounded;
        isGrounded = true;
        coyoteCounter = coyoteTime;
        if (!wasGrounded)
            RegisterLanding();

    }

    // =========================================================
    // LEDGE CROUCH
    // =========================================================

    private bool HasGroundAhead(float direction)
    {
        Bounds bounds = playerCollider.bounds;

        Vector2 origin = new Vector2(
            bounds.center.x +
            Mathf.Sign(direction) *
            (bounds.extents.x + ledgeLookAhead),
            bounds.min.y + 0.02f);

        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            Vector2.down,
            groundCastDistance + 0.2f,
            runtimeGroundMask);

        return hit.collider != null &&
               hit.rigidbody != rb &&
               !hit.collider.isTrigger &&
               hit.normal.y >= 0.65f;
    }


    private void LateUpdate()
    {
        SyncRuntimeVisual();
    }


    // =========================================================
    // HEAD BUMP AND CORNER ASSISTANCE
    // =========================================================

    // IMPORTANT: run correction on collision enter only.
    // Never repeatedly reposition the physics body every collision
    // stay frame. That was a source of the wall vibration.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleForgivingCollision(collision);
    }


    private void HandleForgivingCollision(Collision2D collision)
    {
        if (collision == null || rb == null || playerCollider == null)
            return;

        int layerBit = 1 << collision.gameObject.layer;
        if ((runtimeGroundMask & layerBit) == 0)
            return;

        Bounds bounds = playerCollider.bounds;
        bool hitHead = false;
        bool hitSide = false;

        // Unity may already have reduced the Rigidbody's upward velocity
        // by the time OnCollisionEnter2D runs. Use the previous frame's
        // velocity as well, otherwise the head-bump assist can be skipped.
        bool wasMovingUp = rb.linearVelocity.y > headBumpMinimumUpwardSpeed ||
            previousYVelocity > headBumpMinimumUpwardSpeed;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            Vector2 normal = contact.normal;

            // Detect a ceiling contact using both the normal and the
            // contact's position relative to the player's top edge.
            // This is more reliable than relying on the normal alone.
            bool nearTop = contact.point.y >= bounds.max.y - headBumpDetectionTolerance;
            bool ceilingNormal = normal.y < -0.5f;

            // A genuine ceiling normal is definitive. The contact-height fallback
            // helps on tilemap seams, but only if the contact is near the top edge.
            if (ceilingNormal || (nearTop && normal.y < -0.15f))
                hitHead = true;

            if (Mathf.Abs(normal.x) > 0.5f)
                hitSide = true;
        }

        if (enableHeadBumpCorrection && hitHead && wasMovingUp)
        {
            Vector2 velocity = rb.linearVelocity;

            if (headBumpStopUpwardVelocity && velocity.y > 0f)
                velocity.y = 0f;

            // Optional extra downward assistance, adjustable in the Inspector.
            if (headBumpDownwardAssist > 0f)
                velocity.y = Mathf.Min(velocity.y, -headBumpDownwardAssist);

            rb.linearVelocity = velocity;
        }

        // Keep corner correction restricted to upward movement and side
        // contacts so it does not interfere with ordinary floor movement.
        if (enableCornerClip && wasMovingUp && hitSide && !hitHead &&
            Mathf.Abs(moveInput) > 0.01f)
        {
            float direction = Mathf.Sign(moveInput);
            bool corrected = TryHorizontalCorrection(direction);
            if (!corrected)
                corrected = TryHorizontalCorrection(-direction);

        }
    }

    private bool TryHorizontalCorrection(float direction)
    {
        if (Mathf.Abs(direction) < 0.01f ||
            cornerCorrectionDistance <= 0f)
        {
            return false;
        }

        Bounds bounds = playerCollider.bounds;

        Vector2 testSize = new Vector2(
            bounds.size.x * 0.94f,
            bounds.size.y * 0.94f);

        float step = Mathf.Max(
            0.005f,
            cornerCorrectionStep);

        int steps = Mathf.CeilToInt(
            cornerCorrectionDistance / step);

        for (int i = 1; i <= steps; i++)
        {
            float offset =
                Mathf.Min(cornerCorrectionDistance, step * i) *
                Mathf.Sign(direction);

            Vector2 candidateCenter =
                (Vector2)bounds.center +
                Vector2.right * offset;

            ContactFilter2D overlapFilter = new ContactFilter2D();
            overlapFilter.SetLayerMask(~0);
            overlapFilter.useTriggers = true;
            assistOverlapHits.Clear();
            Physics2D.OverlapBox(
                candidateCenter,
                testSize,
                rb.rotation,
                overlapFilter,
                assistOverlapHits);
            int overlapCount = assistOverlapHits.Count;

            bool blocked = false;

            for (int overlapIndex = 0; overlapIndex < overlapCount; overlapIndex++)
            {
                Collider2D overlap = assistOverlapHits[overlapIndex];
                if (overlap != null &&
                    overlap.attachedRigidbody != rb &&
                    !overlap.isTrigger)
                {
                    blocked = true;
                    break;
                }
            }

            if (blocked)
                continue;

            rb.position += Vector2.right * offset;
            return true;
        }

        return false;
    }


    // =========================================================
    // VISUAL SPIN AND SQUASH
    // =========================================================

    private void HandleSpin()
    {
        if (!isGrounded && visualRoot != null)
        {
            visualRoot.Rotate(
                0f,
                0f,
                spinVelocity * Time.deltaTime);

            spinVelocity = Mathf.Lerp(
                spinVelocity,
                0f,
                spinDrag * Time.deltaTime);
        }
    }


    private void HandleJellySquash()
    {
        if (visualRoot == null)
            return;

        float yDiff =
            targetScale.y - visualRoot.localScale.y;

        float xDiff =
            -yDiff * squashMultiplier;

        Vector3 desiredScale = new Vector3(
            originalScale.x + xDiff,
            visualRoot.localScale.y + yDiff,
            originalScale.z);

        visualRoot.localScale = Vector3.Lerp(
            visualRoot.localScale,
            desiredScale,
            squashSpeed * Time.deltaTime);
    }


    // =========================================================
    // DEATH / RESPAWN
    // =========================================================

    private IEnumerator DeathSequence()
    {
        isDead = true;

        deathCount++;
        OnPlayerDeath?.Invoke();

        if (deathEffectPrefab != null)
        {
            Instantiate(
                deathEffectPrefab,
                transform.position,
                Quaternion.identity);
        }

        rb.linearVelocity = Vector2.zero;

        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        yield return new WaitForSeconds(respawnDelay);

        if (respawnPoint != null)
            rb.position = respawnPoint.position;

        if (spriteRenderer != null)
            spriteRenderer.enabled = true;

        coyoteCounter = 0f;
        jumpBufferCounter = 0f;
        stickyLandingCounter = 0f;

        jumpHeld = false;
        playerJumpInProgress = false;
        jumpCutApplied = false;
        hasUsedDoubleJump = false;
        isGrounded = false;

        OnPlayerRespawn?.Invoke();

        isDead = false;
    }


    // =========================================================
    // EXISTING HELPERS
    // =========================================================

    private void ResetScale()
    {
        targetScale = originalScale;
    }


    private void TriggerCameraShake()
    {
        if (camFollow != null)
        {
            camFollow.ShakeCamera(
                0.25f,
                pendingShakeMagnitude);
        }
    }


    private void ForceGroundCheckPosition()
    {
        if (groundCheck != null)
        {
            groundCheck.position =
                transform.position -
                Vector3.up * groundCheckOffset;
        }
    }


    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Death") && !isDead)
        {
            StartCoroutine(DeathSequence());
        }
    }


    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;

            Gizmos.DrawWireCube(
                groundCheck.position,
                groundCheckSize);
        }

        if (playerCollider != null)
        {
            Gizmos.color = Color.cyan;

            Gizmos.DrawWireCube(
                playerCollider.bounds.center,
                playerCollider.bounds.size);
        }
    }
}