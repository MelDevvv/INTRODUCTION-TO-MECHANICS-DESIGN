using UnityEngine;
using UnityEngine.InputSystem;
public class CharacterMovement : MonoBehaviour
{
    private Rigidbody2D m_RB;
    [SerializeField] private float m_MoveSpeed;
    [SerializeField] private float m_JumpStrength;
    [SerializeField] private Transform m_RaycastPosition;
    [SerializeField] private LayerMask m_GroundLayer;
    private float m_InMove;
    private PlayerControls m_ActionMap;
    private bool m_IsGrounded;
    private CoyoteTime m_CoyoteTime;
    private JumpBuffer m_JumpBuffer;
    // Stops the player from jumping repeatedly
    // while the ground ray is still touching the floor.
    private bool m_HasJumped;
    private void Awake()
    {
        m_ActionMap = new PlayerControls();
        m_RB = GetComponent<Rigidbody2D>();
        m_CoyoteTime = GetComponent<CoyoteTime>();
        m_JumpBuffer = GetComponent<JumpBuffer>();
    }
    #region Bindings
    private void OnEnable()
    {
        m_ActionMap.Enable();
        m_ActionMap.Default.MoveHoriz.performed += Handle_MovePerformed;
        m_ActionMap.Default.MoveHoriz.canceled += Handle_MoveCancelled;
        m_ActionMap.Default.Jump.performed += Handle_JumpPerformed;
    }
    private void OnDisable()
    {
        m_ActionMap.Disable();
        m_ActionMap.Default.MoveHoriz.performed -= Handle_MovePerformed;
        m_ActionMap.Default.MoveHoriz.canceled -= Handle_MoveCancelled;
        m_ActionMap.Default.Jump.performed -= Handle_JumpPerformed;
    }
    #endregion
    #region InputFunctions
    private void Handle_MovePerformed(InputAction.CallbackContext context)
    {
        m_InMove = context.ReadValue<float>();
    }
    private void Handle_MoveCancelled(InputAction.CallbackContext context)
    {
        m_InMove = 0f;
    }
    private void Handle_JumpPerformed(InputAction.CallbackContext context)
    {
        // Store the jump input for jump buffering.
        m_JumpBuffer.JumpPressed();
        // Normal jump or coyote-time jump.
        TryJump();
    }
    #endregion
    private void FixedUpdate()
    {
        m_RB.linearVelocityX = m_MoveSpeed * m_InMove;
        bool groundedNow = Physics2D.Raycast(
            m_RaycastPosition.position,
            Vector2.down,
            0.1f,
            m_GroundLayer
        );
        // Detect when we have actually left the ground.
        if (!groundedNow)
        {
            m_IsGrounded = false;
            m_HasJumped = true;
        }
        else
        {
            m_IsGrounded = true;
            // We have landed again.
            if (m_HasJumped)
            {
                m_HasJumped = false;
                // Jump buffering:
                // if jump was pressed just before landing,
                // immediately jump.
                if (m_JumpBuffer.HasBufferedJump())
                {
                    PerformJump();
                }
            }
        }
        m_CoyoteTime.SetGrounded(m_IsGrounded);
    }
    private void TryJump()
    {
        // Don't allow another normal jump if we've already jumped
        // and haven't actually landed again.
        if (m_IsGrounded && !m_HasJumped)
        {
            PerformJump();
            return;
        }
        // Coyote-time jump after leaving a platform.
        if (!m_IsGrounded && m_CoyoteTime.CanJump())
        {
            PerformJump();
        }
    }
    private void PerformJump()
    {
        m_RB.linearVelocityY = 0f;
        m_RB.AddForce(
            Vector2.up * m_JumpStrength,
            ForceMode2D.Impulse
        );
        // The player has now used their jump.
        m_HasJumped = true;
        // Consume coyote time and buffered input.
        m_CoyoteTime.UseCoyoteTime();
        m_JumpBuffer.UseBufferedJump();
    }
}