using UnityEngine;
public class VariableJumpHeight : MonoBehaviour
{
    [Header("Variable Jump Height")]
    [SerializeField] private bool m_VariableJumpHeightEnabled = true;
    [Tooltip("Extra gravity applied when the jump button is released.")]
    [SerializeField] private float m_ReleaseGravityMultiplier = 2f;
    [Tooltip("Minimum upward velocity before the jump cut stops.")]
    [SerializeField] private float m_MinJumpVelocity = 2f;
    private Rigidbody2D m_RB;
    private bool m_JumpReleased;
    private void Awake()
    {
        m_RB = GetComponent<Rigidbody2D>();
    }
    private void FixedUpdate()
    {
        if (!m_VariableJumpHeightEnabled || !m_JumpReleased)
        {
            return;
        }
        if (m_RB.linearVelocityY > m_MinJumpVelocity)
        {
            m_RB.linearVelocityY += Physics2D.gravity.y
                * (m_ReleaseGravityMultiplier - 1f)
                * m_RB.gravityScale
                * Time.fixedDeltaTime;
        }
        else
        {
            m_JumpReleased = false;
        }
    }
    public void JumpStarted()
    {
        m_JumpReleased = false;
    }
    public void JumpReleased()
    {
        m_JumpReleased = true;
    }
}