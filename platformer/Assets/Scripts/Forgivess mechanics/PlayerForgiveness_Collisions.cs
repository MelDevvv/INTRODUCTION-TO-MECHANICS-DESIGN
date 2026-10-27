using UnityEngine;

public partial class PlayerController
{
    [Header("Automatic Small Step-Up Assistance")]
    [Tooltip("Lets the player walk over small raised tile edges without pressing jump.")]
    [SerializeField] private bool enableSmallStepUp = true;

    [Tooltip("Maximum height, in world units, that can be stepped up automatically. Start around 0.25 for small tile bumps.")]
    [SerializeField, Range(0.05f, 0.50f)] private float maximumStepUpHeight = 0.28f;

    [Tooltip("How quickly the player rises while stepping up. This is not a jump impulse.")]
    [SerializeField, Range(0.5f, 8f)] private float stepUpSpeed = 4f;

    [Tooltip("Horizontal distance used to detect a small wall directly ahead.")]
    [SerializeField, Range(0.03f, 0.20f)] private float stepUpProbeDistance = 0.10f;

    [Tooltip("Small clearance above the obstacle top to prevent snagging on its edge.")]
    [SerializeField, Range(0.002f, 0.04f)] private float stepUpClearance = 0.012f;

    [Tooltip("Minimum delay before another step correction can begin.")]
    [SerializeField, Range(0.01f, 0.20f)] private float stepUpCooldown = 0.06f;

    private bool stepUpActive;
    private float stepUpTargetY;
    private float nextStepUpTime;
    [Header("Head and Corner Assistance - Stability")]
    [Tooltip("Maximum safe horizontal displacement used to free the player from an upper corner.")]
    [SerializeField, Range(0.02f, 0.25f)]
    private float cornerNudgeDistance = 0.14f;

    [Tooltip("Smallest step used when searching for clear space away from a corner.")]
    [SerializeField, Range(0.005f, 0.04f)]
    private float cornerNudgeStep = 0.01f;

    [Tooltip("Prevents repeated corner nudges from jittering while contact persists.")]
    [SerializeField, Range(0.03f, 0.25f)]
    private float cornerNudgeCooldown = 0.10f;

    [Tooltip("Minimum upward cast distance used to detect a ceiling before the player penetrates it.")]
    [SerializeField, Range(0.01f, 0.30f)]
    private float headBumpProbeDistance = 0.055f;

    [Header("Editable Head-Bump Probe")]
    [Tooltip("Draw the head-bump probe and its cast path in the Scene view.")]
    [SerializeField] private bool showHeadBumpProbeGizmo = true;

    [Tooltip("Probe width as a fraction of the player's main collider width. Reduce it to avoid catching side corners; increase it for wider ceiling detection.")]
    [SerializeField, Range(0.15f, 1.10f)]
    private float headBumpProbeWidthMultiplier = 0.86f;

    [Tooltip("Probe box height in world units. Increase slightly if very thin ceilings are missed.")]
    [SerializeField, Range(0.01f, 0.20f)]
    private float headBumpProbeThickness = 0.04f;

    [Tooltip("Move the probe relative to the top-centre of the player's main collider. X moves left/right; Y moves up/down. Values are in world units.")]
    [SerializeField] private Vector2 headBumpProbeOffset = new Vector2(0f, 0.005f);

    private float nextCornerNudgeTime;

    [Header("Automatic Upper-Corner Sensors")]
    [Tooltip("Automatically creates two small trigger colliders at the upper-left and upper-right edges of the player's physics collider. They help identify corner catches without rotating or replacing the main collider.")]
    [SerializeField] private bool enableUpperCornerSensors = true;

    [Tooltip("How far the upper-corner trigger sensors can extend outside the main collider, in world units.")]
    [SerializeField, Range(0.02f, 0.15f)] private float upperCornerSensorProtrusion = 0.08f;

    [Tooltip("Maximum small upward correction when a rising jump catches on a block corner.")]
    [SerializeField, Range(0.02f, 0.16f)] private float risingCornerNudgeDistance = 0.10f;

    [Tooltip("Minimum time between automatic upper-corner nudges.")]
    [SerializeField, Range(0.03f, 0.20f)] private float risingCornerNudgeCooldown = 0.09f;

