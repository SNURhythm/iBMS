using System;
using System.Collections.Generic;
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
    [SerializeField] public GameObject NoteArea;
    private RhythmControl rhythmControl;
    private IDisposable eventListener;
    private int keyCount;
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
    }

    private void FixedUpdate()
    {
        InputSystem.Update();
    }


    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();

        eventListener = InputSystem.onEvent.ForDevice<Keyboard>().Call(OnEvent);
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInFixedUpdate;
        Touch.onFingerMove += FingerMove;
        Touch.onFingerDown += FingerDown;
        Touch.onFingerUp += FingerUp;
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
        eventListener.Dispose();
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInFixedUpdate;
        Touch.onFingerMove -= FingerMove;
        Touch.onFingerDown -= FingerDown;
        Touch.onFingerUp -= FingerUp;
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
                    if (GameManager.Instance.KeyMode == 7) laneNumber = 0;
                    break;
                case Key.S:
                    if (GameManager.Instance.KeyMode == 7) laneNumber = 1;
                    else laneNumber = 0;
                    break;
                case Key.D:
                    if (GameManager.Instance.KeyMode == 7) laneNumber = 2;
                    else laneNumber = 1;
                    break;
                case Key.Space:
                    if (GameManager.Instance.KeyMode == 7) laneNumber = 3;
                    else laneNumber = 2;
                    break;
                case Key.L:
                    if (GameManager.Instance.KeyMode == 7) laneNumber = 4;
                    else laneNumber = 3;
                    break;
                case Key.Semicolon:
                    if (GameManager.Instance.KeyMode == 7) laneNumber = 5;
                    else laneNumber = 4;
                    break;
                case Key.Quote:
                    if (GameManager.Instance.KeyMode == 7) laneNumber = 6;
                    break;
                case Key.LeftShift:
                    laneNumber = 7;
                    break;
            }
            if (laneNumber >= 0)
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
        // if (laneNumber >= 0 && laneNumber < (GameManager.Instance.KeyCount+1))
        //     rhythmControl.FingerMove(obj, laneNumber);
    }

    private int ToLaneNumber(Vector2 screenPosition)
    {
        var z = LaneArea.transform.position.z - LaneArea.transform.localScale.y / 2 * Mathf.Sin(LaneArea.transform.rotation.eulerAngles.x * Mathf.Deg2Rad);
        var worldPosition = Camera.main.ScreenToWorldPoint(new Vector3(screenPosition.x,
            screenPosition.y, z));
        var laneWidth = NoteArea.transform.localScale.x / (GameManager.Instance.KeyMode+1);
        var lanePosition = worldPosition.x + laneWidth * (GameManager.Instance.KeyMode+1) / 2; // TODO: use constants
        if (lanePosition < 0 || lanePosition > laneWidth * (GameManager.Instance.KeyMode+1)) return -1;
        var laneNumber = (int)((lanePosition / laneWidth) + GameManager.Instance.KeyMode) % (GameManager.Instance.KeyMode+1);
        Debug.Log("Lane Number: " + laneNumber);
        if (laneNumber == GameManager.Instance.KeyMode) return 7;
        return laneNumber;
    }

    private readonly Dictionary<int, int> fingerToLane = new();
    private readonly int[] laneFingerCount = new int[8];
    private void FingerDown(Finger obj)
    {
        if (GameManager.Instance.AutoPlay) return;
        if (obj.currentTouch.screenPosition.x < 0 || obj.currentTouch.screenPosition.x > Screen.width ||
            obj.currentTouch.screenPosition.y < 0 || obj.currentTouch.screenPosition.y > (float)Screen.height/2) return;
        if (Camera.main == null) return;

        var laneNumber = ToLaneNumber(obj.currentTouch.screenPosition);
        if (laneNumber < 0 || laneNumber >= 8) return;
        if (fingerToLane.ContainsKey(obj.index)) return;
        fingerToLane.Add(obj.index, laneNumber);
        laneFingerCount[laneNumber]++;
        rhythmControl.PressLane(laneNumber);
    }

    private void FingerUp(Finger obj)
    {
        if (GameManager.Instance.AutoPlay) return;

        if (!fingerToLane.ContainsKey(obj.index)) return;
        var laneNumber = fingerToLane[obj.index];
        fingerToLane.Remove(obj.index);
        laneFingerCount[laneNumber]--;
        if (laneFingerCount[laneNumber] == 0) rhythmControl.ReleaseLane(laneNumber);
    }
}