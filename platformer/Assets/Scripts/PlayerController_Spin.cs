using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public partial class PlayerController
{
    private int GetPreferredSpinDirection()
    {
        // In Unity, positive Z is counter-clockwise. Left = CCW; right = CW.
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
        spinVelocity = spinDirection * Mathf.Max(Mathf.Abs(spinVelocity), force);
        spinAlignmentActive = false;
        spinAlignmentComplete = false;
        landingAlignmentActive = false;
    }

    private void BoostDoubleJumpSpin()
    {
        // Keep the same spin direction for the whole airborne sequence so a
        // mid-air input change never abruptly reverses the square's rotation.
        spinVelocity = spinDirection * Mathf.Max(Mathf.Abs(spinVelocity), doubleJumpSpinForce);
        spinAlignmentActive = false;
        spinAlignmentComplete = false;
        landingAlignmentActive = false;
    }

    private float GetNextFlatAngle(float angle, int direction)
    {
        // Return the next multiple of 90 degrees in the existing spin direction.
        if (direction >= 0)
            return (Mathf.Floor(angle / 90f) + 1f) * 90f;

        return (Mathf.Ceil(angle / 90f) - 1f) * 90f;
    }

    private bool IsGroundWithinAlignmentDistance(float probeDistance)
    {
        if (playerCollider == null || runtimeGroundMask == 0)
            return false;

        Bounds bounds = playerCollider.bounds;
        Vector2 castSize = new Vector2(
            Mathf.Max(0.01f, bounds.size.x * 0.90f),
            Mathf.Max(0.01f, bounds.size.y * 0.90f));

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(runtimeGroundMask);
        filter.useTriggers = false;
        assistCastHits.Clear();
        Physics2D.BoxCast(
            bounds.center, castSize, 0f, Vector2.down,
            filter, assistCastHits, probeDistance);

        for (int i = 0; i < assistCastHits.Count; i++)
        {
            Collider2D hit = assistCastHits[i].collider;
            if (hit == null || hit == playerCollider || hit.isTrigger ||
                hit.attachedRigidbody == rb)
                continue;

            // Only a walkable top surface should trigger landing alignment.
            // Side hits on stair blocks must not cause premature spin braking.
            if (assistCastHits[i].normal.y >= 0.55f)
                return true;
        }

        return false;
    }

    private void BeginFallAlignment()
    {
        // Select a flat side far enough ahead to let the current rotation
        // brake naturally. We never choose a target behind the current spin.
        // Use the intended fall-spin speed as well as the current speed. This
        // avoids selecting a target too close just because spin is still
        // accelerating immediately after leaving a ledge.
        float speed = Mathf.Max(Mathf.Abs(spinVelocity), fallingSpinSpeed);
        float stoppingAngle = (speed * speed) /
                              (2f * Mathf.Max(100f, fallAlignmentDeceleration));
        float target = GetNextFlatAngle(spinAngle, spinDirection);
        float remaining = spinDirection * (target - spinAngle);

        while (remaining < stoppingAngle + 0.5f)
        {
            target += spinDirection * 90f;
            remaining = spinDirection * (target - spinAngle);
        }

        spinAlignmentTargetAngle = target;
        spinAlignmentActive = true;
    }

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

        if (isGrounded)
        {
            if (landingAlignmentActive)
            {
                // Always correct toward the nearest flat orientation, not the
                // next 90-degree orientation in the spin direction. This avoids
                // an unnecessary quarter-turn after touching the floor.
                float nearestFlatAngle = Mathf.Round(spinAngle / 90f) * 90f;
                float angleError = nearestFlatAngle - spinAngle;

                // Ignore only imperceptibly small errors to prevent micro-jitter.
                // A visible tilt (for example 10-20 degrees) is still corrected.
                const float landingAngleDeadZone = 1.25f;
                if (Mathf.Abs(angleError) <= landingAngleDeadZone)
                {
                    // Settle this tiny remainder without playing a visible
                    // corrective spin on the floor.
                    spinAngle = nearestFlatAngle;
                    landingAlignmentActive = false;
                    landingAlignmentVelocity = 0f;
                }
                else
                {
                    landingAlignmentTargetAngle = nearestFlatAngle;
                    spinAngle = Mathf.SmoothDamp(
                        spinAngle,
                        landingAlignmentTargetAngle,
                        ref landingAlignmentVelocity,
                        Mathf.Max(0.08f, landingAlignmentSmoothTime),
                        Mathf.Infinity,
                        dt);

                    if (Mathf.Abs(spinAngle - landingAlignmentTargetAngle) <= 0.2f)
                    {
                        spinAngle = landingAlignmentTargetAngle;
                        landingAlignmentActive = false;
                        landingAlignmentVelocity = 0f;
                    }
                }

                visualRoot.localRotation = Quaternion.Euler(0f, 0f, spinAngle);
            }

            return;
        }

        if (rb == null)
            return;

        bool falling = rb.linearVelocity.y < -0.05f;

        if (!falling)
        {
            // On the way up, preserve the original spin feel and drag.
            spinAlignmentActive = false;
            spinAlignmentComplete = false;
            spinVelocity *= Mathf.Exp(-Mathf.Max(0f, spinDrag) * dt);

            spinAngle += spinVelocity * dt;
            visualRoot.localRotation = Quaternion.Euler(0f, 0f, spinAngle);
            return;
        }

        // Falling always has a visible spin. Ease toward a steady fall-spin
        // speed, so the rotation neither suddenly starts nor dies at the apex.
        if (!spinAlignmentActive && !spinAlignmentComplete)
        {
            float targetVelocity = spinDirection * fallingSpinSpeed;
            spinVelocity = Mathf.MoveTowards(
                spinVelocity,
                targetVelocity,
                fallingSpinAcceleration * dt);
        }

        // Start alignment according to the current fall speed. A fixed large
        // detection distance made alignment begin too early on small steps,
        // which could leave the cube apparently frozen mid-fall. Faster falls
        // begin braking farther from the surface; slow stair drops wait longer.
        float maxProbeDistance = Mathf.Max(0.35f, fallAlignmentStartDistance);
        float speedBasedDistance = 0.25f + Mathf.Abs(rb.linearVelocity.y) * 0.12f;
        float alignmentProbeDistance = Mathf.Min(
            maxProbeDistance,
            Mathf.Max(0.35f, speedBasedDistance));

        if (!spinAlignmentActive && !spinAlignmentComplete &&
            IsGroundWithinAlignmentDistance(alignmentProbeDistance))
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
                // Braking-distance control: hold the current speed while there
                // is room, then ease the angular speed down as the target side
                // gets closer. Signed velocity never crosses through zero.
                float brakingSpeed = Mathf.Sqrt(
                    2f * fallAlignmentDeceleration * remaining);
                float currentSpeed = Mathf.Abs(spinVelocity);
                float allowedSpeed = Mathf.Min(currentSpeed, brakingSpeed);
                float newSpeed = Mathf.MoveTowards(
                    currentSpeed,
                    allowedSpeed,
                    fallAlignmentDeceleration * dt);

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
            // Normal free-fall spin: smooth constant-direction rotation.
            spinAngle += spinVelocity * dt;
        }

        visualRoot.localRotation = Quaternion.Euler(0f, 0f, spinAngle);
    }
}