    private BoxCollider2D upperLeftCornerSensor;
    private BoxCollider2D upperRightCornerSensor;
    private float nextRisingCornerNudgeTime;
    private bool cornerSensorsInitialized;
    // Limit the rising corner correction to one attempt per airborne period.
    // Repeated Rigidbody position nudges while holding movement into a wall caused vibration.
    private bool risingCornerNudgeUsedThisAirborne;

    private void FixedUpdate()
    {
        if (rb != null && playerCollider != null && enableUpperCornerSensors)
        {
            EnsureUpperCornerSensors();
            UpdateUpperCornerSensorGeometry();
        }

        // Resolve true overhead hits before processing upper-side overlaps. This
        // prevents a ceiling from being misclassified as a side-corner nudge.
        CheckHeadBumpProbe();
        CheckUpperCornerSensorNudges();
        UpdateSmallStepUpAssist();
    }

    private void EnsureUpperCornerSensors()
    {
        if (cornerSensorsInitialized || rb == null || playerCollider == null)
            return;

        upperLeftCornerSensor = GetOrCreateUpperCornerSensor("__RuntimeUpperCornerSensor_Left");
        upperRightCornerSensor = GetOrCreateUpperCornerSensor("__RuntimeUpperCornerSensor_Right");
        cornerSensorsInitialized = upperLeftCornerSensor != null && upperRightCornerSensor != null;
    }

    private BoxCollider2D GetOrCreateUpperCornerSensor(string sensorName)
    {
        Transform existing = transform.Find(sensorName);
        GameObject sensorObject;
        if (existing != null)
        {
            sensorObject = existing.gameObject;
        }
        else
        {
            sensorObject = new GameObject(sensorName);
            sensorObject.layer = gameObject.layer;
            sensorObject.transform.SetParent(transform, false);
        }

        BoxCollider2D sensor = sensorObject.GetComponent<BoxCollider2D>();
        if (sensor == null)
            sensor = sensorObject.AddComponent<BoxCollider2D>();

        sensor.isTrigger = true;
        sensor.enabled = true;
        return sensor;
    }

    private void UpdateUpperCornerSensorGeometry()
    {
        if (upperLeftCornerSensor == null || upperRightCornerSensor == null || playerCollider == null)
            return;

        Bounds bounds = playerCollider.bounds;
        Vector3 lossyScale = transform.lossyScale;
        float scaleX = Mathf.Max(0.0001f, Mathf.Abs(lossyScale.x));
        float scaleY = Mathf.Max(0.0001f, Mathf.Abs(lossyScale.y));

        // Each sensor covers only the top portion of one side. It protrudes a
        // little past the solid collider so an upper-corner catch is detected
        // before the main box gets tightly pinned against the block.
        float sensorWorldWidth = Mathf.Min(bounds.size.x * 0.34f,
            Mathf.Max(0.06f, upperCornerSensorProtrusion * 2.2f));
        float sensorWorldHeight = Mathf.Max(0.05f, bounds.size.y * 0.32f);
        sensorWorldHeight = Mathf.Min(sensorWorldHeight, bounds.size.y * 0.45f);
        float y = bounds.max.y - sensorWorldHeight * 0.5f;
        float xInset = Mathf.Max(-sensorWorldWidth * 0.25f,
            sensorWorldWidth * 0.5f - upperCornerSensorProtrusion);

        SetUpperCornerSensor(upperLeftCornerSensor,
            new Vector3(bounds.min.x + xInset, y, bounds.center.z),
            new Vector2(sensorWorldWidth / scaleX, sensorWorldHeight / scaleY));
        SetUpperCornerSensor(upperRightCornerSensor,
            new Vector3(bounds.max.x - xInset, y, bounds.center.z),
            new Vector2(sensorWorldWidth / scaleX, sensorWorldHeight / scaleY));
    }

    private void SetUpperCornerSensor(BoxCollider2D sensor, Vector3 worldCenter, Vector2 localSize)
    {
        if (sensor == null)
            return;

        sensor.transform.localPosition = transform.InverseTransformPoint(worldCenter);
        sensor.transform.localRotation = Quaternion.identity;
        sensor.transform.localScale = Vector3.one;
        sensor.offset = Vector2.zero;
        sensor.size = localSize;
        sensor.isTrigger = true;
    }

