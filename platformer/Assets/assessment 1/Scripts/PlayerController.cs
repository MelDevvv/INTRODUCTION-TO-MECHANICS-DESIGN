using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public partial class PlayerController : MonoBehaviour
{
    [Header("Movement Tweaks")]
    public float moveSpeed = 5f;
    public float jumpForce = 10f;
    public float doubleJumpForce = 12f;
    public float airControlSpeed = 3f;

    [Header("Juicy Spin")]
    public float jumpSpinForce = 700f;
    public float doubleJumpSpinForce = 1100f;
    public float spinDrag = 6f;

    [Header("Smooth Falling Spin")]
    [Tooltip("Target angular speed once the character is falling, in degrees per second.")]
    [SerializeField, Min(0f)] private float fallingSpinSpeed = 360f;
    [Tooltip("How quickly the character eases into its falling spin. Higher values react faster.")]
    [SerializeField, Min(1f)] private float fallingSpinAcceleration = 1200f;
    [Tooltip("Start aligning to a flat side when a platform is this close below the player.")]
    [SerializeField, Min(0.25f)] private float fallAlignmentStartDistance = 2.5f;
    [Tooltip("Angular deceleration used to settle onto a flat side without reversing spin direction.")]
    [SerializeField, Min(100f)] private float fallAlignmentDeceleration = 1800f;
    [Tooltip("How quickly any tiny remaining angle is settled after contact. Smaller values feel snappier.")]
    [SerializeField, Range(0.04f, 0.25f)] private float landingAlignmentSmoothTime = 0.10f;

    [Header("Ground Detection")]
    [Tooltip("Assign the small trigger collider at the player's feet. The controller uses the main collider's downward cast for stable, top-surface-only grounding.")]
    public Collider2D groundCheckCollider;
    public LayerMask groundLayer;
    [Tooltip("Short downward probe used to detect walkable top surfaces without treating platform sides as ground.")]
    [SerializeField, Range(0.01f, 0.20f)] private float groundProbeDistance = 0.055f;

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

    [Header("Landing Movement")]
    [Tooltip("Optional landing brake when reversing direction. Disabled by default to prevent the player feeling stuck after touching down.")]
    [SerializeField]
    private bool enableStickyLandingAssist = false;

    [Tooltip("How long the optional landing brake lasts after touchdown.")]
    [SerializeField, Min(0f)]
    private float stickyLandingDuration = 0.12f;

    [Tooltip("Deceleration applied by the optional landing brake when reversing direction.")]
    [SerializeField, Min(0f)]
    private float stickyLandingDeceleration = 55f;

    [Header("Missed Landing Assistance")]
    [Tooltip("Uses a small velocity-only nudge near a landing. It never repositions/teleports the Rigidbody.")]
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

    // Spin velocity is signed: positive Z is counter-clockwise, negative Z clockwise.
    private float spinVelocity;
    private int spinDirection = 1;
    private float spinAngle;
    private bool spinAngleInitialized;
    private bool spinAlignmentActive;
    private bool spinAlignmentComplete;
    private bool hasBeenAirborne;
    private float spinAlignmentTargetAngle;
    private bool landingAlignmentActive;
    private float landingAlignmentTargetAngle;
    private float landingAlignmentVelocity;
    private float previousYVelocity;

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

    // Reusable lists use the current Physics2D query APIs without allocating
    // a new result array for each check.
    private readonly List<Collider2D> assistOverlapHits =
        new List<Collider2D>(64);
    private readonly List<RaycastHit2D> assistCastHits =
        new List<RaycastHit2D>(32);

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

        if (visualRoot != null)
        {
            spinAngle = visualRoot.localEulerAngles.z;
            spinAngleInitialized = true;
        }
    }

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

    private void Update()
    {
        if (rb == null || playerCollider == null)
            return;

        CheckGround();

        if (enableMissedJumpAssistance)
            TryCatchMissedJump();

        HandleMovement();
        HandleJump();

        ApplyEarlyJumpRelease();
        ApplyApexAndFallSpeed();


        previousYVelocity = rb.linearVelocity.y;
    }

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
                enableStickyLandingAssist &&
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

    private void LateUpdate()
    {
        SyncRuntimeVisual();
        HandleSpin();
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D rootCollider = playerCollider != null ? playerCollider : GetComponent<Collider2D>();
        if (rootCollider != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(rootCollider.bounds.center, rootCollider.bounds.size);
        }

        // These coloured outlines make the editable trigger areas visible when
        // the Player is selected, even if the sensor child itself is not selected.
        if (upperLeftCornerSensor != null)
        {
            Gizmos.color = new Color(1f, 0.45f, 0.05f, 1f);
            Gizmos.DrawWireCube(upperLeftCornerSensor.bounds.center, upperLeftCornerSensor.bounds.size);
        }
        if (upperRightCornerSensor != null)
        {
            Gizmos.color = new Color(0.25f, 1f, 0.35f, 1f);
            Gizmos.DrawWireCube(upperRightCornerSensor.bounds.center, upperRightCornerSensor.bounds.size);
        }
    }
}