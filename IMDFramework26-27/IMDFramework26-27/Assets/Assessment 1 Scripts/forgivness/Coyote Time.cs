using UnityEngine;
public class CoyoteTime : MonoBehaviour
{
    [Header("Coyote Time")]
    [SerializeField] private float m_CoyoteTimeThreshold = 0.15f;
    private float m_CoyoteTimeCounter;
    private bool m_WasGrounded;
    public void SetGrounded(bool grounded)
    {
        if (grounded)
        {
            // Refresh the timer only when grounded.
            m_CoyoteTimeCounter = m_CoyoteTimeThreshold;
        }
        else if (m_WasGrounded)
        {
            // Start counting down when leaving the ground.
            m_CoyoteTimeCounter = m_CoyoteTimeThreshold;
        }
        else
        {
            m_CoyoteTimeCounter -= Time.fixedDeltaTime;
            m_CoyoteTimeCounter = Mathf.Max(0f, m_CoyoteTimeCounter);
        }
        m_WasGrounded = grounded;
    }
    public bool CanJump()
    {
        return m_CoyoteTimeCounter > 0f;
    }
    public void UseCoyoteTime()
    {
        m_CoyoteTimeCounter = 0f;
    }
}