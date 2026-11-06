
using UnityEngine;

/// <summary>
/// Allows GameplaySceneBootstrap to enable and disable the
/// existing PlayerController input without disabling the player.
/// 
/// Requires the existing PlayerController to be declared as:
/// public partial class PlayerController
/// </summary>
public partial class PlayerController
{
    /// <summary>
    /// Enables or disables movement, jumping and crouching.
    /// Physics, animation and visual spinning remain active.
    /// </summary>
    public void SetGameplayInputEnabled(bool inputEnabled)
    {
        if (moveAction == null ||
            jumpAction == null ||
            crouchAction == null)
        {
            Debug.LogWarning(
                "PlayerController input actions have not been initialised.",
                this
            );

            return;
        }

        if (inputEnabled)
        {
            moveAction.Enable();
            jumpAction.Enable();
            crouchAction.Enable();
        }
        else
        {
            // Clear any input already stored.
            moveInput = 0f;
            jumpHeld = false;
            jumpBufferCounter = 0f;
            jumpWasBufferedInAir = false;

            // Disable controls only.
            // Do not disable the PlayerController component.
            moveAction.Disable();
            jumpAction.Disable();
            crouchAction.Disable();
        }
    }
}