    private bool SensorOverlapsSolidGround(Collider2D sensor)
    {
        if (sensor == null || runtimeGroundMask == 0)
            return false;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(runtimeGroundMask);
        filter.useTriggers = false;

        assistOverlapHits.Clear();
        sensor.Overlap(filter, assistOverlapHits);
        for (int i = 0; i < assistOverlapHits.Count; i++)
        {
            Collider2D other = assistOverlapHits[i];
            if (other == null || other.isTrigger || other == playerCollider ||
                other.attachedRigidbody == rb)
                continue;
            return true;
        }
        return false;
    }

    private void CheckUpperCornerSensorNudges()
    {
        if (!enableUpperCornerSensors || !enableCornerClip || !cornerSensorsInitialized ||
            rb == null || playerCollider == null || runtimeGroundMask == 0)
            return;

        // Reset the one-shot assist after landing. Repeated sideways Rigidbody
        // repositioning while the player held movement into a wall caused vibration.
        if (isGrounded)
        {
            risingCornerNudgeUsedThisAirborne = false;
            return;
        }

        if (rb.linearVelocity.y <= headBumpMinimumUpwardSpeed ||
            Time.time < nextRisingCornerNudgeTime ||
            risingCornerNudgeUsedThisAirborne)
            return;

        bool leftBlocked = SensorOverlapsSolidGround(upperLeftCornerSensor);
        bool rightBlocked = SensorOverlapsSolidGround(upperRightCornerSensor);

        if (!leftBlocked && !rightBlocked)
            return;

        // Do not classify overlapping corner sensors as a ceiling: on a tight
        // obstacle they can both touch the wall. The dedicated central head probe
        // has already checked for a real overhead surface earlier in FixedUpdate.
        // Instead, attempt one tiny, clearance-checked UPWARD nudge. This preserves
        // horizontal intent and upward momentum, helping the cube clear the lip.
        if (!TryRisingCornerNudge())
            return;

        risingCornerNudgeUsedThisAirborne = true;
        nextRisingCornerNudgeTime = Time.time + risingCornerNudgeCooldown;
        nextCornerNudgeTime = Mathf.Max(nextCornerNudgeTime, nextRisingCornerNudgeTime);
    }

    private bool TryRisingCornerNudge()
    {
        if (playerCollider == null || rb == null)
            return false;

        Bounds bounds = playerCollider.bounds;
        Vector2 testSize = new Vector2(
            Mathf.Max(0.02f, bounds.size.x * 0.94f),
            Mathf.Max(0.02f, bounds.size.y * 0.94f));

        // Keep this correction deliberately small. It is a collision-checked
        // clearance nudge, not an extra jump or teleport to the platform top.
        float maxDistance = Mathf.Min(risingCornerNudgeDistance, 0.12f);
        float step = Mathf.Max(0.005f, cornerNudgeStep);
        int steps = Mathf.CeilToInt(maxDistance / step);

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(runtimeGroundMask);
        filter.useTriggers = false;

        for (int i = 1; i <= steps; i++)
        {
            float distance = Mathf.Min(maxDistance, step * i);
            Vector2 offset = Vector2.up * distance;
            Vector2 candidateCenter = (Vector2)bounds.center + offset;

            assistOverlapHits.Clear();
            Physics2D.OverlapBox(candidateCenter, testSize, 0f, filter, assistOverlapHits);

            bool blocked = false;
            for (int j = 0; j < assistOverlapHits.Count; j++)
            {
                Collider2D other = assistOverlapHits[j];
                if (other == null || other.isTrigger || other == playerCollider ||
                    other.attachedRigidbody == rb)
                    continue;

                blocked = true;
                break;
            }

            if (blocked)
                continue;

            // Preserve both velocity components so this doesn't cancel or reverse
            // the player's jump and doesn't fight horizontal input.
            rb.position += offset;
            return true;
        }

        return false;
    }

