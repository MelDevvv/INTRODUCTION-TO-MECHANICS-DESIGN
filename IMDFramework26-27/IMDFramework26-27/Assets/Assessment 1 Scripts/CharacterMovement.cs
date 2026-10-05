using System.Collections;
using UnityEngine;
public class CharacterMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float m_MoveSpeed = 5f;
    [Header("Jump")]
    [SerializeField] private float m_JumpStrength = 10f;
    private Rigidbody2D m_RB;
    private GroundSensor m_GroundSensor;
    private CoyoteTime m_CoyoteTime;
    private JumpBuffer m_JumpBuffer;
    private float m_InMove;
    private bool m_IsGrounded;
    private bool m_HasJumped;
    private bool m_IsMoveActive;
    private Coroutine m_MoveCoroutine;
    private Coroutine m_JumpCoroutine;
    private Coroutine m_GroundCheckCoroutine;
    private float m_NormalGravity;
    private void Awake()
    {
        m_RB = GetComponent<Rigidbody2D>();
        m_GroundSensor = GetComponentInChildren<GroundSensor>();
        m_CoyoteTime = GetComponent<CoyoteTime>();
        m_JumpBuffer = GetComponent<JumpBuffer>();
        m_NormalGravity = m_RB.gravityScale;
    }
    private void Start()
    {
        m_GroundCheckCoroutine = StartCoroutine(C_GroundCheck());
    }
    // ==================================================
    // MOVEMENT
    // ==================================================
    public void SetInMove(float direction)
    {
        m_InMove = direction;
        if (m_InMove == 0f)
        {
            m_IsMoveActive = false;
            m_RB.linearVelocityX = 0f;
            return;
        }
        if (m_IsMoveActive)
        {
            return;
        }
        m_IsMoveActive = true;
        m_MoveCoroutine = StartCoroutine(C_MoveUpdate());
    }
    private IEnumerator C_MoveUpdate()
    {
        while (m_IsMoveActive)
        {
            m_RB.linearVelocityX = m_MoveSpeed * m_InMove;
            yield return new WaitForFixedUpdate();
        }
        m_RB.linearVelocityX = 0f;
    }
    // ==================================================
    // JUMP INPUT
    // ==================================================
    public void JumpPressed()
    {
        // Remember the jump input.
        m_JumpBuffer.JumpPressed();
        TryJump();
    }
    public void JumpCancelled()
    {
        // Intentionally left empty.
        //
        // This keeps every jump the same height.
        // Jump buffering and coyote time still work.
    }
    // ==================================================
    // TRY JUMP
    // ==================================================
    private void TryJump()
    {
        // Normal jump.
        if (m_IsGrounded && !m_HasJumped)
        {
            PerformJump();
            return;
        }
        // Coyote Time jump.
        if (!m_IsGrounded &&
            !m_HasJumped &&
            m_CoyoteTime.CanJump())
        {
            PerformJump();
        }
    }
    // ==================================================
    // PERFORM JUMP
    // ==================================================
    private void PerformJump()
    {
        if (m_JumpCoroutine != null)
        {
            StopCoroutine(m_JumpCoroutine);
        }
        // Always start the jump from exactly the same velocity.
        m_RB.linearVelocityY = 0f;
        // Reset gravity so every jump starts consistently.
        m_RB.gravityScale = m_NormalGravity;
        // Apply exactly the same jump force every time.
        m_RB.AddForce(
            Vector2.up * m_JumpStrength,
            ForceMode2D.Impulse
        );
        m_HasJumped = true;
        // Consume coyote time.
        m_CoyoteTime.UseCoyoteTime();
        // Consume the buffered input.
        m_JumpBuffer.UseBufferedJump();
        m_JumpCoroutine = StartCoroutine(C_JumpUpdate());
    }
    // ==================================================
    // JUMP COROUTINE
    // ==================================================
    private IEnumerator C_JumpUpdate()
    {
        while (m_HasJumped)
        {
            // Check if we have landed.
            if (m_GroundSensor.IsGrounded() &&
                m_RB.linearVelocityY <= 0f)
            {
                m_HasJumped = false;
                m_RB.gravityScale = m_NormalGravity;
                // If jump was pressed just before landing,
                // immediately perform another jump.
                if (m_JumpBuffer.HasBufferedJump())
                {
                    PerformJump();
                }
                yield break;
            }
            yield return new WaitForFixedUpdate();
        }
        m_RB.gravityScale = m_NormalGravity;
    }
    // ==================================================
    // GROUND CHECK
    // ==================================================
    private IEnumerator C_GroundCheck()
    {
        while (true)
        {
            bool groundedNow = m_GroundSensor.IsGrounded();
            m_IsGrounded = groundedNow;
            // Update Coyote Time.
            m_CoyoteTime.SetGrounded(groundedNow);
            // Jump Buffer:
            // if the player pressed jump shortly before
            // touching the ground, jump immediately.
            if (groundedNow &&
                !m_HasJumped &&
                m_JumpBuffer.HasBufferedJump())
            {
                PerformJump();
            }
            yield return new WaitForFixedUpdate();
        }
    }
    // ==================================================
    // INIT CHAIN
    // ==================================================
    public void Init()
    {
        Debug.Log("CharacterMovement initialised!");
    }
}