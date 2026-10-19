using UnityEngine;
public class AntiGravityApex : MonoBehaviour
{
    [Header("Anti-Gravity Apex")]
    [SerializeField] private float m_ApexVelocityThreshold = 0.5f;
    [SerializeField] private float m_ApexGravityMultiplier = 0.8f;
    private Rigidbody2D m_RB;
    private float m_NormalGravity;
    private bool m_IsActive;
    private void Awake()
    {
        m_RB = GetComponent<Rigidbody2D>();
        m_NormalGravity = m_RB.gravityScale;
    }
    private void FixedUpdate()
    {
        float verticalVelocity = m_RB.linearVelocityY;
        // Only reduce gravity near the top of a jump.
        if (m_IsActive &&
            verticalVelocity > 0f &&
            verticalVelocity <= m_ApexVelocityThreshold)
        {
            m_RB.gravityScale =
                m_NormalGravity * m_ApexGravityMultiplier;
        }
        else
        {
            m_RB.gravityScale = m_NormalGravity;
        }
    }
    public void SetJumping(bool jumping)
    {
        m_IsActive = jumping;
        if (!jumping)
        {
            m_RB.gravityScale = m_NormalGravity;
        }
    }
}