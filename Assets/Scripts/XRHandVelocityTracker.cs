using UnityEngine;
using UnityEngine.XR;

public class XRHandVelocityTracker : MonoBehaviour
{
    private InputDevice leftControllerDevice;
    private InputDevice rightControllerDevice;

    public Vector3 LeftControllerVelocity { get; private set; }
    public Vector3 RightControllerVelocity { get; private set; }

    void Start()
    {
        leftControllerDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        rightControllerDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
    }

    void Update()
    {
        UpdateInput();
    }

    void UpdateInput()
    {
        if (!leftControllerDevice.isValid)
            leftControllerDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        if (!rightControllerDevice.isValid)
            rightControllerDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        if (leftControllerDevice.TryGetFeatureValue(CommonUsages.deviceVelocity, out Vector3 leftVelocity))
        {
            LeftControllerVelocity = leftVelocity;
            Debug.Log($"Left Controller Speed: {leftVelocity.magnitude:F3} m/s");
        }

        if (rightControllerDevice.TryGetFeatureValue(CommonUsages.deviceVelocity, out Vector3 rightVelocity))
        {
            RightControllerVelocity = rightVelocity;
            Debug.Log($"Right Controller Speed: {rightVelocity.magnitude:F3} m/s");
        }
    }
}