    private void UpdateSmallStepUpAssist()
    {
        if (rb == null || playerCollider == null)
            return;

        if (stepUpActive)
        {
            ContinueSmallStepUp();
            return;
        }

        if (!enableSmallStepUp || !isGrounded || runtimeGroundMask == 0 ||
            rb.linearVelocity.y > 0.15f || Time.time < nextStepUpTime)
            return;

        // Use the current input first. If the input has just been released but
        // the rigidbody still has horizontal momentum, use that momentum.
        float direction = Mathf.Abs(moveInput) > 0.10f
            ? Mathf.Sign(moveInput)
            : (Mathf.Abs(rb.linearVelocity.x) > 0.5f
                ? Mathf.Sign(rb.linearVelocity.x)
                : 0f);

        if (Mathf.Abs(direction) < 0.5f)
            return;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(runtimeGroundMask);
        filter.useTriggers = false;

        // Only attempt stepping when a nearby obstacle blocks horizontal travel.
        assistCastHits.Clear();
        playerCollider.Cast(
            Vector2.right * direction,
            filter,
            assistCastHits,
            Mathf.Max(0.03f, stepUpProbeDistance));

        bool sideBlocked = false;
        for (int i = 0; i < assistCastHits.Count; i++)
        {
            RaycastHit2D hit = assistCastHits[i];
            Collider2D other = hit.collider;
            if (other == null || other.isTrigger || other == playerCollider ||
                other.attachedRigidbody == rb)
                continue;

            // Moving right should hit a left-facing wall, and vice versa.
            if (hit.normal.x * direction < -0.5f)
            {
                sideBlocked = true;
                break;
            }
        }

        if (!sideBlocked)
            return;

        Bounds bounds = playerCollider.bounds;
        float sampleOffset = Mathf.Max(0.015f, stepUpProbeDistance * 0.30f);
        float sampleX = bounds.center.x + direction * (bounds.extents.x + sampleOffset);
        float originY = bounds.min.y + maximumStepUpHeight + stepUpClearance + 0.03f;
        Vector2 origin = new Vector2(sampleX, originY);

        // Look down just beyond the player's leading edge to find the top of
        // the bump. The cast only accepts upward-facing surfaces.
        assistCastHits.Clear();
        Physics2D.Raycast(
            origin,
            Vector2.down,
            filter,
            assistCastHits,
            maximumStepUpHeight + stepUpClearance + 0.10f);

        float highestWalkableTop = float.NegativeInfinity;
        for (int i = 0; i < assistCastHits.Count; i++)
        {
            RaycastHit2D hit = assistCastHits[i];
            Collider2D other = hit.collider;
            if (other == null || other.isTrigger || other == playerCollider ||
                other.attachedRigidbody == rb || hit.normal.y < 0.55f)
                continue;

            if (hit.point.y > highestWalkableTop)
                highestWalkableTop = hit.point.y;
        }

        if (float.IsNegativeInfinity(highestWalkableTop))
            return;

        float surfaceRise = highestWalkableTop - bounds.min.y;
        // Ignore flat ground, tiny numerical noise, drops, and steps that are
        // too tall; those should keep their normal collision/jump behaviour.
        if (surfaceRise < 0.025f || surfaceRise > maximumStepUpHeight)
            return;

        float riseWithClearance = surfaceRise + stepUpClearance;
        Vector2 targetBodyPosition = rb.position + Vector2.up * riseWithClearance;

        // Never climb into a ceiling or another solid collider.
        if (!CanOccupyStepPosition(targetBodyPosition))
            return;

        stepUpTargetY = targetBodyPosition.y;
        stepUpActive = true;
        nextStepUpTime = Time.time + stepUpCooldown;
    }

    private void ContinueSmallStepUp()
    {
        if (!enableSmallStepUp)
        {
            stepUpActive = false;
            return;
        }

        // A queued step-up must never fight a jump. If the player jumps while a
        // small grounded step is still completing, cancel the step immediately;
        // otherwise rb.position corrections can compete with jump velocity and jitter.
        if (rb == null || rb.linearVelocity.y > headBumpMinimumUpwardSpeed)
        {
            stepUpActive = false;
            return;
        }

        float remaining = stepUpTargetY - rb.position.y;
        if (remaining <= 0.002f)
        {
            stepUpActive = false;
            return;
        }

        float moveUp = Mathf.Min(remaining, stepUpSpeed * Time.fixedDeltaTime);
        Vector2 nextPosition = rb.position + Vector2.up * moveUp;

        if (!CanOccupyStepPosition(nextPosition))
        {
            // Abort safely if geometry changes mid-step (for example, under a ceiling).
            stepUpActive = false;
            return;
        }

        rb.position = nextPosition;

        // Stepping is a grounded traversal aid, not a jump. Do not leave a tiny
        // downward velocity fighting the smooth rise.
        Vector2 velocity = rb.linearVelocity;
        if (velocity.y < 0f)
            velocity.y = 0f;
        rb.linearVelocity = velocity;

        if (stepUpTargetY - rb.position.y <= 0.002f)
            stepUpActive = false;
    }

