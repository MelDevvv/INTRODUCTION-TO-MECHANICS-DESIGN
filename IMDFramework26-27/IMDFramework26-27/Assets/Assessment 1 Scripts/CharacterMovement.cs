using System.Collections;
using UnityEngine;
public class CharacterMovement : MonoBehaviour
{
    [Header("Ground Movement")]
    [SerializeField] private float m_MoveSpeed = 6f;
    [SerializeField] private float m_GroundAcceleration = 45f;
    [SerializeField] private float m_GroundDeceleration = 55f;
    [SerializeField] private float m_GroundTurnSpeed = 65f;
    [Header("Air Movement")]
    [SerializeField] private float m_AirAcceleration = 60f;
    [SerializeField] private float m_AirDeceleration = 8f;
    [SerializeField, Range(0f, 1f)] private float m_AirControl = 1f;
    [SerializeField] private bool m_PreserveAirMomentum = true;
    [SerializeField] private float m_AirMomentumDrag = 0.5f;
    [SerializeField] private float m_AirTurnSpeed = 100f;
    [Header("Jump")]
    [SerializeField] private float m_JumpStrength = 10f;
    [Header("Variable Jump Height")]
    [SerializeField] private bool m_VariableJumpEnabled = true;
    [SerializeField] private float m_JumpHoldGravityMultiplier = 1f;
    [SerializeField] private float m_JumpReleaseGravityMultiplier = 3f;
    [SerializeField] private float m_MinimumJumpHoldTime = 0.08f;
    [SerializeField] private float m_MaximumJumpHoldTime = 0.35f;
    [SerializeField] private float m_MinimumJumpCutVelocity = 2f;
    [Header("Jump Gravity")]
    [SerializeField] private bool m_UseCustomJumpGravity = true;
    [SerializeField] private float m_RisingGravityMultiplier = 2.5f;
    [SerializeField] private float m_ApexGravityMultiplier = 1.8f;
    [SerializeField] private float m_FallingGravityMultiplier = 4.5f;
    [SerializeField] private float m_ApexVelocityThreshold = 0.5f;
    [Header("Early Fall")]
    [SerializeField] private float m_EarlyFallMultiplier = 4.5f;
    [Header("Clamp Fall Speed")]
    [SerializeField] private bool m_ClampFallSpeed = true;
    [SerializeField] private float m_MaxFallSpeed = 25f;
    private Rigidbody2D m_RB;
    private GroundSensor m_GroundSensor;
    private CoyoteTime m_CoyoteTime;
    private JumpBuffer m_JumpBuffer;
    private StickyFeetOnLand m_StickyFeetOnLand;
    private float m_InMove;
    private float m_NormalGravity;
    private float m_JumpHoldTimer;
    private bool m_IsGrounded;
    private bool m_HasJumped;
    private bool m_JumpButtonHeld;
    private bool m_JumpReleasedEarly;
    private bool m_EarlyFallRequested;
    private Coroutine m_JumpCoroutine;
    private void Awake()
    {
        m_RB = GetComponent<Rigidbody2D>();
        m_GroundSensor = GetComponentInChildren<GroundSensor>();
        m_CoyoteTime = GetComponent<CoyoteTime>();
        m_JumpBuffer = GetComponent<JumpBuffer>();
        m_StickyFeetOnLand = GetComponent<StickyFeetOnLand>();
        if (m_RB != null)
        {
            m_NormalGravity = m_RB.gravityScale;
        }
    }
    private void Start()
    {
        if (m_GroundSensor == null)
        {
            Debug.LogError(
                "CharacterMovement: GroundSensor was not found.", this);
            enabled = false;
            return;
        }
        StartCoroutine(C_GroundCheck());
    }
    private void FixedUpdate()
    {
        if (m_RB == null)
            return;
        UpdateHorizontalMovement();
        UpdateJumpGravity();
        // Clamp Fall Speed
        if (m_ClampFallSpeed &&
            m_RB.linearVelocityY < -m_MaxFallSpeed)
        {
            m_RB.linearVelocityY = -m_MaxFallSpeed;
        }
    }
    public void SetInMove(float direction)
    {
        m_InMove = Mathf.Clamp(direction, -1f, 1f);
    }
    private void UpdateHorizontalMovement()
    {
        float currentX = m_RB.linearVelocityX;
        // Sticky Feet multiplier
        float stickyMultiplier = m_StickyFeetOnLand != null
            ? m_StickyFeetOnLand.HorizontalSpeedMultiplier
            : 1f;
        float targetX = m_InMove * m_MoveSpeed * stickyMultiplier;
        if (m_IsGrounded)
        {
            float acceleration;
            if (Mathf.Abs(m_InMove) < 0.01f)
            {
                targetX = 0f;
                acceleration = m_GroundDeceleration;
            }
            else if (Mathf.Abs(currentX) > 0.01f &&
                     Mathf.Sign(currentX) != Mathf.Sign(targetX))
            {
                acceleration = m_GroundTurnSpeed;
            }
            else
            {
                acceleration = m_GroundAcceleration;
            }
            currentX = Mathf.MoveTowards(
                currentX,
                targetX,
                acceleration * Time.fixedDeltaTime);
        }
        else if (Mathf.Abs(m_InMove) > 0.01f)
        {
            float airTarget = targetX * m_AirControl;
            currentX = Mathf.MoveTowards(
                currentX,
                airTarget,
                m_AirAcceleration * Time.fixedDeltaTime);
            if (Mathf.Abs(currentX) > 0.01f &&
                Mathf.Sign(currentX) != Mathf.Sign(airTarget))
            {
                currentX = Mathf.MoveTowards(
                    currentX,
                    airTarget,
                    m_AirTurnSpeed * Time.fixedDeltaTime);
            }
        }
        else if (m_PreserveAirMomentum)
        {
            currentX = Mathf.MoveTowards(
                currentX,
                0f,
                m_AirMomentumDrag * Time.fixedDeltaTime);
        }
        else
        {
            currentX = Mathf.MoveTowards(
                currentX,
                0f,
                m_AirDeceleration * Time.fixedDeltaTime);
        }
        m_RB.linearVelocityX = currentX;
    }
    private void UpdateJumpGravity()
    {
        if (!m_UseCustomJumpGravity)
            return;
        float multiplier = 1f;
        float verticalVelocity = m_RB.linearVelocityY;
        if (verticalVelocity > 0.01f)
        {
            if (m_EarlyFallRequested)
            {
                multiplier = m_EarlyFallMultiplier;
            }
            else if (m_JumpReleasedEarly)
            {
                multiplier = m_JumpReleaseGravityMultiplier;
            }
            else if (m_VariableJumpEnabled &&
                     m_JumpButtonHeld &&
                     m_JumpHoldTimer < m_MaximumJumpHoldTime)
            {
                multiplier = m_JumpHoldGravityMultiplier;
            }
            else if (verticalVelocity <= m_ApexVelocityThreshold)
            {
                multiplier = m_ApexGravityMultiplier;
            }
            else
            {
                multiplier = m_RisingGravityMultiplier;
            }
        }
        else if (verticalVelocity < -0.01f)
        {
            multiplier = m_FallingGravityMultiplier;
        }
        m_RB.gravityScale = m_NormalGravity * multiplier;
    }
    public void JumpPressed()
    {
        if (m_JumpBuffer != null)
        {
            m_JumpBuffer.JumpPressed();
        }
        m_JumpButtonHeld = true;
        m_JumpReleasedEarly = false;
        m_EarlyFallRequested = false;
        m_JumpHoldTimer = 0f;
        TryJump();
    }
    public void JumpCancelled()
    {
        m_JumpButtonHeld = false;
        if (m_VariableJumpEnabled &&
            m_JumpHoldTimer >= m_MinimumJumpHoldTime &&
            m_RB.linearVelocityY > m_MinimumJumpCutVelocity)
        {
            m_JumpReleasedEarly = true;
        }
    }
    public void EarlyFallPressed()
    {
        if (m_HasJumped && m_RB.linearVelocityY > 0f)
        {
            m_EarlyFallRequested = true;
        }
    }
    private void TryJump()
    {
        if (m_IsGrounded && !m_HasJumped)
        {
            PerformJump();
        }
        else if (!m_IsGrounded &&
                 !m_HasJumped &&
                 m_CoyoteTime != null &&
                 m_CoyoteTime.CanJump())
        {
            PerformJump();
        }
    }
    private void PerformJump()
    {
        if (m_JumpCoroutine != null)
        {
            StopCoroutine(m_JumpCoroutine);
        }
        m_RB.gravityScale = m_NormalGravity;
        m_RB.linearVelocityY = 0f;
        m_RB.AddForce(
            Vector2.up * m_JumpStrength,
            ForceMode2D.Impulse);
        m_HasJumped = true;
        m_JumpReleasedEarly = false;
        m_EarlyFallRequested = false;
        m_JumpHoldTimer = 0f;
        if (m_CoyoteTime != null)
        {
            m_CoyoteTime.UseCoyoteTime();
        }
        if (m_JumpBuffer != null)
        {
            m_JumpBuffer.UseBufferedJump();
        }
        m_JumpCoroutine = StartCoroutine(C_JumpUpdate());
    }
    private IEnumerator C_JumpUpdate()
    {
        while (m_HasJumped)
        {
            if (m_JumpButtonHeld)
            {
                m_JumpHoldTimer += Time.fixedDeltaTime;
            }
            if (m_GroundSensor.IsGrounded() &&
                m_RB.linearVelocityY <= 0f)
            {
                m_HasJumped = false;
                m_JumpButtonHeld = false;
                m_JumpReleasedEarly = false;
                m_EarlyFallRequested = false;
                m_RB.gravityScale = m_NormalGravity;
                if (m_JumpBuffer != null &&
                    m_JumpBuffer.HasBufferedJump())
                {
                    PerformJump();
                }
                yield break;
            }
            yield return new WaitForFixedUpdate();
        }
        m_RB.gravityScale = m_NormalGravity;
    }
    private IEnumerator C_GroundCheck()
    {
        while (true)
        {
            bool groundedNow = m_GroundSensor != null &&
                               m_GroundSensor.IsGrounded();
            m_IsGrounded = groundedNow;
            if (m_CoyoteTime != null)
            {
                m_CoyoteTime.SetGrounded(groundedNow);
            }
            if (groundedNow &&
                !m_HasJumped &&
                m_JumpBuffer != null &&
                m_JumpBuffer.HasBufferedJump())
            {
                PerformJump();
            }
            yield return new WaitForFixedUpdate();
        }
    }
    public void Init()
    {
        Debug.Log("CharacterMovement initialised!");
    }
}