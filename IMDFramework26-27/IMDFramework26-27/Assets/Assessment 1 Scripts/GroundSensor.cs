using UnityEngine;
public class GroundSensor : MonoBehaviour
{
    [SerializeField] private LayerMask m_GroundLayer;
    [SerializeField] private float m_RaycastDistance = 0.15f;
    public bool IsGrounded()
    {
        return Physics2D.Raycast(
            transform.position,
            Vector2.down,
            m_RaycastDistance,
            m_GroundLayer
        );
    }
}