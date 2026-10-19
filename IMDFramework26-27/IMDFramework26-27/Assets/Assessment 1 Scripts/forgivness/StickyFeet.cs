using UnityEngine;
public class StickyFeetOnLand : MonoBehaviour
{
    [Header("Sticky Feet on Land")]
    [SerializeField] private bool m_StickyFeetEnabled = true;
    [Tooltip("How long the landing assist lasts.")]
    [SerializeField] private float m_LandingAssistDuration = 0.15f;
    [Tooltip("Horizontal speed multiplier immediately after landing. Lower means stronger braking.")]
    [SerializeField, Range(0f, 1f)]
    private float m_LandingSpeedMultiplier = 0.5f;
    private GroundSensor m_GroundSensor;
    private bool m_WasGrounded;
    private float m_LandingAssistTimer;
    public float HorizontalSpeedMultiplier
    {
        get
        {
            if (!m_StickyFeetEnabled || m_LandingAssistTimer <= 0f)
                return 1f;
            return m_LandingSpeedMultiplier;
        }
    }
    private void Awake()
    {
        m_GroundSensor = GetComponentInChildren<GroundSensor>();
    }
    private void FixedUpdate()
    {
        if (!m_StickyFeetEnabled || m_GroundSensor == null)
        {
            m_WasGrounded = false;
            m_LandingAssistTimer = 0f;
            return;
        }
        bool groundedNow = m_GroundSensor.IsGrounded();
        // Start the assist only when transitioning from airborne to grounded.
        if (groundedNow && !m_WasGrounded)
        {
            m_LandingAssistTimer = m_LandingAssistDuration;
        }
        m_WasGrounded = groundedNow;
        if (m_LandingAssistTimer > 0f)
        {
            m_LandingAssistTimer -= Time.fixedDeltaTime;
        }
    }
}