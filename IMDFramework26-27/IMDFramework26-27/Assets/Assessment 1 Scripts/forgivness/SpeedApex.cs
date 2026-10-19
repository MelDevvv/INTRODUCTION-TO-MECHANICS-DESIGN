using UnityEngine;
public class SpeedApex : MonoBehaviour
{
    [Header("Speed Apex")]
    [SerializeField] private bool m_SpeedApexEnabled = true;
    [Tooltip("How close to the jump apex the effect starts.")]
    [SerializeField] private float m_VerticalSpeedThreshold = 1.5f;
    [Tooltip("Maximum horizontal speed boost. 1.05 = 5% boost.")]
    [SerializeField] private float m_MaxSpeedMultiplier = 1.05f;
    [Tooltip("How smoothly the boost fades in and out.")]
    [SerializeField] private float m_Smoothing = 3f;
    private float m_CurrentMultiplier = 1f;
    public float GetSpeedMultiplier(float verticalVelocity)
    {
        if (!m_SpeedApexEnabled)
        {
            m_CurrentMultiplier = 1f;
            return 1f;
        }
        float proximity = 1f - Mathf.Clamp01(
            Mathf.Abs(verticalVelocity) / m_VerticalSpeedThreshold
        );
        float targetMultiplier = Mathf.Lerp(
            1f,
            m_MaxSpeedMultiplier,
            proximity
        );
        m_CurrentMultiplier = Mathf.MoveTowards(
            m_CurrentMultiplier,
            targetMultiplier,
            m_Smoothing * Time.fixedDeltaTime
        );
        return m_CurrentMultiplier;
    }
    private void OnDisable()
    {
        m_CurrentMultiplier = 1f;
    }
}