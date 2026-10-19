using UnityEngine;
public class BumpedHeadCorrection : MonoBehaviour
{
    [Header("Head Bump Correction")]
    [SerializeField] private bool m_CorrectionEnabled = true;
    [SerializeField] private Collider2D m_HeadCollider;
    [Header("Correction")]
    [SerializeField] private float m_MaxCorrectionDistance = 0.35f;
    [SerializeField] private float m_CheckStep = 0.025f;
    [SerializeField] private float m_SkinWidth = 0.04f;
    [SerializeField] private float m_CorrectionCooldown = 0.12f;
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
        if (!m_CorrectionEnabled ||
            m_RB == null ||
            m_PlayerCollider == null ||
            m_HeadCollider == null ||
            !m_HeadCollider.enabled ||
            !other ||
            other.isTrigger ||
            Time.time < m_NextCorrectionTime ||
            m_RB.linearVelocityY <= 0.05f)
        {
            return;
        }
        if ((m_GroundLayers.value & (1 << other.gameObject.layer)) == 0)
            return;
        // Only correct when the head detector overlaps this platform.
        if (!m_HeadCollider.IsTouching(other))
            return;
        float horizontalVelocity = m_RB.linearVelocityX;
        // Prefer movement direction, then try the other side.
        if (horizontalVelocity > 0.01f)
        {
            if (!TryDirection(1f))
                TryDirection(-1f);
        }
        else if (horizontalVelocity < -0.01f)
        {
            if (!TryDirection(-1f))
                TryDirection(1f);
        }
        else
        {
            if (!TryDirection(1f))
                TryDirection(-1f);
        }
    }
    private bool TryDirection(float direction)
    {
        float step = Mathf.Max(0.01f, m_CheckStep);
        for (float distance = step;
             distance <= m_MaxCorrectionDistance + 0.001f;
             distance += step)
        {
            Vector2 offset = new Vector2(direction * distance, 0f);
            if (!IsPositionClear(offset))
                continue;
            // Preserve the Rigidbody2D's velocity.
            m_RB.position += offset;
            Physics2D.SyncTransforms();
            m_NextCorrectionTime = Time.time + m_CorrectionCooldown;
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
                hit != m_HeadCollider &&
                !hit.isTrigger)
            {
                return false;
            }
        }
        return true;
    }
}