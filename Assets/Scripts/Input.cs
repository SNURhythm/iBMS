using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class Input : MonoBehaviour
{
    [SerializeField] public GameObject LaneArea; // TODO: use config instead
    private RhythmControl rhythmControl;
    private IDisposable eventListener;
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

    private void FixedUpdate()
    {
        InputSystem.Update();
    }


    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        TouchSimulation.Enable();

        eventListener = InputSystem.onEvent.ForDevice<Keyboard>().Call(OnEvent);
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInFixedUpdate;
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
        TouchSimulation.Disable();
        eventListener.Dispose();
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInFixedUpdate;
    }

    private void OnEvent(InputEventPtr eventPtr)
    {
        if (GameManager.Instance.AutoPlay) return;
        if (!eventPtr.IsA<StateEvent>()) return;
        // get key
        foreach (var control in eventPtr.EnumerateChangedControls())
        {
            var device = control.device;
            var keyboard = device as Keyboard;
            //get keycode
            var key = control as KeyControl;

            if (key == null) continue;
            var laneNumber = -1;
            switch (key.keyCode)
            {
                case Key.A:
                    laneNumber = 0;
                    break;
                case Key.S:
                    laneNumber = 1;
                    break;
                case Key.D:
                    laneNumber = 2;
                    break;
                case Key.Space:
                    laneNumber = 3;
                    break;
                case Key.L:
                    laneNumber = 4;
                    break;
                case Key.Semicolon:
                    laneNumber = 5;
                    break;
                case Key.Quote:
                    laneNumber = 6;
                    break;
                case Key.LeftShift:
                    laneNumber = 7;
                    break;
            }
            if (laneNumber >= 0 && laneNumber < 8)
            {
                if (!key.IsPressed())
                {
                    rhythmControl.PressLane(laneNumber, Time.realtimeSinceStartupAsDouble - eventPtr.time);
                }
                else
                {
                    rhythmControl.ReleaseLane(laneNumber, Time.realtimeSinceStartupAsDouble - eventPtr.time);
                }
            }

        }

    }


    private void FingerMove(Finger obj)
    {
        if (GameManager.Instance.AutoPlay) return;
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
        // var laneNumber = ToLaneNumber(obj.currentTouch.screenPosition);
        // if (laneNumber >= 0 && laneNumber < 8)
        //     rhythmControl.FingerMove(obj, laneNumber);
    }

    private int ToLaneNumber(Vector2 screenPosition)
    {
        var z = LaneArea.transform.position.z - LaneArea.transform.localScale.y / 2 * Mathf.Sin(LaneArea.transform.rotation.eulerAngles.x * Mathf.Deg2Rad);
        var worldPosition = Camera.main.ScreenToWorldPoint(new Vector3(screenPosition.x,
            screenPosition.y, z));
        var lanePosition = worldPosition.x + 3.0f * 8 / 2; // TODO: use constants
        if (lanePosition < 0 || lanePosition > 3.0f * 8) return -1;
        return (int)((lanePosition / 3.0f) + 7) % 8;
    }

    int[] touchingFingers = new int[8] { -1, -1, -1, -1, -1, -1, -1, -1 };

    private void FingerDown(Finger obj)
    {
        if (GameManager.Instance.AutoPlay) return;
        if (obj.currentTouch.screenPosition.x < 0 || obj.currentTouch.screenPosition.x > Screen.width ||
            obj.currentTouch.screenPosition.y < 0 || obj.currentTouch.screenPosition.y > Screen.height) return;
        if (Camera.main == null) return;

        var laneNumber = ToLaneNumber(obj.currentTouch.screenPosition);
        if (laneNumber >= 0 && laneNumber < 8)
        {
            if (touchingFingers[laneNumber] == -1)
            {
                rhythmControl.PressLane(laneNumber);
            }
            touchingFingers[laneNumber] = obj.index;
        }
    }

    private void FingerUp(Finger obj)
    {
        if (GameManager.Instance.AutoPlay) return;
        if (obj.currentTouch.screenPosition.x < 0 || obj.currentTouch.screenPosition.x > Screen.width ||
            obj.currentTouch.screenPosition.y < 0 || obj.currentTouch.screenPosition.y > Screen.height) return;
        var laneNumber = ToLaneNumber(obj.currentTouch.screenPosition);
        if (laneNumber >= 0 && laneNumber < 8)
        {
            if (touchingFingers[laneNumber] == obj.index)
            {
                touchingFingers[laneNumber] = -1;
                rhythmControl.ReleaseLane(laneNumber);
            }
        }
    }
}