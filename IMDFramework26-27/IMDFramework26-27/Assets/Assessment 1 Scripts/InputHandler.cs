using UnityEngine;
using UnityEngine.InputSystem;
public class InputHandler : MonoBehaviour
{
    [SerializeField] private CharacterMovement m_CharacterMovement;
    private PlayerControls m_ActionMap;
    private void Awake()
    {
        m_ActionMap = new PlayerControls();
    }
    private void OnEnable()
    {
        m_ActionMap.Enable();
        m_ActionMap.Default.MoveHoriz.performed += Handle_MovePerformed;
        m_ActionMap.Default.MoveHoriz.canceled += Handle_MoveCancelled;
        m_ActionMap.Default.Jump.performed += Handle_JumpPerformed;
        m_ActionMap.Default.Jump.canceled += Handle_JumpCancelled;
    }
    private void OnDisable()
    {
        m_ActionMap.Default.MoveHoriz.performed -= Handle_MovePerformed;
        m_ActionMap.Default.MoveHoriz.canceled -= Handle_MoveCancelled;
        m_ActionMap.Default.Jump.performed -= Handle_JumpPerformed;
        m_ActionMap.Default.Jump.canceled -= Handle_JumpCancelled;
        m_ActionMap.Disable();
    }
    private void Handle_MovePerformed(InputAction.CallbackContext context)
    {
        float direction = context.ReadValue<float>();
        m_CharacterMovement.SetInMove(direction);
    }
    private void Handle_MoveCancelled(InputAction.CallbackContext context)
    {
        m_CharacterMovement.SetInMove(0f);
    }
    private void Handle_JumpPerformed(InputAction.CallbackContext context)
    {
        m_CharacterMovement.JumpPressed();
    }
    private void Handle_JumpCancelled(InputAction.CallbackContext context)
    {
        m_CharacterMovement.JumpCancelled();
    }
}