    private bool CanOccupyStepPosition(Vector2 candidateBodyPosition)
    {
        if (playerCollider == null || rb == null || runtimeGroundMask == 0)
            return false;

        Bounds bounds = playerCollider.bounds;
        Vector2 offset = candidateBodyPosition - rb.position;
        Vector2 candidateCenter = (Vector2)bounds.center + offset;
        Vector2 testSize = new Vector2(
            Mathf.Max(0.02f, bounds.size.x * 0.94f),
            Mathf.Max(0.02f, bounds.size.y * 0.94f));

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(runtimeGroundMask);
        filter.useTriggers = false;

        assistOverlapHits.Clear();
        Physics2D.OverlapBox(candidateCenter, testSize, 0f, filter, assistOverlapHits);

        for (int i = 0; i < assistOverlapHits.Count; i++)
        {
            Collider2D other = assistOverlapHits[i];
            if (other == null || other.isTrigger || other == playerCollider ||
                other.attachedRigidbody == rb)
                continue;

            return false;
        }

        return true;
    }

    private void CheckHeadBumpProbe()
    {
        if (!enableHeadBumpCorrection || rb == null || playerCollider == null ||
            runtimeGroundMask == 0 ||
            rb.linearVelocity.y <= headBumpMinimumUpwardSpeed)
            return;

        Bounds bounds = playerCollider.bounds;
        float probeWidth = Mathf.Max(
            0.03f,
            bounds.size.x * Mathf.Clamp(headBumpProbeWidthMultiplier, 0.15f, 1.10f));
        float probeThickness = Mathf.Max(0.01f, headBumpProbeThickness);

        // The probe position is editable relative to the top-centre of the solid
        // player collider. A positive X offset moves it right; positive Y moves
        // it higher above the head. The exact same geometry is drawn in Scene view.
        Vector2 probeSize = new Vector2(probeWidth, probeThickness);
        Vector2 probeOrigin = new Vector2(
            bounds.center.x + headBumpProbeOffset.x,
            bounds.max.y + headBumpProbeOffset.y + probeThickness * 0.5f);

        // Extend the cast with upward speed so a fast jump cannot pass the probe
        // between physics updates. headBumpProbeDistance is the minimum distance.
        float distance = Mathf.Max(
            headBumpProbeDistance,
            rb.linearVelocity.y * Time.fixedDeltaTime + headBumpDetectionTolerance * 0.35f);

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(runtimeGroundMask);
        filter.useTriggers = false;

        assistCastHits.Clear();
        Physics2D.BoxCast(
            probeOrigin,
            probeSize,
            0f,
            Vector2.up,
            filter,
            assistCastHits,
            distance);

        for (int i = 0; i < assistCastHits.Count; i++)
        {
            RaycastHit2D hit = assistCastHits[i];
            Collider2D other = hit.collider;
            if (other == null || other.isTrigger || other == playerCollider ||
                other.attachedRigidbody == rb)
                continue;

            // Only accept an underside/ceiling hit, not a wall face grazed by the
            // probe. Keep the test centred on the editable probe instead of the
            // full player bounds so moving the probe also moves the detection area.
            if (hit.normal.y > -0.55f ||
                Mathf.Abs(hit.normal.y) < Mathf.Abs(hit.normal.x) * 1.10f)
                continue;

            float headContactHalfWidth = probeSize.x * 0.36f;
            if (Mathf.Abs(hit.point.x - probeOrigin.x) > headContactHalfWidth)
                continue;

            StopUpwardHeadBump();
            return;
        }
    }

    private void StopUpwardHeadBump()
    {
        if (rb == null || rb.linearVelocity.y <= 0f)
            return;

        Vector2 velocity = rb.linearVelocity;
        velocity.y = headBumpDownwardAssist > 0f
            ? -headBumpDownwardAssist
            : 0f;
        rb.linearVelocity = velocity;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleForgivingCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        // Keep checking persistent contacts while airborne, including during a
        // fall. Corner catches often happen on the way DOWN a staircase, so this
        // must not be limited to upward motion. Head-bump handling is still
        // gated by wasMovingUp inside HandleForgivingCollision, and the corner
        // cooldown prevents repeated corrections from causing vibration.
        if (rb != null && !isGrounded)
            HandleForgivingCollision(collision);
    }

