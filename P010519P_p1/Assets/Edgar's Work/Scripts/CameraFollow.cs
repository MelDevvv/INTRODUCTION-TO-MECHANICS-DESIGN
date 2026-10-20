
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
    private float originalZ;
    private float previousTargetY;
    private bool hasPreviousTargetPosition;

    private void Awake()
    {
        baseZ = transform.position.z;
        originalZ = baseZ;
        shakeSeed = Random.Range(0f, 1000f);
    }

    private void Start()
    {
        if (target == null)
            return;

        if (targetRb == null)
            targetRb = target.GetComponent<Rigidbody2D>();

        previousTargetY = target.position.y;
        hasPreviousTargetPosition = true;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        float dt = useUnscaledTime
            ? Time.unscaledDeltaTime
            : Time.deltaTime;

        if (dt <= 0f)
            return;

        Vector2 velocity = GetTargetVelocity(dt);

        // Look ahead in the direction the player is moving.
        float horizontalSpeed = Mathf.Abs(velocity.x);
        float lookStrength = Mathf.Clamp01(
            horizontalSpeed / Mathf.Max(0.01f, velocityForFullLookAhead)
        );

        float targetLookX = 0f;

        if (horizontalSpeed > lookAheadDeadZone)
        {
            targetLookX =
                Mathf.Sign(velocity.x) *
                lookAheadDistance *
                lookStrength;
        }

        Vector3 desiredLookAhead = new Vector3(targetLookX, 0f, 0f);

        currentLookAhead = Vector3.SmoothDamp(
            currentLookAhead,
            desiredLookAhead,
            ref lookAheadVelocity,
            Mathf.Max(0.01f, lookAheadSmoothTime),
            Mathf.Infinity,
            dt
        );

        // Look slightly up during a jump and down during a fall.
        float targetVerticalLook = 0f;
        float verticalStrength = Mathf.Clamp01(
            Mathf.Abs(velocity.y) /
            Mathf.Max(0.01f, verticalVelocityForFullLook)
        );

        if (velocity.y > 0.5f)
        {
            targetVerticalLook = jumpLookUp * verticalStrength;
        }
        else if (velocity.y < -0.5f)
        {
            targetVerticalLook = -fallLookDown * verticalStrength;
        }

        currentVerticalLook = Mathf.SmoothDamp(
            currentVerticalLook,
            targetVerticalLook,
            ref verticalLookVelocity,
            Mathf.Max(0.01f, verticalLookSmoothTime),
            Mathf.Infinity,
            dt
        );

        // Gentle anticipation gives movement a more responsive feel.
        float targetAnticipation = 0f;

        if (horizontalSpeed > lookAheadDeadZone)
        {
            targetAnticipation =
                Mathf.Sign(velocity.x) * anticipationDistance;
        }

        currentAnticipation = Mathf.SmoothDamp(
            currentAnticipation,
            targetAnticipation,
            ref anticipationVelocity,
            Mathf.Max(0.01f, anticipationSmoothTime),
            Mathf.Infinity,
            dt
        );

        Vector3 desiredPosition =
            target.position +
            offset +
            currentLookAhead;

        desiredPosition.x += currentAnticipation;
        desiredPosition.y += currentVerticalLook;
        desiredPosition.z = baseZ;

        Vector3 smoothedPosition = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref followVelocity,
            Mathf.Max(0.01f, smoothTime),
            maxFollowSpeed,
            dt
        );

        // Smooth, decaying shake is applied after normal camera movement.
        UpdateShake(dt);

        smoothedPosition += GetShakeOffset();
        transform.position = smoothedPosition;

        float shakeRotation = GetShakeRotation();
        transform.rotation = Quaternion.Euler(
            0f,
            0f,
            shakeRotation
        );
    }

    private Vector2 GetTargetVelocity(float dt)
    {
        if (targetRb != null)
            return targetRb.linearVelocity;

        if (!hasPreviousTargetPosition)
        {
            previousTargetY = target.position.y;
            hasPreviousTargetPosition = true;
            return Vector2.zero;
        }

        Vector2 estimatedVelocity = new Vector2(
            (target.position.x - transform.position.x) / dt,
            (target.position.y - previousTargetY) / dt
        );

        previousTargetY = target.position.y;
        return estimatedVelocity;
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

        activeShakeMagnitude = Mathf.Max(
            activeShakeMagnitude,
            magnitude
        );
    }

    public void ShakeLanding()
    {
        ShakeCamera(
            shakeDuration,
            shakeMagnitude * landingShakeMultiplier
        );
    }

    public void ShakeHeavyImpact()
    {
        ShakeCamera(
            shakeDuration * 1.5f,
            shakeMagnitude * heavyImpactMultiplier
        );
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

        if (target != null)
        {
            previousTargetY = target.position.y;
            hasPreviousTargetPosition = true;
        }
    }

    private void OnDisable()
    {
        transform.rotation = Quaternion.identity;
    }
}
