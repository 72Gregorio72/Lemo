using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
public class CurvedText : MonoBehaviour
{
    [Header("Curve Settings")]
    [SerializeField] private float rotationY = 10f;
    [SerializeField] private float spacing = 1f;

    private TextMeshProUGUI textComponent;
    private string lastText;

    private void Awake()
    {
        textComponent = GetComponent<TextMeshProUGUI>();
        lastText = textComponent.text;
        CurveText();
    }

    private void LateUpdate()
    {
        // Only curve the text if it has changed
        if (textComponent.text != lastText)
        {
            lastText = textComponent.text;
            CurveText();
        }
    }

    private void OnEnable()
    {
        // Apply curve when object is enabled
        CurveText();
    }

    private void CurveText()
    {
        if (textComponent == null || textComponent.textInfo.characterCount == 0)
            return;

        // Force text mesh to update
        textComponent.ForceMeshUpdate();

        var textInfo = textComponent.textInfo;
        var characterCount = textInfo.characterCount;

        for (int i = 0; i < characterCount; i++)
        {
            if (!textInfo.characterInfo[i].isVisible)
                continue;

            int materialIndex = textInfo.characterInfo[i].materialReferenceIndex;
            int vertexIndex = textInfo.characterInfo[i].vertexIndex;
            Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

            // Calculate the character's position relative to the center
            float normalizedPos = (float)i / (characterCount - 1) - 0.5f;
            
            // Calculate rotation and position
            float angle = normalizedPos * rotationY;
            float x = normalizedPos * spacing * 10f;
            float z = -Mathf.Abs(Mathf.Sin(angle * Mathf.Deg2Rad)) * spacing * 20f;

            // Get character center
            Vector3 centerPoint = (vertices[vertexIndex] + vertices[vertexIndex + 1] + 
                                 vertices[vertexIndex + 2] + vertices[vertexIndex + 3]) / 4f;

            // Create rotation for this character
            Quaternion rotation = Quaternion.Euler(0, angle, 0);

            // Apply transformation to each vertex
            for (int j = 0; j < 4; j++)
            {
                Vector3 vertex = vertices[vertexIndex + j];
                Vector3 offset = vertex - centerPoint;
                
                // Apply rotation around character center
                vertex = centerPoint + rotation * offset;
                // Apply position offset
                vertex += new Vector3(x, 0, z);
                
                vertices[vertexIndex + j] = vertex;
            }
        }

        // Apply the changes
        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            var meshInfo = textInfo.meshInfo[i];
            meshInfo.mesh.vertices = meshInfo.vertices;
            textComponent.UpdateGeometry(meshInfo.mesh, i);
        }
    }
} 