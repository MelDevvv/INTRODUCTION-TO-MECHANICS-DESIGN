
using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public partial class PlayerController
{
    [Header("Jump Audio")]
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioSource jumpAudioSource;

    [Range(0f, 1f)]
    [SerializeField] private float jumpSoundVolume = 0.65f;

    private void PlayJumpSound()
    {
        if (jumpSound == null)
            return;

        if (jumpAudioSource == null)
        {
            jumpAudioSource = gameObject.AddComponent<AudioSource>();
            jumpAudioSource.playOnAwake = false;
            jumpAudioSource.loop = false;
            jumpAudioSource.spatialBlend = 0f;
        }

        jumpAudioSource.PlayOneShot(jumpSound, jumpSoundVolume);
    }

    private bool QueryGround()
    {
        if (playerCollider == null || rb == null || runtimeGroundMask == 0)
            return false;

        if (stepUpActive)
            return true;

        if (rb.linearVelocity.y > 0.5f)
            return false;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(runtimeGroundMask);
        filter.useTriggers = false;

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
            hasUsedDoubleJump = true;

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
                landingAlignmentTargetAngle = Mathf.Round(spinAngle / 90f) * 90f;
            }

            landingAlignmentActive =
                Mathf.Abs(spinAngle - landingAlignmentTargetAngle) > 0.15f;

            landingAlignmentVelocity = 0f;

            if (!landingAlignmentActive)
            {
                spinAngle = landingAlignmentTargetAngle;
                visualRoot.localRotation =
                    Quaternion.Euler(0f, 0f, spinAngle);
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

        // Plays for both normal jumps and double jumps.
        PlayJumpSound();
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
