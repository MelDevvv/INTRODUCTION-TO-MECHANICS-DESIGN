using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public partial class PlayerController
{
    private bool QueryGround()
    {
        if (playerCollider == null || rb == null || runtimeGroundMask == 0)
            return false;

        // While the small step-up assist is gradually raising the player over
        // a low bump, keep the controller grounded so it doesn't trigger coyote
        // time, falling spin, or airborne movement during the step.
        if (stepUpActive)
            return true;

        // Never count the player as grounded while clearly rising.
        if (rb.linearVelocity.y > 0.5f)
            return false;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(runtimeGroundMask);
        filter.useTriggers = false;

        // Cast the real solid player collider down a very short distance.
        // Only upward-facing hit normals count as ground. This prevents the
        // side of a lower stair/platform from keeping isGrounded true and
        // suppressing the falling-spin transition.
        assistCastHits.Clear();
        playerCollider.Cast(
            Vector2.down,
            filter,
            assistCastHits,
            Mathf.Max(0.01f, groundProbeDistance));

        for (int i = 0; i < assistCastHits.Count; i++)
        {
            RaycastHit2D hit = assistCastHits[i];
            Collider2D other = hit.collider;

            if (other == null || other == playerCollider || other.isTrigger ||
                other.attachedRigidbody == rb)
                continue;

            // A walkable surface must face upwards. Ignore vertical tile sides.
            if (hit.normal.y >= 0.55f)
                return true;
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

        if (!isGrounded)
            hasBeenAirborne = true;

        if (isGrounded && !wasGrounded)
        {
            RegisterLanding();
        }
        else if (wasGrounded && !isGrounded && !playerJumpInProgress &&
                 rb.linearVelocity.y <= 0.05f)
        {
            // Walking off an edge is NOT a jump. Keep coyote time available for
            // a short grace-period normal jump, but do not grant a double jump
            // several seconds later when the grace period has expired.
            hasUsedDoubleJump = true;

            // Walking off an edge: pick the spin direction from travel, then
            // let HandleSpin ease into the falling spin rather than starting abruptly.
            spinDirection = GetPreferredSpinDirection();
            hasBeenAirborne = true;
            spinAlignmentActive = false;
            spinAlignmentComplete = false;
            landingAlignmentActive = false;
        }
    }

    private void RegisterLanding()
    {
        hasUsedDoubleJump = false;

        landingMomentumX = rb.linearVelocity.x;
        stickyLandingCounter = stickyLandingDuration;
        playerJumpInProgress = false;

        if (visualRoot != null)
        {
            // Keep the side selected during the final descent. If the ground
            // probe detects contact before predictive alignment finishes, use
            // the same directional target and ease into it (never snap).
            if (spinAlignmentActive || spinAlignmentComplete)
            {
                landingAlignmentTargetAngle = spinAlignmentTargetAngle;
            }
            else if (hasBeenAirborne)
            {
                float nearestFlat = Mathf.Round(spinAngle / 90f) * 90f;
                landingAlignmentTargetAngle =
                    Mathf.Abs(nearestFlat - spinAngle) <= 2f
                        ? nearestFlat
                        : GetNextFlatAngle(spinAngle, spinDirection);
            }
            else
            {
                // Initial scene grounding should not rotate the idle player.
                landingAlignmentTargetAngle = Mathf.Round(spinAngle / 90f) * 90f;
            }

            landingAlignmentActive = Mathf.Abs(spinAngle - landingAlignmentTargetAngle) > 0.15f;
            landingAlignmentVelocity = 0f;

            if (!landingAlignmentActive)
            {
                spinAngle = landingAlignmentTargetAngle;
                visualRoot.localRotation = Quaternion.Euler(0f, 0f, spinAngle);
            }
        }

        spinVelocity = 0f;
        spinAlignmentActive = false;
        spinAlignmentComplete = false;
        hasBeenAirborne = false;
    }

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
                BeginJumpSpin(jumpSpinForce);

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
                BoostDoubleJumpSpin();

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
        hasBeenAirborne = true;
        jumpCutApplied = false;
    }

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

    private void ApplyApexAndFallSpeed()
    {
        Vector2 velocity = rb.linearVelocity;

        // Gently reduce vertical speed around the apex without adding upward energy
        // or pinning the player at the apex.
        if (!isGrounded && Mathf.Abs(velocity.y) < apexVelocityThreshold)
        {
            velocity.y = Mathf.MoveTowards(
                velocity.y, 0f, apexSlowdownAcceleration * Time.deltaTime);
            apexAssistLogged = true;
        }

        if (velocity.y < -maximumFallSpeed)
        {
            velocity.y = -maximumFallSpeed;
            fallCapLogged = true;
        }

        rb.linearVelocity = velocity;
    }
}