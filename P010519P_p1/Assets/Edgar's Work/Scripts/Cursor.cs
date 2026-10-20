using UnityEngine;
using UnityEngine.InputSystem;

public class CustomCursor : MonoBehaviour
{
    // things we need to make the cursor show up on the menu
    public RectTransform cursorImage;
    public Canvas canvas;
    public Camera uiCamera;

    void Start()
    {
        // hide the ugly windows mouse
        Cursor.visible = false;

        // make sure our custom one is off at the start
        if (cursorImage != null)
            cursorImage.gameObject.SetActive(false);
    }

    void Update()
    {
        // if we forgot to plug something in, just stop so it doesnt crash
        if (cursorImage == null || canvas == null || uiCamera == null) return;

        // get where the mouse is using the new input system
        Vector2 mousePos = Mouse.current.position.ReadValue();

        Vector2 pos;

        // math to turn the screen pixel position into a spot on our UI canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                  canvas.transform as RectTransform,
                  mousePos,
                  uiCamera,
                  out pos
              );

        // move our image to that spot
        cursorImage.localPosition = pos;
    }

    // call these from other scripts like the Pause Menu
    public void ShowCursor()
    {
        if (cursorImage != null)
            cursorImage.gameObject.SetActive(true);
    }

    public void HideCursor()
    {
        if (cursorImage != null)
            cursorImage.gameObject.SetActive(false);
    }
}