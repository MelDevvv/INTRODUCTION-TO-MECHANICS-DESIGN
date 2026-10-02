using UnityEngine;
public class JumpBuffer : MonoBehaviour
{
    [SerializeField] private float m_JumpBufferTime = 0.15f;
    private float m_JumpBufferCounter;
    public void JumpPressed()
    {
        m_JumpBufferCounter = m_JumpBufferTime;
    }
    private void Update()
    {
        if (m_JumpBufferCounter > 0f)
        {
            m_JumpBufferCounter -= Time.deltaTime;
        }
    }
    public bool HasBufferedJump()
    {
        return m_JumpBufferCounter > 0f;
    }
    public void UseBufferedJump()
    {
        m_JumpBufferCounter = 0f;
    }
}