    private void HandleForgivingCollision(Collision2D collision)
    {
        if (collision == null || rb == null || playerCollider == null)
            return;

        // Trigger overlaps do not generate useful solid contact points.
        if (collision.collider == null || collision.otherCollider == null ||
            collision.collider.isTrigger || collision.otherCollider.isTrigger)
            return;

        Bounds bounds = playerCollider.bounds;
        bool hitHead = false;
        bool hitUpperSide = false;
        float strongestSideContact = 0f;
        float obstacleSide = 0f;

        bool wasMovingUp =
            rb.linearVelocity.y > headBumpMinimumUpwardSpeed ||
            previousYVelocity > headBumpMinimumUpwardSpeed;

        // Classify contacts by both position and normal. Corner contacts can
        // appear near the top edge, so position alone must never cancel a jump.
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            Vector2 point = contact.point;

            bool nearTop = point.y >= bounds.max.y - headBumpDetectionTolerance;
            float horizontalOffset = point.x - bounds.center.x;

            // A side-wall contact can happen right at the top corner and may
            // report a vertical-looking normal. Do not call that a head bump
            // unless the contact is also well inside the top face. This is the
            // key guard that lets the player keep rising alongside a tall block.
            Vector2 normal = contact.normal;
            bool verticalContact = Mathf.Abs(normal.y) > 0.55f &&
                Mathf.Abs(normal.y) >= Mathf.Abs(normal.x) * 1.15f;
            float headInteriorHalfWidth = bounds.extents.x * 0.62f;
            bool contactInsideHead = Mathf.Abs(horizontalOffset) <= headInteriorHalfWidth;
            if (nearTop && contactInsideHead && verticalContact)
                hitHead = true;

            float sideThreshold = Mathf.Max(0.015f, bounds.extents.x * 0.50f);
            bool onSide = Mathf.Abs(horizontalOffset) >= sideThreshold;
            bool onUpperHalf = point.y >= bounds.center.y - headBumpDetectionTolerance;

            // An upper corner can be both a head and a side collision. Do not
            // discard the side response just because a head contact was found.
            if (onSide && onUpperHalf && Mathf.Abs(horizontalOffset) > strongestSideContact)
            {
                strongestSideContact = Mathf.Abs(horizontalOffset);
                obstacleSide = Mathf.Sign(horizontalOffset); // +1 = obstacle on right.
                hitUpperSide = true;
            }
        }

        if (wasMovingUp && enableHeadBumpCorrection && hitHead)
        {
            if (headBumpStopUpwardVelocity)
                StopUpwardHeadBump();
            else if (headBumpDownwardAssist > 0f && rb.linearVelocity.y > 0f)
            {
                Vector2 velocity = rb.linearVelocity;
                velocity.y = -headBumpDownwardAssist;
                rb.linearVelocity = velocity;
            }
        }

        // While the player is actively rising into a side/upper corner, do not
        // nudge them away or kill their horizontal progress. That used to pin
        // the character just below the platform lip, especially when jump and
        // direction were held together. A genuine ceiling hit has already been
        // handled above. Keep corner nudging for falling/descending catches.
        // Do not reposition the Rigidbody sideways while it is rising or near
        // the apex. That correction fights the jump and makes the player vibrate
        // against the corner. Falling-side recovery is allowed once descent is clear.
        if (rb.linearVelocity.y > -0.15f && !hitHead)
            return;

        // Free the player from a corner by nudging away only when clearly falling.
        // Grounded wall contact is excluded so normal walking against walls stays stable.
        if (!enableCornerClip || isGrounded || !hitUpperSide ||
            Time.time < nextCornerNudgeTime)
            return;

        // Only nudge if input or momentum is trying to enter that obstacle.
        bool movingIntoObstacle =
            (Mathf.Abs(moveInput) > 0.01f && moveInput * obstacleSide > 0f) ||
            rb.linearVelocity.x * obstacleSide > 0.05f;

        if (!movingIntoObstacle)
            return;

