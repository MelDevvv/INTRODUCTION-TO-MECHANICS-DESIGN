
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Rigidbody2D targetRb;
    public Vector3 offset = new Vector3(0f, 1f, -10f);

    [Header("Smooth Follow")]
    [Min(0.01f)] public float smoothTime = 0.19f;
    public float maxFollowSpeed = 35f;

    [Header("Horizontal Look-Ahead")]
    public float lookAheadDistance = 2f;
    public float lookAheadSmoothTime = 0.25f;
    public float velocityForFullLookAhead = 8f;
    public float lookAheadDeadZone = 0.5f;
    public float lookAheadStartSpeed = 0.9f;
    public float directionSwitchSpeed = 1.25f;
    public float directionReleaseSpeed = 0.25f;
    public float velocityFilterSmoothTime = 0.12f;

    [Header("Vertical Look")]
    public float jumpLookUp = 1.1f;
    public float fallLookDown = 1.5f;
    public float verticalVelocityForFullLook = 10f;
    public float verticalLookSmoothTime = 0.2f;

    [Header("Movement Anticipation")]
    public float anticipationDistance = 0.35f;
    public float anticipationSmoothTime = 0.3f;

    [Header("Fall Camera Impact")]
    public float minimumImpactDip = 0.04f;
    public float maximumImpactDip = 0.17f;
    public float impactRecoveryTime = 0.3f;

    [Header("Fall Screen Shake")]
    public float minimumShakeMagnitude = 0.018f;
    public float maximumShakeMagnitude = 0.065f;
    public float minimumShakeDuration = 0.10f;
    public float maximumShakeDuration = 0.22f;
    public float shakeFrequency = 12f;

    [Range(0f, 1f)]
    public float rotationShake = 0f;

    [Header("Speed Zoom")]
    public bool enableSpeedZoom = false;
    public float speedZoomAmount = 0.25f;
    public float speedForMaximumZoom = 12f;
    public float zoomSmoothTime = 0.3f;

    [Header("Camera Bounds")]
    public bool useCameraBounds = false;
    public BoxCollider2D cameraBounds;

    private Camera attachedCamera;

    private Vector3 followVelocity;
    private Vector3 currentLookAhead;
    private Vector3 lookAheadVelocity;

    private float currentVerticalLook;
    private float verticalLookVelocity;
    private float currentAnticipation;
    private float anticipationVelocity;

    private Vector2 filteredTargetVelocity;
    private Vector2 velocityFilterDamp;

    private int horizontalLeadDirection;

    private Vector2 previousTargetPosition;
    private bool hasPreviousTargetPosition;

    private float baseZ;
    private Quaternion baseRotation;
    private float baseOrthographicSize;
    private float zoomVelocity;

    private float currentImpactDip;
    private float impactDipVelocity;

    private float shakeTimeRemaining;
    private float shakeDurationTotal;
    private float currentShakeMagnitude;
    private float shakeTimer;
    private float shakeSeed;

    private void Awake()
    {
        attachedCamera = GetComponent<Camera>();

        baseZ = transform.position.z;
        baseRotation = transform.rotation;
        baseOrthographicSize = attachedCamera.orthographicSize;

        shakeSeed = Random.Range(0f, 1000f);

        if (!attachedCamera.orthographic)
        {
            Debug.LogWarning(
                "CameraFollow is designed for an Orthographic camera.",
                this
            );
        }
    }

    private void Start()
    {
        if (target != null)
            SetTarget(target);
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        float dt = Time.deltaTime;

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

        UpdateHorizontalLookAhead(filteredTargetVelocity.x, dt);
        UpdateVerticalLook(filteredTargetVelocity.y, dt);
        UpdateImpactDip(dt);
        UpdateZoom(dt);
        UpdateShake(dt);

        Vector3 desiredPosition = target.position + offset;

        desiredPosition.x += currentLookAhead.x + currentAnticipation;
        desiredPosition.y += currentVerticalLook + currentImpactDip;
        desiredPosition.z = baseZ;

        Vector3 smoothedPosition = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref followVelocity,
            Mathf.Max(0.01f, smoothTime),
            Mathf.Max(0.1f, maxFollowSpeed),
            dt
        );

        if (useCameraBounds &&
            cameraBounds != null &&
            attachedCamera.orthographic)
        {
            smoothedPosition = ClampToCameraBounds(smoothedPosition);
        }

        smoothedPosition += GetShakeOffset();

        transform.position = smoothedPosition;

        transform.rotation = baseRotation * Quaternion.Euler(
            0f,
            0f,
            GetShakeRotation()
        );
    }

    private Vector2 GetTargetVelocity(float dt)
    {
        if (targetRb != null)
        {
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

        Vector2 velocity =
            (currentPosition - previousTargetPosition) /
            Mathf.Max(dt, 0.0001f);

        previousTargetPosition = currentPosition;

        return velocity;
    }

    private void UpdateHorizontalLookAhead(float velocity, float dt)
    {
        float speed = Mathf.Abs(velocity);

        float startSpeed = Mathf.Max(
            lookAheadStartSpeed,
            lookAheadDeadZone
        );

        int direction = speed > 0.001f
            ? (velocity > 0f ? 1 : -1)
            : 0;

        if (horizontalLeadDirection == 0)
        {
            if (speed >= startSpeed)
                horizontalLeadDirection = direction;
        }
        else
        {
            if (direction == -horizontalLeadDirection &&
                speed >= Mathf.Max(startSpeed, directionSwitchSpeed))
            {
                horizontalLeadDirection = direction;
            }
            else if (speed <= directionReleaseSpeed)
            {
                horizontalLeadDirection = 0;
            }
        }

        float strength = horizontalLeadDirection == 0
            ? 0f
            : Mathf.Clamp01(
                speed /
                Mathf.Max(startSpeed + 0.01f, velocityForFullLookAhead)
            );

        float targetLook =
            horizontalLeadDirection * lookAheadDistance * strength;

        float targetAnticipation =
            horizontalLeadDirection * anticipationDistance * strength;

        currentLookAhead.x = Mathf.SmoothDamp(
            currentLookAhead.x,
            targetLook,
            ref lookAheadVelocity.x,
            Mathf.Max(0.01f, lookAheadSmoothTime),
            Mathf.Infinity,
            dt
        );

        currentLookAhead.y = 0f;
        currentLookAhead.z = 0f;

        currentAnticipation = Mathf.SmoothDamp(
            currentAnticipation,
            targetAnticipation,
            ref anticipationVelocity,
            Mathf.Max(0.01f, anticipationSmoothTime),
            Mathf.Infinity,
            dt
        );
    }

    private void UpdateVerticalLook(float velocity, float dt)
    {
        float strength = Mathf.Clamp01(
            Mathf.Abs(velocity) /
            Mathf.Max(0.01f, verticalVelocityForFullLook)
        );

        float targetLook = 0f;

        if (velocity > 0.5f)
            targetLook = jumpLookUp * strength;
        else if (velocity < -0.5f)
            targetLook = -fallLookDown * strength;

        currentVerticalLook = Mathf.SmoothDamp(
            currentVerticalLook,
            targetLook,
            ref verticalLookVelocity,
            Mathf.Max(0.01f, verticalLookSmoothTime),
            Mathf.Infinity,
            dt
        );
    }

    // --------------------------------------------------
    // FALL IMPACT
    // Called by PlayerGameJuice only after a confirmed fall.
    // --------------------------------------------------

    public void TriggerFallImpact(float fallDistance, float impactSpeed)
    {
        float distanceStrength = Mathf.InverseLerp(
            1.5f,
            8f,
            fallDistance
        );

        float speedStrength = Mathf.InverseLerp(
            5f,
            16f,
            impactSpeed
        );

        float strength = Mathf.Clamp01(
            Mathf.Max(distanceStrength, speedStrength * 0.75f)
        );

        float dip = Mathf.Lerp(
            minimumImpactDip,
            maximumImpactDip,
            strength
        );

        currentImpactDip = -dip;
        impactDipVelocity = 0f;

        float magnitude = Mathf.Lerp(
            minimumShakeMagnitude,
            maximumShakeMagnitude,
            strength
        );

        float duration = Mathf.Lerp(
            minimumShakeDuration,
            maximumShakeDuration,
            strength
        );

        ShakeCamera(duration, magnitude);
    }

    // Compatibility method for other scripts.
    public void TriggerLandingImpact(float fallSpeed)
    {
        TriggerFallImpact(fallSpeed, fallSpeed);
    }

    private void UpdateImpactDip(float dt)
    {
        currentImpactDip = Mathf.SmoothDamp(
            currentImpactDip,
            0f,
            ref impactDipVelocity,
            Mathf.Max(0.01f, impactRecoveryTime),
            Mathf.Infinity,
            dt
        );
    }

    // --------------------------------------------------
    // SPEED ZOOM
    // --------------------------------------------------

    private void UpdateZoom(float dt)
    {
        if (!attachedCamera.orthographic)
            return;

        float desiredZoom = baseOrthographicSize;

        if (enableSpeedZoom)
        {
            float speed = filteredTargetVelocity.magnitude;

            float strength = Mathf.Clamp01(
                speed / Mathf.Max(0.1f, speedForMaximumZoom)
            );

            desiredZoom += speedZoomAmount * strength;
        }

        attachedCamera.orthographicSize = Mathf.SmoothDamp(
            attachedCamera.orthographicSize,
            desiredZoom,
            ref zoomVelocity,
            Mathf.Max(0.01f, zoomSmoothTime),
            Mathf.Infinity,
            dt
        );
    }

    // --------------------------------------------------
    // CAMERA BOUNDS
    // --------------------------------------------------

    private Vector3 ClampToCameraBounds(Vector3 position)
    {
        Bounds bounds = cameraBounds.bounds;

        float halfHeight = attachedCamera.orthographicSize;
        float halfWidth = halfHeight * attachedCamera.aspect;

        float minX = bounds.min.x + halfWidth;
        float maxX = bounds.max.x - halfWidth;

        float minY = bounds.min.y + halfHeight;
        float maxY = bounds.max.y - halfHeight;

        position.x = minX > maxX
            ? bounds.center.x
            : Mathf.Clamp(position.x, minX, maxX);

        position.y = minY > maxY
            ? bounds.center.y
            : Mathf.Clamp(position.y, minY, maxY);

        return position;
    }

    // --------------------------------------------------
    // SCREEN SHAKE
    // --------------------------------------------------

    private void ShakeCamera(float duration, float magnitude)
    {
        if (duration <= 0f || magnitude <= 0f)
            return;

        if (shakeTimeRemaining <= 0f)
        {
            shakeSeed = Random.Range(0f, 1000f);
            shakeTimer = 0f;

            currentShakeMagnitude = magnitude;
            shakeDurationTotal = duration;
            shakeTimeRemaining = duration;
        }
        else
        {
            currentShakeMagnitude = Mathf.Max(
                currentShakeMagnitude,
                magnitude
            );

            shakeTimeRemaining = Mathf.Max(
                shakeTimeRemaining,
                duration
            );

            shakeDurationTotal = Mathf.Max(
                shakeDurationTotal,
                duration
            );
        }
    }

    private void UpdateShake(float dt)
    {
        if (shakeTimeRemaining > 0f)
            shakeTimeRemaining -= dt;

        if (shakeTimeRemaining <= 0f)
        {
            shakeTimeRemaining = 0f;
            currentShakeMagnitude = 0f;
        }

        shakeTimer += dt;
    }

    private float GetShakeEnvelope()
    {
        if (shakeTimeRemaining <= 0f || shakeDurationTotal <= 0f)
            return 0f;

        float remaining = Mathf.Clamp01(
            shakeTimeRemaining / shakeDurationTotal
        );

        return Mathf.SmoothStep(0f, 1f, remaining);
    }

    private Vector3 GetShakeOffset()
    {
        float envelope = GetShakeEnvelope();

        if (envelope <= 0f || currentShakeMagnitude <= 0f)
            return Vector3.zero;

        float time = shakeTimer * shakeFrequency;

        float x =
            (Mathf.PerlinNoise(shakeSeed, time) - 0.5f) * 2f;

        float y =
            (Mathf.PerlinNoise(shakeSeed + 50f, time) - 0.5f) * 2f;

        return new Vector3(x, y, 0f) *
            currentShakeMagnitude *
            envelope;
    }

    private float GetShakeRotation()
    {
        if (rotationShake <= 0f)
            return 0f;

        float envelope = GetShakeEnvelope();

        float noise = (
            Mathf.PerlinNoise(
                shakeSeed + 100f,
                shakeTimer * shakeFrequency
            ) - 0.5f
        ) * 2f;

        return noise * rotationShake * envelope;
    }

    // --------------------------------------------------
    // TARGET
    // --------------------------------------------------

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

        currentLookAhead = Vector3.zero;
        currentAnticipation = 0f;
        currentVerticalLook = 0f;

        horizontalLeadDirection = 0;

        hasPreviousTargetPosition = target != null;

        if (target != null)
        {
            previousTargetPosition = target.position;

            filteredTargetVelocity = targetRb != null
                ? targetRb.linearVelocity
                : Vector2.zero;
        }
        else
        {
            filteredTargetVelocity = Vector2.zero;
        }
    }

    private void OnDisable()
    {
        transform.rotation = baseRotation;

        if (attachedCamera != null && attachedCamera.orthographic)
            attachedCamera.orthographicSize = baseOrthographicSize;
    }
}
