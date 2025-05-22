using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;


public class PalmUpTriggerOnOff : MonoBehaviour
{
    [Header("Hand & Device Settings")]
    public XRNode handNode = XRNode.RightHand;
    public Transform handTransform;

    [Header("Palm-Up Detection")]
    [Tooltip("Dot product threshold to determine palm facing up.")]
    [Range(-1f, 1f)] public float palmUpThreshold = 0.7f;

    [Header("Rotation Direction Filtering")]
    [Tooltip("Minimum rotational delta (degrees) around X to detect counterclockwise motion.")]
    public float rotationDirectionThreshold = 0.2f;

    [Header("Spawn Settings")]
    public GameObject objectToSpawn;

    [Header("Debug")]
    public bool debugRays = true;
    public bool logRotation = false;

    private bool hasSpawned = false;
    private Quaternion previousRotation;

    // Update is called once per frame

    void Start()
    {
        objectToSpawn.SetActive(false);
        InputDevice device = InputDevices.GetDeviceAtXRNode(handNode);
        if (device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot))
        {
            previousRotation = rot;
        }
    }

    void Update()
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(handNode);
        if (!device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion currentRotation))
            return;

        // Step 1: Use correct palm normal (based on -X direction)
        Vector3 palmNormal = currentRotation * -Vector3.right;
        float dot = Vector3.Dot(palmNormal.normalized, Vector3.up);

        if (debugRays)
        {
            Debug.DrawRay(handTransform.position, palmNormal.normalized * 0.2f, Color.green);
        }

        // Step 2: Detect rotation around local X axis (twisting up/down)
        Quaternion deltaRotation = currentRotation * Quaternion.Inverse(previousRotation);
        deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);
        float xDirection = axis.x * angle;

        if (logRotation)
        {
            //Debug.Log($"Palm Dot: {dot:F3}, Axis: {axis}, Angle: {angle:F2}, XRotDelta: {xDirection:F2}");
        }

        // Step 3: Trigger gesture (palm up + rotating counterclockwise around X)
        if (!hasSpawned && dot > palmUpThreshold && xDirection > rotationDirectionThreshold)
        {
            SpawnParticle();
        }

        // Update previous rotation
        previousRotation = currentRotation;

        
    }

    void SpawnParticle()
    {
        objectToSpawn.SetActive(true);
        hasSpawned = true;

        
    }
}
