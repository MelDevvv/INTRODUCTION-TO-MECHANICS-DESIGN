
using UnityEngine;
using System.Collections.Generic;

public partial class PlayerController
{
    [Header("Start Preview Animation")]
    [SerializeField, Min(0f)]
    private float startPreviewSpinSpeed = 150f;

    [SerializeField, Min(0f)]
    private float startPreviewFloatHeight = 0.12f;

    [SerializeField, Min(0f)]
    private float startPreviewGroundOffset = 0.8f;

    [SerializeField, Min(0.1f)]
    private float startPreviewFloatFrequency = 0.7f;

    private bool startPreviewActive;
    private float startPreviewTime;
    private Vector3 startPreviewBaseLocalPosition;
    private bool startPreviewPositionCached;

    [Header("Start Release Alignment")]
    [SerializeField, Min(0.01f)]
    private float startReleaseAlignmentSmoothTime = 0.18f;

    private bool startReleaseAlignmentActive;
    private float startReleaseAlignmentTargetAngle;
    private float startReleaseAlignmentVelocity;

    // Preview physics state
    private bool previewGravityCached;
    private float originalPreviewGravityScale;

    // ==================================================
    // START PREVIEW
    // ==================================================

    public void SetStartPreviewActive(bool active)
    {
        if (active)
        {
            if (startPreviewActive)
                return;

            startPreviewActive = true;
            startPreviewTime = 0f;
            startPreviewPositionCached = false;
            startReleaseAlignmentActive = false;

            // Hold the actual player body in the air during the preview.
            if (rb != null)
            {
                if (!previewGravityCached)
                {
                    originalPreviewGravityScale = rb.gravityScale;
                    previewGravityCached = true;
                }

                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.gravityScale = 0f;

                // Raise the Rigidbody and its collider together.
                rb.position += Vector2.up * startPreviewGroundOffset;
            }

            if (visualRoot != null)
            {
                CacheStartPreviewPosition();

                spinAngle = visualRoot.localEulerAngles.z;
                spinAngleInitialized = true;
            }

            spinDirection = 1;
            spinVelocity = 0f;

            spinAlignmentActive = false;
            spinAlignmentComplete = false;
            landingAlignmentActive = false;
            landingAlignmentVelocity = 0f;
        }
        else
        {
            if (!startPreviewActive)
                return;

            startPreviewActive = false;

            // Restore gravity so the player genuinely falls onto the ground.
            if (rb != null && previewGravityCached)
            {
                rb.gravityScale = originalPreviewGravityScale;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                rb.angularVelocity = 0f;

                previewGravityCached = false;
            }

            if (visualRoot != null)
            {
                if (startPreviewPositionCached)
                    visualRoot.localPosition = startPreviewBaseLocalPosition;

                spinAngle = visualRoot.localEulerAngles.z;
                spinAngleInitialized = true;

                // Smoothly straighten the cube as it falls.
                startReleaseAlignmentTargetAngle =
                    Mathf.Round(spinAngle / 90f) * 90f;

                startReleaseAlignmentVelocity = 0f;
                startReleaseAlignmentActive = true;
            }

            startPreviewPositionCached = false;
            spinVelocity = 0f;

            spinAlignmentActive = false;
            spinAlignmentComplete = false;
            landingAlignmentActive = false;
            landingAlignmentVelocity = 0f;
        }
    }

    private void CacheStartPreviewPosition()
    {
        if (visualRoot == null || startPreviewPositionCached)
            return;

        startPreviewBaseLocalPosition = visualRoot.localPosition;
        startPreviewPositionCached = true;
    }

