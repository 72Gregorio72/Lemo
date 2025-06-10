using UnityEngine;

public class SphericalBackgroundScroller : MonoBehaviour
{
    public Material skyboxMaterial;
    public float rotationSpeed = 10f;
    
    void Update()
    {
        float currentAngle = skyboxMaterial.GetFloat("_RotationSpeed");
        skyboxMaterial.SetFloat("_RotationSpeed", rotationSpeed);  
    }
}
