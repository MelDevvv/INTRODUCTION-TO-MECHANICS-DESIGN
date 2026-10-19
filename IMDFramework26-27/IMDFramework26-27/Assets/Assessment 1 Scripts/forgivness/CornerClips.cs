using UnityEngine;
public class CornerClipOnJump : MonoBehaviour
{
    [Header("Corner Detectors")]
    [SerializeField] private Collider2D m_LeftCornerDetector;
    [SerializeField] private Collider2D m_RightCornerDetector;
    [Header("Corner Clip")]
    [SerializeField] private bool m_CornerClipEnabled = true;
    [SerializeField] private float m_MaxNudgeDistance = 0.15f;
    [SerializeField] private float m_CheckStep = 0.025f;
    [SerializeField] private float m_SkinWidth = 0.04f;
    [SerializeField] private float m_Cooldown = 0.08f;
    [Header("Collision")]
    [SerializeField] private LayerMask m_GroundLayers;
    private Rigidbody2D m_RB;
    private BoxCollider2D m_PlayerCollider;
    private float m_NextCorrectionTime;
    private readonly Collider2D[] m_Overlaps = new Collider2D[32];
    private void Awake()
    {
        m_RB = GetComponent<Rigidbody2D>();
        m_PlayerCollider = GetComponent<BoxCollider2D>();
    }
    private void OnTriggerStay2D(Collider2D other)
    {
        if (!m_CornerClipEnabled ||
            m_RB == null ||
            m_PlayerCollider == null ||
            m_RB.linearVelocityY <= 0f ||
            Time.time < m_NextCorrectionTime)
        {
            return;
        }
        if (other != m_LeftCornerDetector &&
            other != m_RightCornerDetector)
        {
            return;
        }
        // Only react to solid objects on the Ground layer.
        if (other.isTrigger ||
            (m_GroundLayers.value &
            (1 << other.gameObject.layer)) == 0)
        {
            return;
        }
        // If the left detector catches a corner, try nudging right.
        // If the right detector catches a corner, try nudging left.
        float direction = other == m_LeftCornerDetector ? 1f : -1f;
        if (TryNudge(direction) || TryNudge(-direction))
        {
            m_NextCorrectionTime = Time.time + m_Cooldown;
        }
    }
    private bool TryNudge(float direction)
    {
        float step = Mathf.Max(0.01f, m_CheckStep);
        for (float distance = step;
             distance <= m_MaxNudgeDistance + 0.001f;
             distance += step)
        {
            Vector2 offset = new Vector2(direction * distance, 0f);
            if (!IsPositionClear(offset))
                continue;
            m_RB.position += offset;
            Physics2D.SyncTransforms();
            return true;
        }
        return false;
    }
    private bool IsPositionClear(Vector2 offset)
    {
        Bounds bounds = m_PlayerCollider.bounds;
        Vector2 size = new Vector2(
            Mathf.Max(0.01f, bounds.size.x - 2f * m_SkinWidth),
            Mathf.Max(0.01f, bounds.size.y - 2f * m_SkinWidth)
        );
        Vector2 centre = (Vector2)bounds.center + offset;
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(m_GroundLayers);
        filter.useTriggers = false;
        int count = Physics2D.OverlapBox(
            centre, size, 0f, filter, m_Overlaps
        );
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = m_Overlaps[i];
            if (hit != null &&
                hit != m_PlayerCollider &&
                !hit.isTrigger)
            {
                return false;
            }
        }
        return true;
    }
}