    private void UpdateStartPreview(float deltaTime)
    {
        if (visualRoot == null)
            return;

        CacheStartPreviewPosition();

        startPreviewTime += deltaTime;

        // Float the visual around its original local position.
        Vector3 visualPosition = startPreviewBaseLocalPosition;

        visualPosition.y += Mathf.Sin(
            startPreviewTime *
            Mathf.PI * 2f *
            startPreviewFloatFrequency
        ) * startPreviewFloatHeight;

        visualRoot.localPosition = visualPosition;

        // Continuously rotate during the start preview.
        spinAngle += spinDirection * startPreviewSpinSpeed * deltaTime;

        visualRoot.localRotation = Quaternion.Euler(
            0f,
            0f,
            spinAngle
        );
    }

    // ==================================================
    // JUMP SPIN
    // ==================================================

    private int GetPreferredSpinDirection()
    {
        if (moveInput < -0.1f)
            return 1;

        if (moveInput > 0.1f)
            return -1;

        if (rb != null)
        {
            if (rb.linearVelocity.x < -0.15f)
                return 1;

            if (rb.linearVelocity.x > 0.15f)
                return -1;
        }

        if (Mathf.Abs(spinVelocity) > 5f)
            return spinVelocity > 0f ? 1 : -1;

        return spinDirection;
    }

    private void BeginJumpSpin(float force)
    {
        spinDirection = GetPreferredSpinDirection();

        spinVelocity = spinDirection *
            Mathf.Max(Mathf.Abs(spinVelocity), force);

        spinAlignmentActive = false;
        spinAlignmentComplete = false;
        landingAlignmentActive = false;
    }

    private void BoostDoubleJumpSpin()
    {
        spinVelocity = spinDirection *
            Mathf.Max(Mathf.Abs(spinVelocity), doubleJumpSpinForce);

        spinAlignmentActive = false;
        spinAlignmentComplete = false;
        landingAlignmentActive = false;
    }

    // ==================================================
    // SPIN ALIGNMENT
    // ==================================================

    private float GetNextFlatAngle(float angle, int direction)
    {
        if (direction >= 0)
            return (Mathf.Floor(angle / 90f) + 1f) * 90f;

        return (Mathf.Ceil(angle / 90f) - 1f) * 90f;
    }

