using UnityEngine;
public class CoyoteTime : MonoBehaviour
{
    [SerializeField] private float m_CoyoteTimeThreshold = 0.15f;
    private float m_CoyoteTimeCounter;
    public void SetGrounded(bool grounded)
    {
        if (grounded)
        {
            m_CoyoteTimeCounter = m_CoyoteTimeThreshold;
        }
        else
        {
            m_CoyoteTimeCounter -= Time.fixedDeltaTime;
        }
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