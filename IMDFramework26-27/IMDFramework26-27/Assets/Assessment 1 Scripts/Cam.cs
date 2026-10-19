using UnityEngine;
public class SmoothCameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform m_Target;
    [Header("Smooth Follow")]
    [SerializeField] private float m_SmoothTime = 0.22f;
    [SerializeField] private float m_MaxSpeed = 30f;
    [Header("Camera Position")]
    [SerializeField] private float m_HeightOffset = 1f;
    [SerializeField] private float m_Distance = 10f;
    [Header("Look Ahead")]
    [SerializeField] private bool m_UseLookAhead = true;
    [SerializeField] private float m_LookAheadDistance = 1.5f;
    [SerializeField] private float m_LookAheadSmoothTime = 0.3f;
    private Vector3 m_Velocity;
    private float m_CurrentLookAhead;
    private float m_LookAheadVelocity;
    private Rigidbody2D m_TargetRB;
    private void Start()
    {
        if (m_Target == null)
        {
            Debug.LogError(
                "SmoothCameraFollow: Assign the player Transform.", this);
            enabled = false;
            return;
        }
        m_TargetRB = m_Target.GetComponent<Rigidbody2D>();
        // Start at the target so the camera doesn't travel from the
        // scene's original position when play begins.
        transform.position = GetDesiredPosition();
    }
    private void LateUpdate()
    {
        if (m_Target == null)
            return;
        Vector3 desiredPosition = GetDesiredPosition();
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref m_Velocity,
            m_SmoothTime,
            m_MaxSpeed,
            Time.deltaTime
        );
    }
    private Vector3 GetDesiredPosition()
    {
        float lookAheadTarget = 0f;
        if (m_UseLookAhead && m_TargetRB != null)
        {
            float horizontalSpeed = m_TargetRB.linearVelocityX;
            if (Mathf.Abs(horizontalSpeed) > 0.15f)
            {
                lookAheadTarget =
                    Mathf.Sign(horizontalSpeed) * m_LookAheadDistance;
            }
        }
        m_CurrentLookAhead = Mathf.SmoothDamp(
            m_CurrentLookAhead,
            lookAheadTarget,
            ref m_LookAheadVelocity,
            m_LookAheadSmoothTime
        );
        return new Vector3(
            m_Target.position.x + m_CurrentLookAhead,
            m_Target.position.y + m_HeightOffset,
            -m_Distance
        );
    }
}