        float awayDirection = -obstacleSide;
        if (TryHorizontalCorrection(awayDirection))
        {
            nextCornerNudgeTime = Time.time + cornerNudgeCooldown;

            // Stop the immediate push back into the same corner. Player input
            // remains available, so the player can move away or continue later.
            Vector2 velocity = rb.linearVelocity;
            if (velocity.x * obstacleSide > 0f)
                velocity.x = 0f;
            rb.linearVelocity = velocity;
        }
    }

    private bool TryHorizontalCorrection(float direction)
    {
        if (Mathf.Abs(direction) < 0.01f || cornerNudgeDistance <= 0f ||
            playerCollider == null || rb == null)
            return false;

        Bounds bounds = playerCollider.bounds;
        Vector2 testSize = new Vector2(
            Mathf.Max(0.02f, bounds.size.x * 0.96f),
            Mathf.Max(0.02f, bounds.size.y * 0.96f));

        float step = Mathf.Max(0.005f, cornerNudgeStep);
        int steps = Mathf.CeilToInt(cornerNudgeDistance / step);

        ContactFilter2D overlapFilter = new ContactFilter2D();
        overlapFilter.SetLayerMask(~0);
        overlapFilter.useTriggers = false;

        for (int i = 1; i <= steps; i++)
        {
            float distance = Mathf.Min(cornerNudgeDistance, step * i);
            float signedOffset = Mathf.Sign(direction) * distance;
            Vector2 candidateCenter = (Vector2)bounds.center + Vector2.right * signedOffset;

            assistOverlapHits.Clear();
            Physics2D.OverlapBox(
                candidateCenter,
                testSize,
                0f,
                overlapFilter,
                assistOverlapHits);

            bool blocked = false;
            for (int j = 0; j < assistOverlapHits.Count; j++)
            {
                Collider2D overlap = assistOverlapHits[j];
                if (overlap == null || overlap.isTrigger ||
                    overlap == playerCollider || overlap.attachedRigidbody == rb)
                    continue;

                blocked = true;
                break;
            }

            if (blocked)
                continue;

            rb.position += Vector2.right * signedOffset;
            return true;
        }

        return false;
    }

    // Draw the exact head-bump BoxCast start box and cast path in the Scene view.
    // Uses OnDrawGizmos (rather than OnDrawGizmosSelected) to coexist with the
    // main controller's own OnDrawGizmosSelected method in another partial file.
    private void OnDrawGizmos()
    {
        if (!showHeadBumpProbeGizmo)
            return;

        Collider2D gizmoCollider = playerCollider != null
            ? playerCollider
            : GetComponent<Collider2D>();
        if (gizmoCollider == null)
            return;

        Bounds bounds = gizmoCollider.bounds;
        float width = Mathf.Max(
            0.03f,
            bounds.size.x * Mathf.Clamp(headBumpProbeWidthMultiplier, 0.15f, 1.10f));
        float height = Mathf.Max(0.01f, headBumpProbeThickness);
        Vector3 startCenter = new Vector3(
            bounds.center.x + headBumpProbeOffset.x,
            bounds.max.y + headBumpProbeOffset.y + height * 0.5f,
            bounds.center.z);
        Vector3 boxSize = new Vector3(width, height, 0.01f);

        Rigidbody2D gizmoRb = rb != null ? rb : GetComponent<Rigidbody2D>();
        float castDistance = headBumpProbeDistance;
        if (gizmoRb != null && gizmoRb.linearVelocity.y > 0f)
        {
            castDistance = Mathf.Max(
                castDistance,
                gizmoRb.linearVelocity.y * Time.fixedDeltaTime + headBumpDetectionTolerance * 0.35f);
        }
        castDistance = Mathf.Max(0.01f, castDistance);
        Vector3 endCenter = startCenter + Vector3.up * castDistance;

        Gizmos.color = new Color(0.1f, 0.9f, 1f, 1f);
        Gizmos.DrawWireCube(startCenter, boxSize);
        Gizmos.DrawLine(
            startCenter + Vector3.left * width * 0.5f,
            endCenter + Vector3.left * width * 0.5f);
        Gizmos.DrawLine(
            startCenter + Vector3.right * width * 0.5f,
            endCenter + Vector3.right * width * 0.5f);
        Gizmos.DrawWireCube(endCenter, boxSize);
    }

}