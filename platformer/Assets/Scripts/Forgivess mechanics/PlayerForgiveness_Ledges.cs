using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public partial class PlayerController
{
    private void TryCatchMissedJump()
    {
        // A previous version changed rb.position directly, causing visible
        // horizontal snaps and breaking the cube's falling animation. This
        // version only nudges horizontal velocity, and only while descending.
        if (rb == null || playerCollider == null || isGrounded ||
            rb.linearVelocity.y >= -0.1f || runtimeGroundMask == 0 ||
            Time.time < nextMissedJumpAssistTime ||
            missedJumpForwardDistance <= 0f)
            return;

        float direction = Mathf.Abs(rb.linearVelocity.x) > 0.15f
            ? Mathf.Sign(rb.linearVelocity.x)
            : Mathf.Sign(moveInput);

        // Do not invent a correction direction when the player has no clear
        // horizontal travel direction.
        if (Mathf.Abs(direction) < 0.1f)
            return;

        Bounds bounds = playerCollider.bounds;
        Vector2 probeSize = new Vector2(
            Mathf.Max(0.04f, bounds.size.x * 0.72f),
            Mathf.Max(0.025f, bounds.size.y * 0.06f));
        float castDistance = Mathf.Max(0.08f, missedJumpLift + missedJumpDrop);
        Vector2 origin = new Vector2(
            bounds.center.x + direction * missedJumpForwardDistance,
            bounds.min.y + 0.025f);

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(runtimeGroundMask);
        filter.useTriggers = false;
        assistCastHits.Clear();
        Physics2D.BoxCast(
            origin, probeSize, 0f, Vector2.down,
            filter, assistCastHits, castDistance);

        bool foundWalkableGround = false;
        for (int i = 0; i < assistCastHits.Count; i++)
        {
            RaycastHit2D hit = assistCastHits[i];
            Collider2D other = hit.collider;
            if (other == null || other == playerCollider || other.isTrigger ||
                other.attachedRigidbody == rb)
                continue;

            // A platform side is not a landing surface.
            if (hit.normal.y >= 0.55f)
            {
                foundWalkableGround = true;
                break;
            }
        }

        if (!foundWalkableGround)
            return;

        // Steer toward the platform smoothly. This does not move rb.position,
        // change vertical velocity, or reset rotation/spin animation.
        float currentAlong = rb.linearVelocity.x * direction;
        float minAssistSpeed = Mathf.Max(0.5f, moveSpeed * 0.35f);
        float maxAssistSpeed = Mathf.Max(minAssistSpeed, moveSpeed * 1.10f);
        float targetAlong = Mathf.Clamp(
            Mathf.Max(currentAlong, minAssistSpeed) + 0.65f,
            minAssistSpeed,
            maxAssistSpeed);
        float acceleration = Mathf.Max(4f, moveSpeed * 2f);
        float newAlong = Mathf.MoveTowards(
            currentAlong, targetAlong, acceleration * Time.deltaTime);

        rb.linearVelocity = new Vector2(direction * newAlong, rb.linearVelocity.y);
        nextMissedJumpAssistTime = Time.time + missedJumpAssistCooldown;
    }

    private bool HasGroundAhead(float direction)
    {
        if (playerCollider == null || runtimeGroundMask == 0 || Mathf.Abs(direction) < 0.01f)
            return true;

        Bounds bounds = playerCollider.bounds;
        float probeX = bounds.center.x + Mathf.Sign(direction) *
                       (bounds.extents.x + ledgeLookAhead);
        float probeY = bounds.min.y + 0.03f;
        Vector2 probeSize = new Vector2(Mathf.Max(0.04f, bounds.size.x * 0.12f), 0.04f);
        float probeDepth = Mathf.Max(0.18f, ledgeLookAhead + 0.12f);

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(runtimeGroundMask);
        filter.useTriggers = false;
        assistCastHits.Clear();
        Physics2D.BoxCast(
            new Vector2(probeX, probeY), probeSize, 0f, Vector2.down,
            filter, assistCastHits, probeDepth);

        for (int i = 0; i < assistCastHits.Count; i++)
        {
            Collider2D hit = assistCastHits[i].collider;
            if (hit == null || hit == playerCollider || hit.isTrigger ||
                hit.attachedRigidbody == rb)
                continue;

            if (assistCastHits[i].normal.y >= 0.55f)
                return true;
        }

        return false;
    }
}
