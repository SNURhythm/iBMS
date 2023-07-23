using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class Input : MonoBehaviour
{
    [SerializeField] public GameObject LaneArea; // TODO: use config instead
    private RhythmControl rhythmControl;

    private void Awake()
    {
        // set polling frequency to 1000Hz
        InputSystem.pollingFrequency = 1000;
        // set fixed update rate to 1000Hz
        Time.fixedDeltaTime = 1.0f / 1000.0f;
    }

    private void Start()
    {
        rhythmControl = GetComponent<RhythmControl>();
        Touch.onFingerMove += FingerMove;
        Touch.onFingerDown += FingerDown;
        Touch.onFingerUp += FingerUp;
    }


    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        TouchSimulation.Enable();
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
        TouchSimulation.Disable();
    }


    private void FingerMove(Finger obj)
    {
        if (obj.currentTouch.screenPosition.x < 0 || obj.currentTouch.screenPosition.x > Screen.width ||
    obj.currentTouch.screenPosition.y < 0 || obj.currentTouch.screenPosition.y > Screen.height) return;
        // TODO: remove this

        // //Debug.Log("Finger Move[" + obj.index + "]: " + obj.currentTouch.screenPosition + " " + enhancedTouchCnt);
        // // draw circle at finger position
        // // first transform finger position to world position
        // // check range
        // if (obj.currentTouch.screenPosition.x < 0 || obj.currentTouch.screenPosition.x > Screen.width ||
        //     obj.currentTouch.screenPosition.y < 0 || obj.currentTouch.screenPosition.y > Screen.height) return;
        // if (Camera.main == null) return;
        // var worldPosition = Camera.main.ScreenToWorldPoint(new Vector3(obj.currentTouch.screenPosition.x,
        //     obj.currentTouch.screenPosition.y, 10));
        // // then summon a circle at that position
        // var circle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        // circle.transform.position = worldPosition;
        // circle.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
        // // change color
        // circle.GetComponent<Renderer>().material.color = Color.red;
        var laneNumber = ToLaneNumber(obj.currentTouch.screenPosition);
        if (laneNumber >= 0 && laneNumber < 8)
            rhythmControl.FingerMove(obj, laneNumber);
    }

    private int ToLaneNumber(Vector2 screenPosition)
    {
        var z = LaneArea.transform.position.z - LaneArea.transform.localScale.y / 2 * Mathf.Sin(LaneArea.transform.rotation.eulerAngles.x * Mathf.Deg2Rad);
        var worldPosition = Camera.main.ScreenToWorldPoint(new Vector3(screenPosition.x,
            screenPosition.y, z));
        var lanePosition = worldPosition.x + 3.0f * 8 / 2; // TODO: use constants
        return (int)((lanePosition / 3.0f) + 7) % 8;
    }

    private void FingerDown(Finger obj)
    {
        if (obj.currentTouch.screenPosition.x < 0 || obj.currentTouch.screenPosition.x > Screen.width ||
            obj.currentTouch.screenPosition.y < 0 || obj.currentTouch.screenPosition.y > Screen.height) return;
        if (Camera.main == null) return;

        var laneNumber = ToLaneNumber(obj.currentTouch.screenPosition);
        if (laneNumber >= 0 && laneNumber < 8)
            rhythmControl.FingerDown(obj, laneNumber);
    }

    private void FingerUp(Finger obj)
    {
        if (obj.currentTouch.screenPosition.x < 0 || obj.currentTouch.screenPosition.x > Screen.width ||
    obj.currentTouch.screenPosition.y < 0 || obj.currentTouch.screenPosition.y > Screen.height) return;
        var laneNumber = ToLaneNumber(obj.currentTouch.screenPosition);
        if (laneNumber >= 0 && laneNumber < 8)
            rhythmControl.FingerUp(obj, laneNumber);
    }
}