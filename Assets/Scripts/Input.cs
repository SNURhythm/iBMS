
using System;
using System.Collections;
using FMOD;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Debug = UnityEngine.Debug;
using Thread = System.Threading.Thread;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;


public class Input : MonoBehaviour
{
    private RhythmControl rhythmControl;
    private int enhancedTouchCnt = 0;

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

    private void Awake()
    {
        rhythmControl = GetComponent<RhythmControl>();

        Debug.Log("Input Awake");
        // set polling frequency to 1000Hz
        InputSystem.pollingFrequency = 1000;
        // set fixed update rate to 1000Hz
        Time.fixedDeltaTime = 1.0f / 1000.0f;
    }

    private void Start()
    {
        Touch.onFingerMove += FingerMove;
        Touch.onFingerDown += FingerDown;
        Touch.onFingerUp += FingerUp;

    }


    private void FingerMove(Finger obj)
    {
        // TODO: remove this
        rhythmControl.FingerMove(obj);
        //Debug.Log("Finger Move[" + obj.index + "]: " + obj.currentTouch.screenPosition + " " + enhancedTouchCnt);
        // draw circle at finger position
        // first transform finger position to world position
        // check range
        if (obj.currentTouch.screenPosition.x < 0 || obj.currentTouch.screenPosition.x > Screen.width || obj.currentTouch.screenPosition.y < 0 || obj.currentTouch.screenPosition.y > Screen.height)
        {
            return;
        }
        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(new Vector3(obj.currentTouch.screenPosition.x, obj.currentTouch.screenPosition.y, 10));
        // then summon a circle at that position
        GameObject circle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        circle.transform.position = worldPosition;
        circle.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
        // change color
        circle.GetComponent<Renderer>().material.color = Color.red;
    }

    private void FingerDown(Finger obj)
    {
        //Debug.Log("Finger Down[" + obj.index + "]: " + obj.currentTouch.screenPosition + " isActive: " + obj.isActive);
    }

    private void FingerUp(Finger obj)
    {
        //Debug.Log("Finger Up[" + obj.index + "]: " + obj.currentTouch.screenPosition + " isActive: " + obj.isActive);
    }


}
