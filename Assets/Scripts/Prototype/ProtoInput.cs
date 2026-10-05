using UnityEngine;
using UnityEngine.EventSystems;

public enum PointerPhase { None, Began, Held, Ended }

// One-finger pointer abstraction over mouse and touch, so gameplay code handles a single gesture.
public static class ProtoInput
{
    public static PointerPhase Poll(out Vector2 position, out bool overUI)
    {
        EventSystem es = EventSystem.current;

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            position = touch.position;
            overUI = es != null && es.IsPointerOverGameObject(touch.fingerId);

            switch (touch.phase)
            {
                case TouchPhase.Began: return PointerPhase.Began;
                case TouchPhase.Moved:
                case TouchPhase.Stationary: return PointerPhase.Held;
                default: return PointerPhase.Ended;
            }
        }

        position = Input.mousePosition;
        overUI = es != null && es.IsPointerOverGameObject();

        if (Input.GetMouseButtonDown(0)) return PointerPhase.Began;
        if (Input.GetMouseButton(0)) return PointerPhase.Held;
        if (Input.GetMouseButtonUp(0)) return PointerPhase.Ended;
        return PointerPhase.None;
    }
}