    private bool IsGroundWithinAlignmentDistance()
    {
        if (playerCollider == null || runtimeGroundMask == 0)
            return false;

        Bounds bounds = playerCollider.bounds;

        Vector2 castSize = new Vector2(
            Mathf.Max(0.01f, bounds.size.x * 0.90f),
            Mathf.Max(0.01f, bounds.size.y * 0.90f)
        );

        ContactFilter2D filter = new ContactFilter2D();

        filter.SetLayerMask(runtimeGroundMask);
        filter.useTriggers = false;

        assistCastHits.Clear();

        Physics2D.BoxCast(
            bounds.center,
            castSize,
            0f,
            Vector2.down,
            filter,
            assistCastHits,
            fallAlignmentStartDistance
        );

        for (int i = 0; i < assistCastHits.Count; i++)
        {
            Collider2D hit = assistCastHits[i].collider;

            if (hit == null ||
                hit == playerCollider ||
                hit.isTrigger ||
                hit.attachedRigidbody == rb)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void BeginFallAlignment()
    {
        float speed = Mathf.Abs(spinVelocity);

        float stoppingAngle =
            (speed * speed) /
            (2f * Mathf.Max(100f, fallAlignmentDeceleration));

        float target = GetNextFlatAngle(
            spinAngle,
            spinDirection
        );

        float remaining =
            spinDirection * (target - spinAngle);

        while (remaining < stoppingAngle + 0.5f)
        {
            target += spinDirection * 90f;

            remaining =
                spinDirection * (target - spinAngle);
        }

        spinAlignmentTargetAngle = target;
        spinAlignmentActive = true;
    }

    // ==================================================
    // MAIN SPIN UPDATE
    // ==================================================

    private void HandleSpin()
    {
        if (visualRoot == null)
            return;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        if (!spinAngleInitialized)
        {
            spinAngle = visualRoot.localEulerAngles.z;
            spinAngleInitialized = true;
        }

        // Start preview has priority over normal spin.
        if (startPreviewActive)
        {
            UpdateStartPreview(dt);
            return;
        }

        // Smoothly straighten after the player starts the level.
        if (startReleaseAlignmentActive)
        {
            spinAngle = Mathf.SmoothDamp(
                spinAngle,
                startReleaseAlignmentTargetAngle,
                ref startReleaseAlignmentVelocity,
                startReleaseAlignmentSmoothTime,
                Mathf.Infinity,
                dt
            );

            if (Mathf.Abs(
                spinAngle - startReleaseAlignmentTargetAngle
            ) <= 0.15f)
            {
                spinAngle = startReleaseAlignmentTargetAngle;
                startReleaseAlignmentActive = false;
                startReleaseAlignmentVelocity = 0f;
            }

            visualRoot.localRotation =
                Quaternion.Euler(0f, 0f, spinAngle);

            return;
        }

        // Grounded landing alignment.
        if (isGrounded)
        {
            if (landingAlignmentActive)
            {
                spinAngle = Mathf.SmoothDamp(
                    spinAngle,
                    landingAlignmentTargetAngle,
                    ref landingAlignmentVelocity,
                    landingAlignmentSmoothTime,
                    Mathf.Infinity,
                    dt
                );

                if (Mathf.Abs(
                    spinAngle - landingAlignmentTargetAngle
                ) <= 0.15f)
                {
                    spinAngle = landingAlignmentTargetAngle;
                    landingAlignmentActive = false;
                    landingAlignmentVelocity = 0f;
                }

                visualRoot.localRotation =
                    Quaternion.Euler(0f, 0f, spinAngle);
            }

            return;
        }

        if (rb == null)
            return;

        bool falling = rb.linearVelocity.y < -0.05f;

        if (!falling)
        {
            spinAlignmentActive = false;
            spinAlignmentComplete = false;

            spinVelocity *= Mathf.Exp(
                -Mathf.Max(0f, spinDrag) * dt
            );

            spinAngle += spinVelocity * dt;

            visualRoot.localRotation =
                Quaternion.Euler(0f, 0f, spinAngle);

            return;
        }

        // Smooth falling spin.
        if (!spinAlignmentActive && !spinAlignmentComplete)
        {
            float targetVelocity =
                spinDirection * fallingSpinSpeed;

            spinVelocity = Mathf.MoveTowards(
                spinVelocity,
                targetVelocity,
                fallingSpinAcceleration * dt
            );
        }

        if (!spinAlignmentActive &&
            !spinAlignmentComplete &&
            IsGroundWithinAlignmentDistance())
        {
            BeginFallAlignment();
        }

        if (spinAlignmentActive)
        {
            float remaining = spinDirection *
                (spinAlignmentTargetAngle - spinAngle);

            if (remaining <= 0.15f)
            {
                spinAngle = spinAlignmentTargetAngle;
                spinVelocity = 0f;

                spinAlignmentActive = false;
                spinAlignmentComplete = true;
            }
            else
            {
                float brakingSpeed = Mathf.Sqrt(
                    2f * fallAlignmentDeceleration * remaining
                );

                float currentSpeed = Mathf.Abs(spinVelocity);

                float allowedSpeed = Mathf.Min(
                    currentSpeed,
                    brakingSpeed
                );

                float newSpeed = Mathf.MoveTowards(
                    currentSpeed,
                    allowedSpeed,
                    fallAlignmentDeceleration * dt
                );

                spinVelocity = spinDirection * newSpeed;

                float angularStep = spinVelocity * dt;

                if (Mathf.Abs(angularStep) >= remaining)
                {
                    spinAngle = spinAlignmentTargetAngle;
                    spinVelocity = 0f;

                    spinAlignmentActive = false;
                    spinAlignmentComplete = true;
                }
                else
                {
                    spinAngle += angularStep;
                }
            }
        }
        else if (!spinAlignmentComplete)
        {
            spinAngle += spinVelocity * dt;
        }

        visualRoot.localRotation =
            Quaternion.Euler(0f, 0f, spinAngle);
    }
}
