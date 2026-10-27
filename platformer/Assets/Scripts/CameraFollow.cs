using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Rigidbody2D targetRb;
    public Vector3 offset = new Vector3(0f, 1f, -10f);

    [Header("Smooth Follow")]
    [Min(0.01f)] public float smoothTime = 0.22f;
    public float maxFollowSpeed = 35f;
    public bool useUnscaledTime = false;

    [Header("Movement Look-Ahead")]
    public float lookAheadDistance = 1.5f;
    public float lookAheadSmoothTime = 0.25f;
    public float velocityForFullLookAhead = 8f;
    public float lookAheadDeadZone = 0.5f;

    [Tooltip("Horizontal speed needed before the camera starts leading the player.")]
    [Min(0f)] public float lookAheadStartSpeed = 0.9f;

    [Tooltip("A direction change must reach this speed before the camera lead switches sides.")]
    [Min(0f)] public float directionSwitchSpeed = 1.25f;

    [Tooltip("Below this speed, the camera smoothly releases its directional lead.")]
    [Min(0f)] public float directionReleaseSpeed = 0.25f;

    [Tooltip("Filters small physics velocity changes before they affect the camera.")]
    [Min(0.01f)] public float velocityFilterSmoothTime = 0.12f;

    [Header("Vertical Movement")]
    public float jumpLookUp = 0.8f;
    public float fallLookDown = 1.2f;
    public float verticalVelocityForFullLook = 10f;
    public float verticalLookSmoothTime = 0.2f;

    [Header("Camera Lead")]
    public float anticipationDistance = 0.25f;
    public float anticipationSmoothTime = 0.3f;

    [Header("Screen Shake")]
    public float shakeDuration = 0.2f;
    public float shakeMagnitude = 0.35f;
    public float dampingSpeed = 2f;
    public float shakeFrequency = 25f;
    public float rotationShake = 1.5f;

    [Header("Impact Effects")]
    public float landingShakeMultiplier = 1.5f;
    public float heavyImpactMultiplier = 2.5f;

    private Vector3 followVelocity;
    private Vector3 currentLookAhead;
    private Vector3 lookAheadVelocity;

    private float currentVerticalLook;
    private float verticalLookVelocity;
    private float currentAnticipation;
    private float anticipationVelocity;

    private float currentShakeDuration;
    private float activeShakeMagnitude;
    private float shakeTimer;
    private float shakeSeed;
    private float currentShakeRotation;

    private float baseZ;
    private Vector2 previousTargetPosition;
    private bool hasPreviousTargetPosition;

    // Filtered velocity and direction hysteresis stop small Rigidbody2D
    // velocity fluctuations from repeatedly flipping the camera lead.
    private Vector2 filteredTargetVelocity;
    private Vector2 velocityFilterDamp;
    private int horizontalLeadDirection;

    private void Awake()
    {
        baseZ = transform.position.z;
        shakeSeed = Random.Range(0f, 1000f);
    }

    private void Start()
    {
        if (target == null)
            return;

        if (targetRb == null)
            targetRb = target.GetComponent<Rigidbody2D>();

        previousTargetPosition = target.position;
        hasPreviousTargetPosition = true;

        if (targetRb != null)
            filteredTargetVelocity = targetRb.linearVelocity;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        // LateUpdate follows the visually interpolated Rigidbody2D transform.
        // Do not use Rigidbody2D.position here, as it is the fixed-step pose.
        float dt = useUnscaledTime
            ? Time.unscaledDeltaTime
            : Time.deltaTime;

        if (dt <= 0f)
            return;

        Vector2 rawVelocity = GetTargetVelocity(dt);
        filteredTargetVelocity = Vector2.SmoothDamp(
            filteredTargetVelocity,
            rawVelocity,
            ref velocityFilterDamp,
            Mathf.Max(0.01f, velocityFilterSmoothTime),
            Mathf.Infinity,
            dt
        );

        UpdateHorizontalLead(filteredTargetVelocity.x, dt);
        UpdateVerticalLook(filteredTargetVelocity.y, dt);

        Vector3 desiredPosition = target.position + offset;
        desiredPosition.x += currentLookAhead.x + currentAnticipation;
        desiredPosition.y += currentVerticalLook;
        desiredPosition.z = baseZ;

        Vector3 smoothedPosition = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref followVelocity,
            Mathf.Max(0.01f, smoothTime),
            Mathf.Max(0.01f, maxFollowSpeed),
            dt
        );

        UpdateShake(dt);
        smoothedPosition += GetShakeOffset();
        transform.position = smoothedPosition;

        transform.rotation = Quaternion.Euler(0f, 0f, GetShakeRotation());
    }

    private Vector2 GetTargetVelocity(float dt)
    {
        if (targetRb != null)
        {
            // Keep the fallback position history current even when a Rigidbody2D
            // is assigned, so switching targets/components doesn't cause a spike.
            previousTargetPosition = target.position;
            hasPreviousTargetPosition = true;
            return targetRb.linearVelocity;
        }

        Vector2 currentPosition = target.position;
        if (!hasPreviousTargetPosition)
        {
            previousTargetPosition = currentPosition;
            hasPreviousTargetPosition = true;
            return Vector2.zero;
        }

        // Important: estimate target motion from its previous position, not
        // from the camera position. Using camera position creates false velocity.
        Vector2 estimatedVelocity = (currentPosition - previousTargetPosition) / dt;
        previousTargetPosition = currentPosition;
        return estimatedVelocity;
    }

    private void UpdateHorizontalLead(float horizontalVelocity, float dt)
    {
        float speed = Mathf.Abs(horizontalVelocity);
        int measuredDirection = speed > 0.001f
            ? (horizontalVelocity > 0f ? 1 : -1)
            : 0;

        if (horizontalLeadDirection == 0)
        {
            // Start leading only after movement is clearly established.
            if (speed >= Mathf.Max(0f, lookAheadStartSpeed))
                horizontalLeadDirection = measuredDirection;
        }
        else
        {
            // Hysteresis: don't change sides because the velocity jitters near zero.
            if (measuredDirection == -horizontalLeadDirection &&
                speed >= Mathf.Max(lookAheadStartSpeed, directionSwitchSpeed))
            {
                horizontalLeadDirection = measuredDirection;
            }
            else if (speed <= directionReleaseSpeed)
            {
                horizontalLeadDirection = 0;
            }
        }

        float strength = 0f;
        if (horizontalLeadDirection != 0)
        {
            float fullSpeed = Mathf.Max(lookAheadStartSpeed + 0.01f,
                velocityForFullLookAhead);
            strength = Mathf.Clamp01(speed / fullSpeed);
        }

        float leadTarget = horizontalLeadDirection * lookAheadDistance * strength;
        float anticipationTarget = horizontalLeadDirection == 0
            ? 0f
            : horizontalLeadDirection * anticipationDistance * strength;

        // One filtered velocity and stable direction drive both effects, so
        // look-ahead and anticipation cannot fight each other or flicker sides.
        currentLookAhead.x = Mathf.SmoothDamp(
            currentLookAhead.x,
            leadTarget,
            ref lookAheadVelocity.x,
            Mathf.Max(0.01f, lookAheadSmoothTime),
            Mathf.Infinity,
            dt
        );
        currentLookAhead.y = 0f;
        currentLookAhead.z = 0f;

        currentAnticipation = Mathf.SmoothDamp(
            currentAnticipation,
            anticipationTarget,
            ref anticipationVelocity,
            Mathf.Max(0.01f, anticipationSmoothTime),
            Mathf.Infinity,
            dt
        );
    }

    private void UpdateVerticalLook(float verticalVelocity, float dt)
    {
        float verticalStrength = Mathf.Clamp01(
            Mathf.Abs(verticalVelocity) /
            Mathf.Max(0.01f, verticalVelocityForFullLook)
        );

        float targetVerticalLook = 0f;
        if (verticalVelocity > 0.5f)
            targetVerticalLook = jumpLookUp * verticalStrength;
        else if (verticalVelocity < -0.5f)
            targetVerticalLook = -fallLookDown * verticalStrength;

        currentVerticalLook = Mathf.SmoothDamp(
            currentVerticalLook,
            targetVerticalLook,
            ref verticalLookVelocity,
            Mathf.Max(0.01f, verticalLookSmoothTime),
            Mathf.Infinity,
            dt
        );
    }

    private void UpdateShake(float dt)
    {
        if (currentShakeDuration <= 0f)
        {
            currentShakeDuration = 0f;
            activeShakeMagnitude = 0f;
            currentShakeRotation = 0f;
            return;
        }

        currentShakeDuration -= dt;
        shakeTimer += dt;

        float fade = Mathf.Clamp01(
            currentShakeDuration / Mathf.Max(0.01f, shakeDuration)
        );

        activeShakeMagnitude = shakeMagnitude * fade;

        float noise = Mathf.PerlinNoise(
            shakeSeed,
            shakeTimer * shakeFrequency
        );

        currentShakeRotation =
            (noise - 0.5f) * 2f * rotationShake * fade;
    }

    private Vector3 GetShakeOffset()
    {
        if (activeShakeMagnitude <= 0f)
            return Vector3.zero;

        float time = shakeTimer * shakeFrequency;
        float x = Mathf.PerlinNoise(shakeSeed + 10f, time) - 0.5f;
        float y = Mathf.PerlinNoise(shakeSeed + 20f, time) - 0.5f;

        return new Vector3(x, y, 0f) * activeShakeMagnitude * 2f;
    }

    private float GetShakeRotation()
    {
        return currentShakeDuration > 0f
            ? currentShakeRotation
            : 0f;
    }

    // Call this from other scripts for impacts, landings or explosions.
    public void ShakeCamera(float duration, float magnitude)
    {
        if (duration <= 0f || magnitude <= 0f)
            return;

        currentShakeDuration = Mathf.Max(currentShakeDuration, duration);
        shakeDuration = Mathf.Max(0.01f, duration);
        shakeTimer = 0f;
        shakeSeed = Random.Range(0f, 1000f);
        activeShakeMagnitude = Mathf.Max(activeShakeMagnitude, magnitude);
    }

    public void ShakeLanding()
    {
        ShakeCamera(shakeDuration, shakeMagnitude * landingShakeMultiplier);
    }

    public void ShakeHeavyImpact()
    {
        ShakeCamera(shakeDuration * 1.5f, shakeMagnitude * heavyImpactMultiplier);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        targetRb = target != null
            ? target.GetComponent<Rigidbody2D>()
            : null;

        followVelocity = Vector3.zero;
        lookAheadVelocity = Vector3.zero;
        verticalLookVelocity = 0f;
        anticipationVelocity = 0f;
        velocityFilterDamp = Vector2.zero;
        horizontalLeadDirection = 0;
        currentLookAhead = Vector3.zero;
        currentAnticipation = 0f;
        currentVerticalLook = 0f;

        if (target != null)
        {
            previousTargetPosition = target.position;
            hasPreviousTargetPosition = true;
            filteredTargetVelocity = targetRb != null
                ? targetRb.linearVelocity
                : Vector2.zero;
        }
        else
        {
            hasPreviousTargetPosition = false;
            filteredTargetVelocity = Vector2.zero;
        }
    }

    private void OnDisable()
    {
        transform.rotation = Quaternion.identity;
    }
}